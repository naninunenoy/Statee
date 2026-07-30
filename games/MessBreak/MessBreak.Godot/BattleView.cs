using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MessBreak.Logic;

namespace MessBreak;

/// <summary>
/// 盤面描画ノード。演出タイマー・Events→音/マーカー翻訳・1 フレーム描画を担う。
/// スプライト選択は BattleSprites、座標変換は GameCamera に委譲する。
/// </summary>
public sealed partial class BattleView : Node2D
{
    private const int HitMarkerFrames = 12;
    private const int EnemyFlashFrames = 4;
    private const int BurstMarkerFrames = 18;
    private const float FacingLerp = 0.35f;

    // 床・壁のタイルシート(art/tiles.sprite.txt)。1 コマ = ステージ 1 マス
    private const int TileCell = 40;
    private const int TileSheetRowFloor = 0;
    private const int TileSheetRowWall = 1;
    private const int TileSheetColumns = 4;
    private const float WallShadowDepth = 6f;

    private readonly GameCamera _camera;
    private readonly BattleSprites _sprites = new();
    private readonly List<(float Y, Enemy? Enemy)> _actors = [];
    private readonly Dictionary<int, int> _enemyFlashFrames = new();
    private readonly List<(System.Numerics.Vector2 Pos, int Frames)> _hitMarkers = [];
    private readonly List<(System.Numerics.Vector2 Pos, int Frames, float Radius)> _burstMarkers =
    [];

    private BattleLogic _logic = null!;
    private bool _paused;
    private int _hitstopFrames;
    private float _displayFacingAngle;
    private int _walkPhase = -1;
    private System.Numerics.Vector2 _lastPlayerPos;

    public BattleView(GameCamera camera)
    {
        _camera = camera;
        Name = "BattleView";
        // ドット絵は最近傍拡大で描く(にじみ防止)
        TextureFilter = TextureFilterEnum.Nearest;
    }

    public GameCamera Camera => _camera;

    /// <summary>ヒットストップ中か。Main の論理 tick を止める判定に使う。</summary>
    public bool IsInHitstop => _hitstopFrames > 0;

    /// <summary>見た目の向き(描画に使う滑らかな向き)。</summary>
    public System.Numerics.Vector2 DisplayFacing =>
        new(MathF.Cos(_displayFacingAngle), MathF.Sin(_displayFacingAngle));

    public void LoadAssets() => _sprites.Load(this);

    public void Bind(BattleLogic logic, bool paused)
    {
        _logic = logic;
        _paused = paused;
    }

    /// <summary>ミッション再開時に演出の残骸を消す。</summary>
    public void ResetPresentation(System.Numerics.Vector2 playerPos)
    {
        _camera.Position = playerPos;
        _camera.ZoomFactor = 1f;
        _hitMarkers.Clear();
        _burstMarkers.Clear();
        _hitstopFrames = 0;
        _enemyFlashFrames.Clear();
        _displayFacingAngle = 0f;
        _walkPhase = -1;
        _lastPlayerPos = playerPos;
    }

    /// <summary>physics フレームでヒットストップを1減らす。残っている間は論理を進めない。</summary>
    public void TickHitstop() => _hitstopFrames--;

