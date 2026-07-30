using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MessBreak.Logic;

namespace MessBreak;

/// <summary>
/// 盤面描画ノード。演出タイマーと Events→音/マーカー翻訳を持ち、
/// 実描画は BattlePainter / BattleSprites に委譲する。
/// </summary>
public sealed partial class BattleView : Node2D
{
    private const int HitMarkerFrames = 12;
    private const int EnemyFlashFrames = 4;
    private const int BurstMarkerFrames = 18;
    private const float FacingLerp = 0.35f;

    private readonly GameCamera _camera;
    private readonly BattleSprites _sprites = new();
    private readonly BattlePainter _painter = new();
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
        _painter.Paint(
            this,
            _camera,
            _sprites,
            _logic,
            DisplayFacing,
            _walkPhase,
            _enemyFlashFrames,
            _hitMarkers,
            _burstMarkers,
            _paused,
            GetGlobalMousePosition()
        );
    }
}
