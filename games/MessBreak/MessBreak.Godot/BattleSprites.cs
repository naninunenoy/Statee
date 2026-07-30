using System;
using System.Collections.Generic;
using Godot;
using MessBreak.Logic;

namespace MessBreak;

/// <summary>
/// プレイヤ／敵スプライトのコマ選択・描画とシート／効果音のロード。
/// BattleView が依存する下位レイヤー。描画と State(ui/hud)が同じコマ選択を通る。
/// </summary>
public sealed class BattleSprites
{
    // 歩行シート(art/attacker.sprite.txt)。16x16 のコマを 3 列 × 3 行に並べたもの
    private const int SpriteCell = 16;
    public const int SpriteRowDown = 0;
    public const int SpriteRowUp = 1;
    public const int SpriteRowSide = 2;

    /// <summary>歩行の列の並び(待機 → 左足 → 待機 → 右足)。</summary>
    private static readonly int[] WalkColumns = [0, 1, 0, 2];

    /// <summary>歩行 1 コマぶんの論理 tick 数。</summary>
    private const int WalkTicksPerFrame = 8;

    // 敵シート(art/mob.sprite.txt / art/boss.sprite.txt)。列 = 待機 / 揺れ / 被弾フラッシュ、
    // 行 = 雑魚は 1 行のみ、強敵は down / up / side(プレイヤースプライトと同じ並び)
    private const int MobCell = 16;
    private const int BossCell = 32;
    private const int EnemySpriteColumnFlash = 2;

    /// <summary>敵の揺れ 1 コマぶんの論理 tick 数。</summary>
    private const int BobTicksPerFrame = 20;

    private Dictionary<CharacterId, Texture2D> _characterTextures = null!;
    private Texture2D _mobTexture = null!;
    private Texture2D _bossTexture = null!;
    private AudioStreamPlayer _shotPlayer = null!;
    private AudioStreamPlayer _skillPlayer = null!;

    /// <summary>床・壁のタイルシート(BattleView の盤面描画が使う)。</summary>
    public Texture2D TileTexture { get; private set; } = null!;

    /// <summary>
    /// スプライトと効果音を実行時ロードする。定義テキスト(art/*.sprite.txt, audio/*.sfx.txt)が
    /// 単一ソースで、生成物をゲームディレクトリから直接読む(Godot の import 経路を使わない)。
    /// </summary>
    public void Load(Node host)
    {
        _characterTextures = new Dictionary<CharacterId, Texture2D>
        {
            [CharacterId.Attacker] = LoadSheet("attacker"),
            [CharacterId.Debuffer] = LoadSheet("debuffer"),
        };
        TileTexture = LoadSheet("tiles");
        _mobTexture = LoadSheet("mob");
        _bossTexture = LoadSheet("boss");
        _shotPlayer = new AudioStreamPlayer
        {
            Stream = AudioStreamWav.LoadFromFile(
                ProjectSettings.GlobalizePath("res://../audio/shot.wav")
            ),
        };
        host.AddChild(_shotPlayer);
        _skillPlayer = new AudioStreamPlayer
        {
            Stream = AudioStreamWav.LoadFromFile(
                ProjectSettings.GlobalizePath("res://../audio/skill.wav")
            ),
        };
        host.AddChild(_skillPlayer);
    }

    public void PlayShot() => _shotPlayer.Play();

    public void PlaySkill() => _skillPlayer.Play();

    /// <summary>
    /// 歩行シートのどのコマを描くかを決める。向きで行(正面/背面/横)を、歩行位相で
    /// 列(待機/左足/右足)を選ぶ。左向きの絵は持たず、横向きの左右反転で賄う。
    /// </summary>
    public static (int Row, int Column, bool Mirror) PlayerSpriteCell(
        System.Numerics.Vector2 facing,
        int walkPhase
    )
    {
        var (row, mirror) = DirectionCell(facing);
        var column =
            walkPhase < 0 ? 0 : WalkColumns[walkPhase / WalkTicksPerFrame % WalkColumns.Length];
        return (row, column, mirror);
    }

    /// <summary>歩行シートの行を State 用の名前に写す。</summary>
    public static string DirectionName(int row) =>
        row switch
        {
            SpriteRowDown => "down",
            SpriteRowUp => "up",
            _ => "side",
        };