    /// <summary>
    /// 論理 tick 1 回ぶん演出タイマーを進める。自動 tick と tick コマンドの両方から呼ぶことで、
    /// freeze + tick でも実時間と同じ歩調で減衰し、見た目の検証が決定論的になる(D-079)。
    /// </summary>
    public void AdvanceEffectTimers()
    {
        for (var i = 0; i < _hitMarkers.Count; i++)
        {
            _hitMarkers[i] = _hitMarkers[i] with { Frames = _hitMarkers[i].Frames - 1 };
        }
        _hitMarkers.RemoveAll(m => m.Frames <= 0);
        for (var i = 0; i < _burstMarkers.Count; i++)
        {
            _burstMarkers[i] = _burstMarkers[i] with { Frames = _burstMarkers[i].Frames - 1 };
        }
        _burstMarkers.RemoveAll(m => m.Frames <= 0);
        foreach (var id in _enemyFlashFrames.Keys.ToArray())
        {
            if (--_enemyFlashFrames[id] <= 0)
            {
                _enemyFlashFrames.Remove(id);
            }
        }
        // 歩行は「実際に動いたか」で進める。入力ではなく位置差分を見るので、
        // 壁に押し付けて動けていないときは足が止まる
        _walkPhase = _logic.PlayerPos == _lastPlayerPos ? -1 : _walkPhase + 1;
        _lastPlayerPos = _logic.PlayerPos;
        // 見た目の向きも最短弧で滑らかに追従させる(ロジックの向きは即時)。
        // 実時間ではなく論理 tick で回すので、freeze + tick で決定的に観測できる(D-079)
        _displayFacingAngle = Mathf.LerpAngle(
            _displayFacingAngle,
            MathF.Atan2(_logic.PlayerFacing.Y, _logic.PlayerFacing.X),
            FacingLerp
        );
    }

    /// <summary>ロジックの Events を音・演出へ翻訳する。</summary>
    public void ApplyEvents()
    {
        foreach (var battleEvent in _logic.Events)
        {
            switch (battleEvent.Kind)
            {
                case BattleEventKind.BulletFired:
                    _sprites.PlayShot();
                    break;
                case BattleEventKind.EnemyHit:
                    _hitMarkers.Add((battleEvent.Pos, HitMarkerFrames));
                    _enemyFlashFrames[battleEvent.EnemyId] = EnemyFlashFrames;
                    _hitstopFrames = 2;
                    break;
                case BattleEventKind.EnemyKilled:
                    _hitstopFrames = 6;
                    break;
                case BattleEventKind.TurretFired:
                    _sprites.PlayShot();
                    break;
                case BattleEventKind.BossAppeared:
                    _hitstopFrames = 6;
                    _sprites.PlaySkill();
                    break;
                case BattleEventKind.MissionCleared:
                    _hitstopFrames = 12;
                    break;
                case BattleEventKind.SkillBurst:
                    _burstMarkers.Add(
                        (
                            battleEvent.Pos,
                            BurstMarkerFrames,
                            _logic.Config.CharacterOf(_logic.ActiveCharacter).SkillRadius
                        )
                    );
                    _sprites.PlaySkill();
                    break;
            }
        }
    }

    /// <summary>カメラ更新。毎フレーム(描画前)に呼ぶ。</summary>
    public void UpdateCamera(Vector2 mouseScreen)
    {
        var ads = Input.IsMouseButtonPressed(MouseButton.Right);
        _camera.Update(
            _logic.PlayerPos,
            _camera.ToLogic(mouseScreen),
            ads,
            _logic.Stage.Width,
            _logic.Stage.Height
        );
    }

    /// <summary>ui/hud 用のプレイヤースプライト見た目。</summary>
    public (CharacterId Character, int Row, int Column, bool Mirror) PlayerSpriteAppearance()
    {
        var cell = BattleSprites.PlayerSpriteCell(DisplayFacing, _walkPhase);
        return (_logic.ActiveCharacter, cell.Row, cell.Column, cell.Mirror);
    }

