namespace RaidBoss.Logic;

/// <summary>対局フロー。</summary>
public enum GamePhase
{
    Waiting,
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
/// 1〜4人協力ボス戦の中核状態機械(D-053/D-054/D-056/D-057)。固定Tickで進み、壁時計を持たない
/// (D-023)。乱数を使わず攻撃対象を決定論的に選ぶため、同一シード・同一入力列なら
/// 何度実行しても同じ結果に収束する(D-054のロックステップの前提)。
/// </summary>
public sealed class GameLogic(int seed)
{
    public const int MinPlayerCount = 1;
    public const int MaxPlayerCount = 4;
    public const int BossMaxHp = 100;
    public const int PlayerMaxHp = 100;
    public const int PlayerAttackDamage = 10;
    public const int BossAttackDamage = 15;
    public const int BossAttackInterval = 5;

    /// <summary>HPが0以下になってから操作不能が続くTick数(D-057)。</summary>
    public const int IncapacitationDuration = 3;

    /// <summary>操作不能が明けたときに回復するHP(D-057)。全快ではなく半分に留める。</summary>
    public const int ReviveHp = PlayerMaxHp / 2;

    private int[] _playerHps = [];
    private int[] _incapacitatedTicks = [];

    /// <summary>生成に使ったシード。State で公開して再現性を検証できるようにする。</summary>
    public int Seed { get; } = seed;

    public GamePhase Phase { get; private set; } = GamePhase.Waiting;
    public int PlayerCount { get; private set; }
    public int TickCount { get; private set; }
    public int BossHp { get; private set; } = BossMaxHp;
    public IReadOnlyList<int> PlayerHps => _playerHps;

    /// <summary>各プレイヤーの残り操作不能Tick数(0なら操作可能)。State公開用。</summary>
    public IReadOnlyList<int> IncapacitatedTicks => _incapacitatedTicks;

    /// <summary>
    /// 参加人数(1〜4)を確定してPlayingへ遷移する。Waiting以外では何もしない。
    /// マッチング待機は1人でも開始できる(D-057)。
    /// </summary>
    public void Start(int playerCount)
    {
        if (Phase != GamePhase.Waiting)
        {
            return;
        }

        PlayerCount = playerCount;
        _playerHps = Enumerable.Repeat(PlayerMaxHp, playerCount).ToArray();
        _incapacitatedTicks = new int[playerCount];
        Phase = GamePhase.Playing;
    }

    /// <summary>
    /// 1Tick進める。全プレイヤーの行動を同時に適用し、続いてボスの反撃周期を判定する。
    /// Playing 以外では何もしない。操作不能中のプレイヤーの行動はIdle扱いになる(D-057)。
    /// </summary>
    public void Step(IReadOnlyList<PlayerAction> actions)
    {
        if (Phase != GamePhase.Playing)
        {
            return;
        }

        TickCount++;

        for (var i = 0; i < PlayerCount; i++)
        {
            if (_incapacitatedTicks[i] > 0)
            {
                continue;
            }
            if (actions[i] == PlayerAction.Attack)
            {
                BossHp -= PlayerAttackDamage;
            }
        }
        if (BossHp <= 0)
        {
            Phase = GamePhase.Victory;
            return;
        }

        // 今Tickで倒れたプレイヤーの操作不能タイマーは減らさない(倒れた直後の1Tickは
        // まるまるIncapacitationDuration残す)ため、ボスの反撃より先に既存分を減らす
        for (var i = 0; i < PlayerCount; i++)
        {
            if (_incapacitatedTicks[i] <= 0)
            {
                continue;
            }
            _incapacitatedTicks[i]--;
            if (_incapacitatedTicks[i] == 0)
            {
                _playerHps[i] = ReviveHp;
            }
        }

        if (TickCount % BossAttackInterval == 0)
        {
            // 反撃回数をプレイヤー数で割った余りで狙う相手を巡回する(乱数を使わない決定論的な選択)
            var attackNumber = TickCount / BossAttackInterval;
            var targetIndex = (attackNumber - 1) % PlayerCount;
            _playerHps[targetIndex] -= BossAttackDamage;
            if (_playerHps[targetIndex] <= 0 && _incapacitatedTicks[targetIndex] == 0)
            {
                _incapacitatedTicks[targetIndex] = IncapacitationDuration;
            }
        }

        if (_playerHps.All(hp => hp <= 0))
        {
            Phase = GamePhase.Defeat;
        }
    }
}