    /// <summary>
    /// 向きをシートの行(正面/背面/横)と左右反転へスナップする。絵柄が正面向きの疑似 2.5D で
    /// スプライトを回転できないため、斜めは最寄りの 4 方向へ寄せ、左向きは横向きの反転で賄う。
    /// </summary>
    public static (int Row, bool Mirror) DirectionCell(System.Numerics.Vector2 facing) =>
        MathF.Abs(facing.X) > MathF.Abs(facing.Y)
            ? (SpriteRowSide, facing.X < 0f)
            : (facing.Y > 0f ? SpriteRowDown : SpriteRowUp, false);

    /// <summary>プレイヤーを歩行シートから 1 コマ選んで描く。</summary>
    public void DrawPlayer(
        CanvasItem canvas,
        GameCamera camera,
        BattleLogic logic,
        System.Numerics.Vector2 facing,
        Color tint,
        int walkPhase
    )
    {
        var (row, column, mirror) = PlayerSpriteCell(facing, walkPhase);
        var src = new Rect2(column * SpriteCell, row * SpriteCell, SpriteCell, SpriteCell);
        if (mirror)
        {
            // 負の幅で左右反転する(左向き用のコマは持たない)
            src = new Rect2(src.Position.X + SpriteCell, src.Position.Y, -SpriteCell, SpriteCell);
        }
        var size = new Vector2(SpriteCell, SpriteCell) * camera.ScaleFactor;
        canvas.DrawTextureRectRegion(
            _characterTextures[logic.ActiveCharacter],
            new Rect2(camera.ToScreen(logic.PlayerPos) - size / 2f, size),
            src,
            tint
        );
    }

    /// <summary>
    /// 敵を 1 体描く。雑魚は 1 行だけのシート、強敵はプレイヤーを向いた行を選ぶ。
    /// 被弾中は白シルエットのコマに差し替え、それ以外は残 HP ぶん暗くする。
    /// </summary>
    public void DrawEnemy(
        CanvasItem canvas,
        GameCamera camera,
        BattleLogic logic,
        Enemy enemy,
        bool flashing
    )
    {
        var mob = enemy.Kind == EnemyKind.Mob;
        var cell = mob ? MobCell : BossCell;
        // 強敵は常にプレイヤーを追うので、向きは追跡先で決まる
        var (row, mirror) = mob ? (0, false) : DirectionCell(logic.PlayerPos - enemy.Pos);
        // 揺れは敵ごとに位相をずらす(揃うと群れが機械的に見える)
        var column = flashing
            ? EnemySpriteColumnFlash
            : (logic.TickCount / BobTicksPerFrame + enemy.Id) % 2;

        var src = new Rect2(column * cell, row * cell, cell, cell);
        if (mirror)
        {
            src = new Rect2(src.Position.X + cell, src.Position.Y, -cell, cell);
        }
        var maxHp = mob ? logic.Config.MobMaxHp : logic.Config.BossMaxHp;
        var tint = flashing
            ? Colors.White
            : Colors.White.Lerp(new Color(0.5f, 0.42f, 0.5f), 1f - enemy.Hp / (float)maxHp);
        var size = new Vector2(cell, cell) * camera.ScaleFactor;
        canvas.DrawTextureRectRegion(
            mob ? _mobTexture : _bossTexture,
            new Rect2(camera.ToScreen(enemy.Pos) - size / 2f, size),
            src,
            tint
        );

        // デバフ中は敵の周りに紫のリングを出す(コンボの好機を可視化)
        if (enemy.DebuffTicks > 0)
        {
            var radius = mob ? logic.Config.MobRadius : logic.Config.BossRadius;
            canvas.DrawArc(
                camera.ToScreen(enemy.Pos),
                (radius + 4f) * camera.ScaleFactor,
                0f,
                Mathf.Tau,
                32,
                new Color(0.8f, 0.4f, 1f, 0.9f),
                width: 3f
            );
        }
    }

    /// <summary>歩行シート(art/&lt;name&gt;.png)を読む。Godot の import 経路は使わない。</summary>
    private static Texture2D LoadSheet(string name) =>
        ImageTexture.CreateFromImage(
            Image.LoadFromFile(ProjectSettings.GlobalizePath($"res://../art/{name}.png"))
        );
}