    public override void _Draw()
    {
        var config = _logic.Config;
        var scale = _camera.ScaleFactor;

        // 床と壁(タイルシートから 1 マスずつ)
        var stage = _logic.Stage;
        var tileSize = stage.TileSize * scale;
        for (var row = 0; row < stage.Rows.Count; row++)
        {
            for (var col = 0; col < stage.Rows[row].Length; col++)
            {
                var solid = stage.IsSolidCell(col, row);
                var topLeft = _camera.ToScreen(
                    new System.Numerics.Vector2(col * stage.TileSize, row * stage.TileSize)
                );
                DrawTextureRectRegion(
                    _sprites.TileTexture,
                    new Rect2(topLeft, new Vector2(tileSize, tileSize)),
                    TileSource(solid ? TileSheetRowWall : TileSheetRowFloor, col, row)
                );
                // 壁の下隣が床なら、そこへ影を落とす(壁に厚みを感じさせる)
                if (!solid && row > 0 && stage.IsSolidCell(col, row - 1))
                {
                    DrawRect(
                        new Rect2(topLeft, new Vector2(tileSize, WallShadowDepth * scale)),
                        new Color(0f, 0f, 0f, 0.45f)
                    );
                }
            }
        }

        // 設置スロット(制圧後に見える。設置済みは塗り、未設置は枠だけ)
        if (_logic.ZoneCaptured)
        {
            var slotScreen = _camera.ToScreen(stage.TurretSlot);
            var half = 8f * scale;
            var slotRect = new Rect2(
                slotScreen - new Vector2(half, half),
                new Vector2(half * 2f, half * 2f)
            );
            if (_logic.TurretPlaced)
            {
                DrawRect(slotRect, new Color(0.45f, 0.8f, 0.5f));
                DrawCircle(slotScreen, 3f * scale, new Color(0.2f, 0.35f, 0.25f));
            }
            else
            {
                DrawRect(slotRect, new Color(0.45f, 0.8f, 0.5f, 0.8f), filled: false, width: 2f);
            }
        }

        // 強敵の出現ポイント(アトラクト前だけ菱形マーカーを出す)
        if (!_logic.BossAppeared)
        {
            var spawnScreen = _camera.ToScreen(stage.BossSpawn);
            var r = 6f * scale;
            DrawPolygon(
                [
                    spawnScreen + new Vector2(0, -r),
                    spawnScreen + new Vector2(r, 0),
                    spawnScreen + new Vector2(0, r),
                    spawnScreen + new Vector2(-r, 0),
                ],
                [new Color(1f, 0.35f, 0.35f, 0.75f)]
            );
        }

        // プレイヤー(ドッジ中は半透明)。キャラの見分けは専用スプライトが持つ
        var playerTint =
            _logic.PlayerAction == PlayerAction.Dodge
                ? Colors.White with
                {
                    A = 0.5f,
                }
                : Colors.White;
        var displayFacing = DisplayFacing;
        // 向きは常にカーソルが決めるので、照準線も常に出す。床の上に敷いてスプライトの下に置く
        DrawLine(
            _camera.ToScreen(_logic.PlayerPos),
            _camera.ToScreen(_logic.PlayerPos + displayFacing * 60f),
            new Color(1f, 1f, 1f, 0.15f),
            width: 1f
        );

        // 立っているものは Y 順に描く(足元が下にあるものほど手前。疑似 2.5D の前後関係)
        _actors.Clear();
        foreach (var enemy in _logic.Enemies)
        {
            _actors.Add((enemy.Pos.Y, enemy));
        }
        _actors.Add((_logic.PlayerPos.Y, null));
        _actors.Sort(static (a, b) => a.Y.CompareTo(b.Y));
        foreach (var (_, enemy) in _actors)
        {
            if (enemy is null)
            {
                _sprites.DrawPlayer(this, _camera, _logic, displayFacing, playerTint, _walkPhase);
                DrawNose(
                    _logic.PlayerPos,
                    displayFacing,
                    config.PlayerRadius,
                    new Color(1f, 0.9f, 0.75f)
                );
            }
            else
            {
                _sprites.DrawEnemy(
                    this,
                    _camera,
                    _logic,
                    enemy,
                    _enemyFlashFrames.ContainsKey(enemy.Id)
                );
            }
        }

        // 弾
        foreach (var bullet in _logic.Bullets)
        {
            DrawCircle(
                _camera.ToScreen(bullet.Pos),
                config.BulletRadius * scale,
                new Color(1f, 0.85f, 0.4f)
            );
        }

        // スキル爆発(爆心に半径いっぱいまで広がるリング)
        foreach (var (pos, frames, radius) in _burstMarkers)
        {
            var t = 1f - frames / (float)BurstMarkerFrames;
            DrawArc(
                _camera.ToScreen(pos),
                radius * t * scale,
                0f,
                Mathf.Tau,
                48,
                new Color(1f, 0.6f, 0.25f, 1f - t),
                width: 4f
            );
        }

        // ヒットマーカー(命中位置に広がって消えるリング)
        foreach (var (pos, frames) in _hitMarkers)
        {
            var t = 1f - frames / (float)HitMarkerFrames;
            DrawArc(
                _camera.ToScreen(pos),
                (4f + 8f * t) * scale,
                0f,
                Mathf.Tau,
                24,
                new Color(1f, 1f, 1f, 1f - t),
                width: 2f
            );
        }

        // 画面外の敵の方向インジケーター(画面端の三角矢印)
        foreach (var enemy in _logic.Enemies)
        {
            DrawOffscreenIndicator(_camera.ToScreen(enemy.Pos));
        }

        // レティクル(OS カーソルの代わり)。ポーズ中はメニュー操作用に OS カーソルを出すので消す
        if (!_paused)
        {
            DrawReticle(GetGlobalMousePosition(), Input.IsMouseButtonPressed(MouseButton.Right));
        }
    }

