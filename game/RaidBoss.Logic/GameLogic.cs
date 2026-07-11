namespace RaidBoss.Logic;

/// <summary>対局フロー。</summary>
public enum GamePhase
{
    Playing,
    Victory,
    Defeat,
}

/// <summary>1Tickでプレイヤーが選べる行動。</summary>
public enum PlayerAction
{
    Idle,
    Attack,
}

/// <summary>
/// 2人協力ボス戦の中核状態機械(D-053/D-054)。固定Tickで進み、壁時計を持たない
/// (D-023)。乱数を使わず攻撃対象を決定論的に選ぶため、同一シード・同一入力列なら
/// 何度実行しても同じ結果に収束する(D-054のロックステップの前提)。
/// </summary>
public sealed class GameLogic(int seed)
{
    public const int BossMaxHp = 100;
    public const int PlayerMaxHp = 100;
    public const int PlayerAttackDamage = 10;
    public const int BossAttackDamage = 15;
    public const int BossAttackInterval = 5;

    /// <summary>生成に使ったシード。State で公開して再現性を検証できるようにする。</summary>
    public int Seed { get; } = seed;

    public GamePhase Phase { get; private set; } = GamePhase.Playing;
    public int TickCount { get; private set; }
    public int BossHp { get; private set; } = BossMaxHp;
    public int Player1Hp { get; private set; } = PlayerMaxHp;
    public int Player2Hp { get; private set; } = PlayerMaxHp;

    /// <summary>
    /// 1Tick進める。両プレイヤーの行動を同時に適用し、続いてボスの反撃周期を判定する。
    /// Playing 以外では何もしない。
    /// </summary>
    public void Step(PlayerAction player1Action, PlayerAction player2Action)
    {
        if (Phase != GamePhase.Playing)
        {
            return;
        }

        TickCount++;

        if (player1Action == PlayerAction.Attack)
        {
            BossHp -= PlayerAttackDamage;
        }
        if (player2Action == PlayerAction.Attack)
        {
            BossHp -= PlayerAttackDamage;
        }
        if (BossHp <= 0)
        {
            Phase = GamePhase.Victory;
            return;
        }

        if (TickCount % BossAttackInterval == 0)
        {
            // 反撃回数の偶奇でプレイヤーを交互に狙う(乱数を使わない決定論的な選択)
            var attackNumber = TickCount / BossAttackInterval;
            if (attackNumber % 2 == 1)
            {
                Player1Hp -= BossAttackDamage;
            }
            else
            {
                Player2Hp -= BossAttackDamage;
            }
        }

        if (Player1Hp <= 0 && Player2Hp <= 0)
        {
            Phase = GamePhase.Defeat;
        }
    }
}
