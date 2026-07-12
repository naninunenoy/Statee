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
    MoveLeft,
    MoveRight,
}

/// <summary>
/// 飛行中の弾(D-058)。Attackで発射してから <see cref="GameLogic.ProjectileTravelTicks"/> Tick後に
/// ボスへ着弾する。位置は物理ではなく残りTick数のみで表す純粋な決定論的カウントダウン。
/// </summary>
public sealed record Projectile(int OwnerIndex, int TicksRemaining);

/// <summary>
/// 1〜4人協力ボス戦の中核状態機械(D-053/D-054/D-056/D-057/D-058/D-059)。固定Tickで進み、
/// 壁時計を持たない(D-023)。乱数を使わず攻撃対象・着弾を決定論的に処理するため、
/// 同一シード・同一入力列なら何度実行しても同じ結果に収束する(D-054のロックステップの前提)。
/// プレイヤーはレーン上を左右に移動でき、ボスの攻撃は「レーン予告 → 数Tick後に着弾」なので
/// 移動で回避できる(D-059)。
/// </summary>
public sealed class GameLogic(int seed)
{
    public const int MinPlayerCount = 1;
    public const int MaxPlayerCount = 4;
    public const int BossMaxHp = 100;
    public const int PlayerMaxHp = 100;
    public const int PlayerAttackDamage = 10;

    /// <summary>ボス攻撃の威力(D-059)。回避可能になったため被弾3発でダウンする重さにしている。</summary>
    public const int BossAttackDamage = 40;

    public const int BossAttackInterval = 5;

    /// <summary>プレイヤーが移動できるレーン数(D-059)。0〜LaneCount-1 の離散位置。</summary>
    public const int LaneCount = 7;

    /// <summary>ボス攻撃の予告から着弾までのTick数(D-059)。この間に移動すれば回避できる。</summary>
    public const int BossAttackWindupTicks = 2;

    /// <summary>HPが0以下になってから操作不能が続くTick数(D-057)。</summary>
    public const int IncapacitationDuration = 3;

    /// <summary>操作不能が明けたときに回復するHP(D-057)。全快ではなく半分に留める。</summary>
    public const int ReviveHp = PlayerMaxHp / 2;

    /// <summary>Attackで発射した弾がボスへ着弾するまでのTick数(D-058)。</summary>
    public const int ProjectileTravelTicks = 2;

    private int[] _playerHps = [];
    private int[] _incapacitatedTicks = [];
    private int[] _playerLanes = [];
    private readonly List<Projectile> _projectiles = [];

    /// <summary>生成に使ったシード。State で公開して再現性を検証できるようにする。</summary>
    public int Seed { get; } = seed;

    public GamePhase Phase { get; private set; } = GamePhase.Waiting;
    public int PlayerCount { get; private set; }
    public int TickCount { get; private set; }
    public int BossHp { get; private set; } = BossMaxHp;
    public IReadOnlyList<int> PlayerHps => _playerHps;

    /// <summary>各プレイヤーの残り操作不能Tick数(0なら操作可能)。State公開用。</summary>
    public IReadOnlyList<int> IncapacitatedTicks => _incapacitatedTicks;

    /// <summary>各プレイヤーの現在レーン(D-059)。描画・State公開用。</summary>
    public IReadOnlyList<int> PlayerLanes => _playerLanes;

    /// <summary>飛行中の弾(D-058)。描画・State公開用。</summary>
    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    /// <summary>予告中のボス攻撃の対象レーン(D-059)。予告がないときは -1。</summary>
    public int PendingBossAttackLane { get; private set; } = -1;

    /// <summary>予告中のボス攻撃が着弾するまでの残りTick数(D-059)。予告がないときは 0。</summary>
    public int PendingBossAttackTicks { get; private set; }

    /// <summary>
    /// 参加人数(1〜4)を確定してPlayingへ遷移する。Waiting以外では何もしない。
    /// マッチング待機は1人でも開始できる(D-057)。初期レーンは中央寄りに等間隔で並べる。
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
        var startLane = (LaneCount - playerCount) / 2;
        _playerLanes = Enumerable.Range(startLane, playerCount).ToArray();
        Phase = GamePhase.Playing;
    }

    /// <summary>
    /// 1Tick進める。Attackは即ダメージではなく弾を発射し、既存の弾を1Tick分進めて
    /// 着弾判定する(D-058)。移動はレーンを1つずらす(端で止まる)。ボスは周期ごとに
    /// レーンを予告し、予告の <see cref="BossAttackWindupTicks"/> Tick後にそのレーンへ
    /// 着弾する(D-059)。Playing 以外では何もしない。
    /// 操作不能中のプレイヤーの行動はIdle扱いになる(D-057。移動も弾の発射もできない)。
    /// </summary>
    public void Step(IReadOnlyList<PlayerAction> actions)
    {
        if (Phase != GamePhase.Playing)
        {
            return;
        }

        TickCount++;

        // 既存の弾を1Tick分進める(今Tick発射した弾は含めない。着弾まで丸ごと
        // ProjectileTravelTicks かかる)
        for (var i = _projectiles.Count - 1; i >= 0; i--)
        {
            var projectile = _projectiles[i] with
            {
                TicksRemaining = _projectiles[i].TicksRemaining - 1,
            };
            if (projectile.TicksRemaining <= 0)
            {
                BossHp -= PlayerAttackDamage;
                _projectiles.RemoveAt(i);
            }
            else
            {
                _projectiles[i] = projectile;
            }
        }

        for (var i = 0; i < PlayerCount; i++)
        {
            if (_incapacitatedTicks[i] > 0)
            {
                continue;
            }
            switch (actions[i])
            {
                case PlayerAction.Attack:
                    _projectiles.Add(new Projectile(i, ProjectileTravelTicks));
                    break;
                case PlayerAction.MoveLeft:
                    _playerLanes[i] = Math.Max(0, _playerLanes[i] - 1);
                    break;
                case PlayerAction.MoveRight:
                    _playerLanes[i] = Math.Min(LaneCount - 1, _playerLanes[i] + 1);
                    break;
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

        if (PendingBossAttackLane >= 0)
        {
            PendingBossAttackTicks--;
            if (PendingBossAttackTicks <= 0)
            {
                for (var i = 0; i < PlayerCount; i++)
                {
                    if (_incapacitatedTicks[i] > 0 || _playerLanes[i] != PendingBossAttackLane)
                    {
                        continue;
                    }
                    _playerHps[i] -= BossAttackDamage;
                    if (_playerHps[i] <= 0)
                    {
                        _incapacitatedTicks[i] = IncapacitationDuration;
                    }
                }
                PendingBossAttackLane = -1;
            }
        }
        else if (TickCount % BossAttackInterval == 0)
        {
            // 反撃回数からレーンを決める(乱数を使わない決定論的な巡回。D-059)
            var attackNumber = TickCount / BossAttackInterval;
            PendingBossAttackLane = attackNumber % LaneCount;
            PendingBossAttackTicks = BossAttackWindupTicks;
        }

        if (_playerHps.All(hp => hp <= 0))
        {
            Phase = GamePhase.Defeat;
        }
    }
}
