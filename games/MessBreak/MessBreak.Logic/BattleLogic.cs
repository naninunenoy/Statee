using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// 縦切り1「部屋1つ + 敵1種 + 攻撃と回避」の戦闘ロジック(docs/DESIGN.md)。
/// tick 駆動・決定論。時間経過はすべて Tick 呼び出し回数で表し、実時間に依存しない。
/// Godot 層は入力を <see cref="TickInput"/> に詰めて Tick を呼び、公開プロパティを描画するだけ。
/// </summary>
public sealed class BattleLogic(BattleConfig config, int seed)
{
    public BattleConfig Config { get; } = config;

    /// <summary>生成に使ったシード。State で公開して再現性を検証できるようにする。</summary>
    public int Seed { get; } = seed;

    /// <summary>経過 tick 数。</summary>
    public int TickCount { get; private set; }

    public BattlePhase Phase { get; private set; } = BattlePhase.Playing;

    // プレイヤー
    public Vector2 PlayerPos { get; private set; } = config.PlayerSpawn;
    public Vector2 PlayerFacing { get; private set; } = new(1f, 0f);
    public int PlayerHp { get; private set; } = config.PlayerMaxHp;
    public PlayerAction PlayerAction { get; private set; } = PlayerAction.Free;
    public int DodgeCooldown { get; private set; }

    // 敵
    public Vector2 EnemyPos { get; private set; } = config.EnemySpawn;
    public int EnemyHp { get; private set; } = config.EnemyMaxHp;
    public EnemyAction EnemyAction { get; private set; } = EnemyAction.Idle;

    /// <summary>1 tick 進める。Phase が Playing 以外なら何もしない。</summary>
    public void Tick(TickInput input) { }
}
