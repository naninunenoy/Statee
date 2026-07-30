using System;
using System.Collections.Generic;
using Godot;
using MessBreak.Logic;

namespace MessBreak;

/// <summary>
/// 1 フレームぶんの盤面描画。BattleView が持つ演出状態を受け取り CanvasItem へ写す。
/// BattleView が依存する描画下位レイヤー(ノード状態は持たない)。
/// </summary>
public sealed class BattlePainter
{
    private const int HitMarkerFrames = 12;
    private const int BurstMarkerFrames = 18;

    // 床・壁のタイルシート(art/tiles.sprite.txt)。1 コマ = ステージ 1 マス
    private const int TileCell = 40;
    private const int TileSheetRowFloor = 0;
    private const int TileSheetRowWall = 1;
    private const int TileSheetColumns = 4;
    private const float WallShadowDepth = 6f;

    private readonly List<(float Y, Enemy? Enemy)> _actors = [];

    public void Paint(
        CanvasItem canvas,
        GameCamera camera,
        BattleSprites sprites,
        BattleLogic logic,
        System.Numerics.Vector2 displayFacing,
        int walkPhase,
        IReadOnlyDictionary<int, int> enemyFlashFrames,
        IReadOnlyList<(System.Numerics.Vector2 Pos, int Frames)> hitMarkers,
        IReadOnlyList<(System.Numerics.Vector2 Pos, int Frames, float Radius)> burstMarkers,
        bool paused,
        Vector2 mouseScreen
    )
    {
        var config = logic.Config;
        var scale = camera.ScaleFactor;

        // 床と壁(タイルシートから 1 マスずつ)
        var stage = logic.Stage;
        var tileSize = stage.TileSize * scale;
        for (var row = 0; row < stage.Rows.Count; row++)
        {
            for (var col = 0; col < stage.Rows[row].Length; col++)
            {
                var solid = stage.IsSolidCell(col, row);
                var topLeft = camera.ToScreen(
                    new System.Numerics.Vector2(col * stage.TileSize, row * stage.TileSize)
                );
                canvas.DrawTextureRectRegion(
                    sprites.TileTexture,
                    new Rect2(topLeft, new Vector2(tileSize, tileSize)),
                    TileSource(solid ? TileSheetRowWall : TileSheetRowFloor, col, row)
                );
                // 壁の下隣が床なら、そこへ影を落とす(壁に厚みを感じさせる)
                if (!solid && row > 0 && stage.IsSolidCell(col, row - 1))
                {
                    canvas.DrawRect(
                        new Rect2(topLeft, new Vector2(tileSize, WallShadowDepth * scale)),
                        new Color(0f, 0f, 0f, 0.45f)
                    );
                }
            }
        }

        // 設置スロット(制圧後に見える。設置済みは塗り、未設置は枠だけ)
        if (logic.ZoneCaptured)
        {
            var slotScreen = camera.ToScreen(stage.TurretSlot);
            var half = 8f * scale;
            var slotRect = new Rect2(
                slotScreen - new Vector2(half, half),
                new Vector2(half * 2f, half * 2f)
            );
            if (logic.TurretPlaced)
            {
                canvas.DrawRect(slotRect, new Color(0.45f, 0.8f, 0.5f));
                canvas.DrawCircle(slotScreen, 3f * scale, new Color(0.2f, 0.35f, 0.25f));
            }
            else
            {
                canvas.DrawRect(
                    slotRect,
                    new Color(0.45f, 0.8f, 0.5f, 0.8f),
                    filled: false,
                    width: 2f
                );
            }
        }

        // 強敵の出現ポイント(アトラクト前だけ菱形マーカーを出す)
        if (!logic.BossAppeared)
        {
            var spawnScreen = camera.ToScreen(stage.BossSpawn);
            var r = 6f * scale;
            canvas.DrawPolygon(
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
            logic.PlayerAction == PlayerAction.Dodge
                ? Colors.White with
                {
                    A = 0.5f,
                }
                : Colors.White;
        // 向きは常にカーソルが決めるので、照準線も常に出す。床の上に敷いてスプライトの下に置く
        canvas.DrawLine(
            camera.ToScreen(logic.PlayerPos),
            camera.ToScreen(logic.PlayerPos + displayFacing * 60f),
            new Color(1f, 1f, 1f, 0.15f),
            width: 1f
        );

        // 立っているものは Y 順に描く(足元が下にあるものほど手前。疑似 2.5D の前後関係)
        _actors.Clear();
        foreach (var enemy in logic.Enemies)
        {
            _actors.Add((enemy.Pos.Y, enemy));
        }
        _actors.Add((logic.PlayerPos.Y, null));
        _actors.Sort(static (a, b) => a.Y.CompareTo(b.Y));
        foreach (var (_, enemy) in _actors)
        {
            if (enemy is null)
            {
                sprites.DrawPlayer(canvas, camera, logic, displayFacing, playerTint, walkPhase);
                DrawNose(
                    canvas,
                    camera,
                    logic.PlayerPos,
                    displayFacing,
                    config.PlayerRadius,
                    new Color(1f, 0.9f, 0.75f)
                );
            }
            else
            {
                sprites.DrawEnemy(
                    canvas,
                    camera,
                    logic,
                    enemy,
                    enemyFlashFrames.ContainsKey(enemy.Id)
                );
            }
        }

        // 弾
        foreach (var bullet in logic.Bullets)
        {
            canvas.DrawCircle(
                camera.ToScreen(bullet.Pos),
                config.BulletRadius * scale,
                new Color(1f, 0.85f, 0.4f)
            );
        }

        // スキル爆発(爆心に半径いっぱいまで広がるリング)
        foreach (var (pos, frames, radius) in burstMarkers)
        {
            var t = 1f - frames / (float)BurstMarkerFrames;
            canvas.DrawArc(
                camera.ToScreen(pos),
                radius * t * scale,
                0f,
                Mathf.Tau,
                48,
                new Color(1f, 0.6f, 0.25f, 1f - t),
                width: 4f
            );
        }

        // ヒットマーカー(命中位置に広がって消えるリング)
        foreach (var (pos, frames) in hitMarkers)
        {
            var t = 1f - frames / (float)HitMarkerFrames;
            canvas.DrawArc(
                camera.ToScreen(pos),
                (4f + 8f * t) * scale,
                0f,
                Mathf.Tau,
                24,
                new Color(1f, 1f, 1f, 1f - t),
                width: 2f
            );
        }

        // 画面外の敵の方向インジケーター(画面端の三角矢印)
        foreach (var enemy in logic.Enemies)
        {
            DrawOffscreenIndicator(canvas, camera, camera.ToScreen(enemy.Pos));
        }

        // レティクル(OS カーソルの代わり)。ポーズ中はメニュー操作用に OS カーソルを出すので消す
        if (!paused)
        {
            DrawReticle(canvas, mouseScreen, Input.IsMouseButtonPressed(MouseButton.Right));
        }
    }

    private static void DrawOffscreenIndicator(
        CanvasItem canvas,
        GameCamera camera,
        Vector2 targetScreen
    )
    {
        const float Margin = 28f;
        var gameRect = camera.GameRect;
        var screenCenter = camera.ScreenCenter;
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
        canvas.DrawPolygon(
            [edge + dir * 12f, edge - dir * 4f + perp * 8f, edge - dir * 4f - perp * 8f],
            [new Color(1f, 0.6f, 0.9f, 0.9f)]
        );
    }

    private static void DrawReticle(CanvasItem canvas, Vector2 pos, bool ads)
    {
        if (ads)
        {
            var color = new Color(1f, 1f, 1f, 0.9f);
            const float Gap = 4f;
            const float Arm = 8f;
            canvas.DrawLine(
                pos + new Vector2(Gap, 0),
                pos + new Vector2(Gap + Arm, 0),
                color,
                1.5f
            );
            canvas.DrawLine(
                pos - new Vector2(Gap, 0),
                pos - new Vector2(Gap + Arm, 0),
                color,
                1.5f
            );
            canvas.DrawLine(
                pos + new Vector2(0, Gap),
                pos + new Vector2(0, Gap + Arm),
                color,
                1.5f
            );
            canvas.DrawLine(
                pos - new Vector2(0, Gap),
                pos - new Vector2(0, Gap + Arm),
                color,
                1.5f
            );
            canvas.DrawCircle(pos, 1.5f, color);
        }
        else
        {
            canvas.DrawArc(pos, 7f, 0f, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.5f), width: 1.5f);
            canvas.DrawCircle(pos, 1.5f, new Color(1f, 1f, 1f, 0.5f));
        }
    }

    private static void DrawNose(
        CanvasItem canvas,
        GameCamera camera,
        System.Numerics.Vector2 center,
        System.Numerics.Vector2 dir,
        float radius,
        Color color
    )
    {
        canvas.DrawLine(
            camera.ToScreen(center + dir * radius * 0.5f),
            camera.ToScreen(center + dir * radius * 1.8f),
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