    private void DrawOffscreenIndicator(Vector2 targetScreen)
    {
        const float Margin = 28f;
        var gameRect = _camera.GameRect;
        var screenCenter = _camera.ScreenCenter;
        if (gameRect.HasPoint(targetScreen))
        {
            return;
        }
        var toTarget = targetScreen - screenCenter;
        if (toTarget == Vector2.Zero)
        {
            return;
        }
        var scaleX =
            toTarget.X == 0 ? float.MaxValue : (screenCenter.X - Margin) / Math.Abs(toTarget.X);
        var scaleY =
            toTarget.Y == 0 ? float.MaxValue : (screenCenter.Y - Margin) / Math.Abs(toTarget.Y);
        var edge = screenCenter + toTarget * Math.Min(scaleX, scaleY);

        var dir = toTarget.Normalized();
        var perp = new Vector2(-dir.Y, dir.X);
        DrawPolygon(
            [edge + dir * 12f, edge - dir * 4f + perp * 8f, edge - dir * 4f - perp * 8f],
            [new Color(1f, 0.6f, 0.9f, 0.9f)]
        );
    }

    private void DrawReticle(Vector2 pos, bool ads)
    {
        if (ads)
        {
            var color = new Color(1f, 1f, 1f, 0.9f);
            const float Gap = 4f;
            const float Arm = 8f;
            DrawLine(pos + new Vector2(Gap, 0), pos + new Vector2(Gap + Arm, 0), color, 1.5f);
            DrawLine(pos - new Vector2(Gap, 0), pos - new Vector2(Gap + Arm, 0), color, 1.5f);
            DrawLine(pos + new Vector2(0, Gap), pos + new Vector2(0, Gap + Arm), color, 1.5f);
            DrawLine(pos - new Vector2(0, Gap), pos - new Vector2(0, Gap + Arm), color, 1.5f);
            DrawCircle(pos, 1.5f, color);
        }
        else
        {
            DrawArc(pos, 7f, 0f, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.5f), width: 1.5f);
            DrawCircle(pos, 1.5f, new Color(1f, 1f, 1f, 0.5f));
        }
    }

    private void DrawNose(
        System.Numerics.Vector2 center,
        System.Numerics.Vector2 dir,
        float radius,
        Color color
    )
    {
        DrawLine(
            _camera.ToScreen(center + dir * radius * 0.5f),
            _camera.ToScreen(center + dir * radius * 1.8f),
            color,
            width: 3f
        );
    }

    private static Rect2 TileSource(int sheetRow, int col, int row)
    {
        var hash = (uint)(col * 73856093) ^ (uint)(row * 19349663);
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        var column = (int)((hash ^ (hash >> 16)) % TileSheetColumns);
        return new Rect2(column * TileCell, sheetRow * TileCell, TileCell, TileCell);
    }
}
