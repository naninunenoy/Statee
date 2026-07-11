using System.Numerics;
using Arch.Core;
using VitalRouter;

namespace ShootingGame.Logic;

/// <summary>
/// 横スクロール STG の規則エンジン(D-048)。固定タイムステップ(60Hz)の
/// Tick(InputState) でだけ状態が進む完全決定論。運動・衝突は自前の数式で、
/// Godot 物理を使わない。弾・敵は Arch の Entity、イベントは VitalRouter で流す。
/// 乱数はコンストラクタのシード由来の1系統のみ(ウェーブの出現タイミング・位置・種類)。
/// </summary>
public sealed class ShootingLogic : IDisposable, ICommandSubscriber
{
    private struct PositionComponent
    {
        public Vector2 Value;
    }

    private struct VelocityComponent
    {
        public Vector2 Value;
    }

    private struct PlayerBulletComponent
    {
        public int Id;
    }

    private struct EnemyBulletComponent
    {
        public int Id;
    }

    private struct EnemyComponent
    {
        public int Id;
        public EnemyKind Kind;
        public int Hp;
    }

    /// <summary>サイン波敵 🛸 の蛇行。X は Velocity、Y はこの成分が毎 Tick 上書きする。</summary>
    private struct SineMotionComponent
    {
        public float BaseY;
        public int Age;
    }

    /// <summary>シューター敵 🦑 の発射管理。</summary>
    private struct ShooterComponent
    {
        public int Cooldown;
    }

    /// <summary>アイテム ⭐。</summary>
    private struct ItemComponent
    {
        public int Id;
    }

    /// <summary>ボス 🐙 の入場・蛇行・フェーズ・発射管理。</summary>
    private struct BossComponent
    {
        public bool Anchored;
        public float BaseY;
        public int Age;
        public int Cooldown;
        public int Phase;
    }

    private readonly record struct PendingSpawn(int Tick, EnemyKind Kind, float Y);

    private static readonly QueryDescription MovingQuery = new QueryDescription().WithAll<
        PositionComponent,
        VelocityComponent
    >();
    private static readonly QueryDescription BulletQuery = new QueryDescription().WithAll<
        PlayerBulletComponent,
        PositionComponent
    >();
    private static readonly QueryDescription EnemyBulletQuery = new QueryDescription().WithAll<
        EnemyBulletComponent,
        PositionComponent
    >();
    private static readonly QueryDescription EnemyQuery = new QueryDescription().WithAll<
        EnemyComponent,
        PositionComponent
    >();
    private static readonly QueryDescription SineQuery = new QueryDescription().WithAll<
        SineMotionComponent,
        PositionComponent
    >();
    private static readonly QueryDescription ShooterQuery = new QueryDescription().WithAll<
        ShooterComponent,
        PositionComponent
    >();
    private static readonly QueryDescription ItemQuery = new QueryDescription().WithAll<
        ItemComponent,
        PositionComponent
    >();
    private static readonly QueryDescription BossQuery = new QueryDescription().WithAll<
        BossComponent,
        EnemyComponent,
        PositionComponent,
        VelocityComponent
    >();

    private readonly World _world = World.Create();
    private readonly HashSet<Entity> _toDestroy = [];
    private readonly Random _random;
    private readonly Queue<PendingSpawn> _pendingSpawns = new();
    private readonly List<InputState> _inputLog = [];
    private Vector2 _playerPosition;
    private int _fireCooldown;
    private int _invincibleTicksLeft;
    private int _nextBulletId = 1;
    private int _nextEnemyBulletId = 1;
    private int _nextEnemyId = 1;
    private int _nextItemId = 1;
    private int _waveIndex = -1;

    public ShootingLogic(int seed, ShootingConfig? config = null)
    {
        Seed = seed;
        Config = config ?? new ShootingConfig();
        _random = new Random(seed);
        EventLog = new EventLog(Config.EventLogCapacity);
        Router.AddFilter(EventLog);
        Router.Subscribe(this);
        Lives = Config.InitialLives;
        _playerPosition = new Vector2(Config.PlayerStartX, Config.FieldHeight / 2f);
    }

    /// <summary>生成に使ったシード。再現性検証のため State で公開する。</summary>
    public int Seed { get; }

    /// <summary>適用中のルール定数。</summary>
    public ShootingConfig Config { get; }

    /// <summary>進んだ Tick 数(60Hz)。</summary>
    public int TickCount { get; private set; }

    /// <summary>自機の位置。</summary>
    public Vector2 PlayerPosition => _playerPosition;

    /// <summary>残機。</summary>
    public int Lives { get; private set; }

    /// <summary>スコア。</summary>
    public int Score { get; private set; }

    /// <summary>被弾後の無敵中か。</summary>
    public bool IsInvincible => _invincibleTicksLeft > 0;

    /// <summary>残機が尽きたか。true 以降は Tick が状態を変えない(盤面凍結)。</summary>
    public bool IsGameOver { get; private set; }

    /// <summary>現在のウェーブ(1 始まり)。ウェーブ進行なし(Waves が空)のときは 0。</summary>
    public int Wave => _waveIndex + 1;

    /// <summary>全ウェーブをクリアしたか。ボスが設定されていればこの後にボス戦が始まる。</summary>
    public bool AllWavesCleared { get; private set; }

    /// <summary>ボスを撃破してクリアしたか。true 以降は Tick が状態を変えない(盤面凍結)。</summary>
    public bool IsCleared { get; private set; }

    /// <summary>ショット強化の段階(1〜MaxPowerLevel)。段階ぶんの弾を同時に撃つ。被弾で1段階下がる。</summary>
    public int PowerLevel { get; private set; } = 1;

    /// <summary>ゲーム内イベントの発行先。購読者(スコア係・演出係等)はここへ Subscribe する。</summary>
    public Router Router { get; } = new();

    /// <summary>全イベントの記録。State 公開・wait 条件の源。</summary>
    public EventLog EventLog { get; }

    /// <summary>受け付けた全入力の記録(Tick ごと)。フレーム精度リプレイ(D-048)の源。</summary>
    public IReadOnlyList<InputState> InputLog => _inputLog;

    /// <summary>入力ログのランレングス圧縮。State 公開・再生コマンドの単位。</summary>
    public IReadOnlyList<InputRun> InputRuns
    {
        get
        {
            var runs = new List<InputRun>();
            foreach (var input in _inputLog)
            {
                if (runs.Count > 0 && runs[^1].Input == input)
                {
                    runs[^1] = runs[^1] with { Ticks = runs[^1].Ticks + 1 };
                }
                else
                {
                    runs.Add(new InputRun(1, input));
                }
            }
            return runs;
        }
    }

    /// <summary>場に出ている自弾(Id 昇順)。</summary>
    public IReadOnlyList<BulletSnapshot> PlayerBullets
    {
        get
        {
            var bullets = new List<BulletSnapshot>();
            _world.Query(
                in BulletQuery,
                (ref PlayerBulletComponent bullet, ref PositionComponent position) =>
                    bullets.Add(new BulletSnapshot(bullet.Id, position.Value))
            );
            bullets.Sort((a, b) => a.Id.CompareTo(b.Id));
            return bullets;
        }
    }

    /// <summary>場に出ている敵弾(Id 昇順)。</summary>
    public IReadOnlyList<BulletSnapshot> EnemyBullets
    {
        get
        {
            var bullets = new List<BulletSnapshot>();
            _world.Query(
                in EnemyBulletQuery,
                (ref EnemyBulletComponent bullet, ref PositionComponent position) =>
                    bullets.Add(new BulletSnapshot(bullet.Id, position.Value))
            );
            bullets.Sort((a, b) => a.Id.CompareTo(b.Id));
            return bullets;
        }
    }

    /// <summary>場に出ているアイテム ⭐(Id 昇順)。</summary>
    public IReadOnlyList<ItemSnapshot> Items
    {
        get
        {
            var items = new List<ItemSnapshot>();
            _world.Query(
                in ItemQuery,
                (ref ItemComponent item, ref PositionComponent position) =>
                    items.Add(new ItemSnapshot(item.Id, position.Value))
            );
            items.Sort((a, b) => a.Id.CompareTo(b.Id));
            return items;
        }
    }

    /// <summary>場に出ている敵(Id 昇順)。</summary>
    public IReadOnlyList<EnemySnapshot> Enemies
    {
        get
        {
            var enemies = new List<EnemySnapshot>();
            _world.Query(
                in EnemyQuery,
                (ref EnemyComponent enemy, ref PositionComponent position) =>
                    enemies.Add(new EnemySnapshot(enemy.Id, enemy.Kind, position.Value, enemy.Hp))
            );
            enemies.Sort((a, b) => a.Id.CompareTo(b.Id));
            return enemies;
        }
    }

    /// <summary>
    /// 1 Tick(1/60 秒)進める。自機移動 → 運動 → ウェーブ出現 → 発射 → 衝突 →
    /// 画面外掃除 → ウェーブクリア判定の順で解決する。
    /// </summary>
    public void Tick(in InputState input)
    {
        if (IsGameOver || IsCleared)
        {
            return;
        }

        TickCount++;
        _inputLog.Add(input);
        EventLog.CurrentTick = TickCount;
        if (_invincibleTicksLeft > 0)
        {
            _invincibleTicksLeft--;
        }

        MovePlayer(input);
        MoveEntities();
        SpawnDueWaveEnemies();
        Fire(input);
        FireShooters();
        UpdateBoss();
        ResolveBulletHits();
        UpdateBossPhase();
        ResolveItemPickup();
        ResolvePlayerHit();
        CullOffscreen();
        CheckWaveClear();
    }

    /// <summary>敵を出現させる(ウェーブ生成・テスト用)。EnemySpawned を発行する。</summary>
    public int SpawnEnemy(EnemyKind kind, Vector2 position)
    {
        var id = _nextEnemyId++;
        var enemy = new EnemyComponent
        {
            Id = id,
            Kind = kind,
            Hp = 1,
        };
        var positionComponent = new PositionComponent { Value = position };
        switch (kind)
        {
            case EnemyKind.Straight:
                _world.Create(
                    enemy,
                    positionComponent,
                    new VelocityComponent { Value = new Vector2(-Config.StraightEnemySpeed, 0f) }
                );
                break;
            case EnemyKind.Sine:
                _world.Create(
                    enemy,
                    positionComponent,
                    new VelocityComponent { Value = new Vector2(-Config.SineEnemySpeed, 0f) },
                    new SineMotionComponent { BaseY = position.Y, Age = 0 }
                );
                break;
            case EnemyKind.Shooter:
                _world.Create(
                    enemy,
                    positionComponent,
                    new VelocityComponent { Value = new Vector2(-Config.ShooterEnemySpeed, 0f) },
                    new ShooterComponent { Cooldown = Config.ShooterFireIntervalTicks }
                );
                break;
            case EnemyKind.Boss:
                var boss = BossOrDefault;
                enemy.Hp = boss.Hp;
                _world.Create(
                    enemy,
                    positionComponent,
                    new VelocityComponent { Value = new Vector2(-boss.EntrySpeed, 0f) },
                    new BossComponent { Cooldown = boss.FireIntervalTicks, Phase = 1 }
                );
                break;
            default:
                _world.Create(enemy, positionComponent, new VelocityComponent());
                break;
        }
        Publish(new EnemySpawned(id, kind));
        return id;
    }

    /// <summary>入力列を同一シードの新しいゲームに再生する(フレーム精度リプレイ検証)。</summary>
    public static ShootingLogic Replay(
        int seed,
        IEnumerable<InputState> inputs,
        ShootingConfig? config = null
    )
    {
        var game = new ShootingLogic(seed, config);
        foreach (var input in inputs)
        {
            game.Tick(input);
        }
        return game;
    }

    /// <summary>アイテム ⭐ を出す(ドロップ・テスト用)。</summary>
    public int SpawnItem(Vector2 position)
    {
        var id = _nextItemId++;
        _world.Create(
            new ItemComponent { Id = id },
            new PositionComponent { Value = position },
            new VelocityComponent { Value = new Vector2(-Config.ItemDriftSpeed, 0f) }
        );
        return id;
    }

    /// <inheritdoc/>
    public void Receive<T>(T command, PublishContext context)
        where T : ICommand
    {
        // スコア係。撃破イベントの購読でスコアを加算する(多対多配線の一端)
        if (command is EnemyDestroyed destroyed)
        {
            Score += destroyed.Kind == EnemyKind.Boss ? (BossOrDefault).Score : Config.EnemyScore;
        }
    }

    public void Dispose()
    {
        Router.Dispose();
        World.Destroy(_world);
    }

    private void MovePlayer(in InputState input)
    {
        var direction = new Vector2(
            (input.Right ? 1f : 0f) - (input.Left ? 1f : 0f),
            (input.Down ? 1f : 0f) - (input.Up ? 1f : 0f)
        );
        var moved = _playerPosition + direction * Config.PlayerSpeed;
        _playerPosition = new Vector2(
            Math.Clamp(moved.X, Config.PlayerRadius, Config.FieldWidth - Config.PlayerRadius),
            Math.Clamp(moved.Y, Config.PlayerRadius, Config.FieldHeight - Config.PlayerRadius)
        );
    }

    private void MoveEntities()
    {
        _world.Query(
            in MovingQuery,
            (ref PositionComponent position, ref VelocityComponent velocity) =>
                position.Value += velocity.Value
        );
        // サイン波の Y は等速移動の後に基準線からの数式で上書きする(決定論)
        var amplitude = Config.SineAmplitude;
        var period = Config.SinePeriodTicks;
        _world.Query(
            in SineQuery,
            (ref SineMotionComponent sine, ref PositionComponent position) =>
            {
                sine.Age++;
                position.Value.Y =
                    sine.BaseY + amplitude * MathF.Sin(2f * MathF.PI * sine.Age / period);
            }
        );
    }

    /// <summary>ウェーブのスケジュール(シード由来)に達した敵を湧かせる。</summary>
    private void SpawnDueWaveEnemies()
    {
        if (Config.Waves.Count == 0 || AllWavesCleared)
        {
            return;
        }
        if (_waveIndex < 0)
        {
            StartWave(0);
        }
        while (_pendingSpawns.Count > 0 && _pendingSpawns.Peek().Tick <= TickCount)
        {
            var spawn = _pendingSpawns.Dequeue();
            SpawnEnemy(spawn.Kind, new Vector2(Config.FieldWidth + Config.EnemyRadius, spawn.Y));
        }
    }

    /// <summary>ウェーブを開始し、出現スケジュールを乱数から確定させる。WaveStarted を発行する。</summary>
    private void StartWave(int index)
    {
        _waveIndex = index;
        var wave = Config.Waves[index];
        var tick = TickCount; // 先頭の敵は即時に湧く
        for (var i = 0; i < wave.EnemyCount; i++)
        {
            var kind = wave.Kinds[_random.Next(wave.Kinds.Length)];
            var y =
                Config.SpawnMarginY
                + (float)_random.NextDouble() * (Config.FieldHeight - 2f * Config.SpawnMarginY);
            _pendingSpawns.Enqueue(new PendingSpawn(tick, kind, y));
            tick += Config.SpawnIntervalTicks + _random.Next(Config.SpawnJitterTicks);
        }
        Publish(new WaveStarted(index + 1));
    }

    /// <summary>出現予定も場の敵も尽きたらウェーブクリア。最終ウェーブなら全クリア。</summary>
    private void CheckWaveClear()
    {
        if (Config.Waves.Count == 0 || AllWavesCleared || _waveIndex < 0)
        {
            return;
        }
        if (_pendingSpawns.Count > 0 || _world.CountEntities(in EnemyQuery) > 0)
        {
            return;
        }
        Publish(new WaveCleared(_waveIndex + 1));
        if (_waveIndex + 1 >= Config.Waves.Count)
        {
            AllWavesCleared = true;
            if (Config.Boss is { } boss)
            {
                SpawnEnemy(
                    EnemyKind.Boss,
                    new Vector2(Config.FieldWidth + boss.Radius, Config.FieldHeight / 2f)
                );
            }
            return;
        }
        StartWave(_waveIndex + 1);
    }

    private void Fire(in InputState input)
    {
        if (_fireCooldown > 0)
        {
            _fireCooldown--;
        }
        if (!input.Shoot || _fireCooldown > 0)
        {
            return;
        }
        // PowerLevel ぶんの弾を Y 方向に等間隔で並べて撃つ
        for (var i = 0; i < PowerLevel; i++)
        {
            var offsetY = (i - (PowerLevel - 1) / 2f) * Config.PowerShotSpacing;
            _world.Create(
                new PlayerBulletComponent { Id = _nextBulletId++ },
                new PositionComponent { Value = _playerPosition + new Vector2(0f, offsetY) },
                new VelocityComponent { Value = new Vector2(Config.PlayerBulletSpeed, 0f) }
            );
        }
        _fireCooldown = Config.FireIntervalTicks;
    }

    /// <summary>シューター敵の発射。弾は発射時の自機方向へ等速直進(ホーミングしない)。</summary>
    private void FireShooters()
    {
        List<Vector2>? origins = null;
        _world.Query(
            in ShooterQuery,
            (ref ShooterComponent shooter, ref PositionComponent position) =>
            {
                if (--shooter.Cooldown > 0)
                {
                    return;
                }
                shooter.Cooldown = Config.ShooterFireIntervalTicks;
                (origins ??= []).Add(position.Value);
            }
        );
        if (origins is null)
        {
            return;
        }
        foreach (var origin in origins)
        {
            var direction = _playerPosition - origin;
            var velocity =
                direction == Vector2.Zero
                    ? new Vector2(-Config.EnemyBulletSpeed, 0f)
                    : Vector2.Normalize(direction) * Config.EnemyBulletSpeed;
            _world.Create(
                new EnemyBulletComponent { Id = _nextEnemyBulletId++ },
                new PositionComponent { Value = origin },
                new VelocityComponent { Value = velocity }
            );
        }
    }

    /// <summary>
    /// ボスの入場(アンカー X で停止)・停止後の上下蛇行・フェーズ遷移・弾幕発射。
    /// </summary>
    private void UpdateBoss()
    {
        var boss = BossOrDefault;
        List<(Vector2 Origin, int Phase)>? volleys = null;
        _world.Query(
            in BossQuery,
            (
                ref BossComponent state,
                ref EnemyComponent enemy,
                ref PositionComponent position,
                ref VelocityComponent velocity
            ) =>
            {
                if (!state.Anchored && position.Value.X <= boss.AnchorX)
                {
                    state.Anchored = true;
                    state.BaseY = position.Value.Y;
                    velocity.Value = Vector2.Zero;
                    position.Value.X = boss.AnchorX;
                }
                if (state.Anchored && boss.SinePeriodTicks > 0)
                {
                    state.Age++;
                    position.Value.Y =
                        state.BaseY
                        + boss.SineAmplitude
                            * MathF.Sin(2f * MathF.PI * state.Age / boss.SinePeriodTicks);
                }

                if (--state.Cooldown > 0)
                {
                    return;
                }
                state.Cooldown = boss.FireIntervalTicks;
                (volleys ??= []).Add((position.Value, state.Phase));
            }
        );
        if (volleys is null)
        {
            return;
        }
        foreach (var (origin, phase) in volleys)
        {
            FireBossVolley(origin, phase);
        }
    }

    /// <summary>被弾解決後の残 HP でフェーズを更新する。遷移した Tick に BossPhaseChanged を発行する。</summary>
    private void UpdateBossPhase()
    {
        var boss = BossOrDefault;
        var phaseChanges = new List<BossPhaseChanged>();
        _world.Query(
            in BossQuery,
            (
                ref BossComponent state,
                ref EnemyComponent enemy,
                ref PositionComponent _,
                ref VelocityComponent _
            ) =>
            {
                var phase = ComputeBossPhase(enemy.Hp, boss.Hp);
                if (phase != state.Phase)
                {
                    state.Phase = phase;
                    phaseChanges.Add(new BossPhaseChanged(phase));
                }
            }
        );
        foreach (var change in phaseChanges)
        {
            Publish(change);
        }
    }

    /// <summary>フェーズ別の弾幕。自機狙いを中心に 1 / 3 / 5 ウェイ。</summary>
    private void FireBossVolley(Vector2 origin, int phase)
    {
        var direction = _playerPosition - origin;
        var baseAngle =
            direction == Vector2.Zero ? MathF.PI : MathF.Atan2(direction.Y, direction.X);
        var wayCount = phase switch
        {
            1 => 1,
            2 => 3,
            _ => 5,
        };
        const float SpreadRadians = 15f * MathF.PI / 180f;
        for (var i = 0; i < wayCount; i++)
        {
            var angle = baseAngle + (i - (wayCount - 1) / 2f) * SpreadRadians;
            _world.Create(
                new EnemyBulletComponent { Id = _nextEnemyBulletId++ },
                new PositionComponent { Value = origin },
                new VelocityComponent
                {
                    Value =
                        new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Config.EnemyBulletSpeed,
                }
            );
        }
    }

    /// <summary>残 HP 比でフェーズを決める。2/3 以下で 2、1/3 以下で 3。</summary>
    private static int ComputeBossPhase(int hp, int maxHp) =>
        hp * 3 <= maxHp ? 3
        : hp * 3 <= maxHp * 2 ? 2
        : 1;

    private void ResolveBulletHits()
    {
        var destroyed = new List<EnemyDestroyed>();
        var dropOrigins = new List<Vector2>();
        _world.Query(
            in BulletQuery,
            (Entity bulletEntity, ref PlayerBulletComponent _, ref PositionComponent bulletPos) =>
            {
                var hit = false;
                var bulletPosition = bulletPos.Value;
                _world.Query(
                    in EnemyQuery,
                    (
                        Entity enemyEntity,
                        ref EnemyComponent enemy,
                        ref PositionComponent enemyPos
                    ) =>
                    {
                        if (hit || _toDestroy.Contains(enemyEntity))
                        {
                            return;
                        }
                        var range = Config.PlayerBulletRadius + EnemyRadiusOf(enemy.Kind);
                        if (!Overlaps(bulletPosition, enemyPos.Value, range))
                        {
                            return;
                        }
                        hit = true;
                        enemy.Hp--;
                        if (enemy.Hp <= 0)
                        {
                            _toDestroy.Add(enemyEntity);
                            destroyed.Add(new EnemyDestroyed(enemy.Id, enemy.Kind));
                            if (enemy.Kind != EnemyKind.Boss)
                            {
                                dropOrigins.Add(enemyPos.Value);
                            }
                        }
                    }
                );
                if (hit)
                {
                    _toDestroy.Add(bulletEntity);
                }
            }
        );
        FlushDestroyed();
        foreach (var origin in dropOrigins)
        {
            // ドロップ抽選。乱数消費は撃破ごとに1回で、入力列が同じなら再現する
            if (_random.NextDouble() < Config.PowerUpDropChance)
            {
                SpawnItem(origin);
            }
        }
        foreach (var evt in destroyed)
        {
            Publish(evt);
            if (evt.Kind == EnemyKind.Boss)
            {
                IsCleared = true;
                Publish(new GameCleared(Score));
            }
        }
    }

    /// <summary>アイテム ⭐ の取得。ショット強化が1段階上がる(上限あり)。</summary>
    private void ResolveItemPickup()
    {
        var range = Config.PlayerRadius + Config.ItemRadius;
        var collected = 0;
        _world.Query(
            in ItemQuery,
            (Entity entity, ref ItemComponent _, ref PositionComponent position) =>
            {
                if (Overlaps(_playerPosition, position.Value, range))
                {
                    _toDestroy.Add(entity);
                    collected++;
                }
            }
        );
        FlushDestroyed();
        for (var i = 0; i < collected; i++)
        {
            PowerLevel = Math.Min(Config.MaxPowerLevel, PowerLevel + 1);
            Publish(new PowerUpCollected(PowerLevel));
        }
    }

    /// <summary>ボス定数。テストが SpawnEnemy(Boss) を直接呼ぶ場合に備え、未設定なら既定値。</summary>
    private BossConfig BossOrDefault => Config.Boss ?? new BossConfig();

    /// <summary>敵種ごとの当たり判定半径。</summary>
    private float EnemyRadiusOf(EnemyKind kind) =>
        kind == EnemyKind.Boss ? (BossOrDefault).Radius : Config.EnemyRadius;

    private void ResolvePlayerHit()
    {
        if (IsInvincible)
        {
            return;
        }
        var hit = false;
        _world.Query(
            in EnemyQuery,
            (ref EnemyComponent enemy, ref PositionComponent position) =>
                hit |= Overlaps(
                    _playerPosition,
                    position.Value,
                    Config.PlayerRadius + EnemyRadiusOf(enemy.Kind)
                )
        );
        // 敵弾は当たった弾だけ消える。無敵中はこの分岐に来ないのですり抜ける
        var bulletRange = Config.PlayerRadius + Config.EnemyBulletRadius;
        _world.Query(
            in EnemyBulletQuery,
            (Entity entity, ref EnemyBulletComponent _, ref PositionComponent position) =>
            {
                if (Overlaps(_playerPosition, position.Value, bulletRange))
                {
                    hit = true;
                    _toDestroy.Add(entity);
                }
            }
        );
        FlushDestroyed();
        if (!hit)
        {
            return;
        }

        Lives--;
        PowerLevel = Math.Max(1, PowerLevel - 1);
        _invincibleTicksLeft = Config.InvincibleTicks;
        Publish(new PlayerHit(Lives));
        if (Lives <= 0)
        {
            IsGameOver = true;
            Publish(new GameEnded(Score));
        }
    }

    private void CullOffscreen()
    {
        _world.Query(
            in BulletQuery,
            (Entity entity, ref PlayerBulletComponent _, ref PositionComponent position) =>
            {
                if (position.Value.X - Config.PlayerBulletRadius > Config.FieldWidth)
                {
                    _toDestroy.Add(entity);
                }
            }
        );
        var enemyBulletRadius = Config.EnemyBulletRadius;
        _world.Query(
            in EnemyBulletQuery,
            (Entity entity, ref EnemyBulletComponent _, ref PositionComponent position) =>
            {
                var p = position.Value;
                if (
                    p.X + enemyBulletRadius < 0f
                    || p.X - enemyBulletRadius > Config.FieldWidth
                    || p.Y + enemyBulletRadius < 0f
                    || p.Y - enemyBulletRadius > Config.FieldHeight
                )
                {
                    _toDestroy.Add(entity);
                }
            }
        );
        _world.Query(
            in EnemyQuery,
            (Entity entity, ref EnemyComponent _, ref PositionComponent position) =>
            {
                if (position.Value.X + Config.EnemyRadius < 0f)
                {
                    _toDestroy.Add(entity);
                }
            }
        );
        _world.Query(
            in ItemQuery,
            (Entity entity, ref ItemComponent _, ref PositionComponent position) =>
            {
                if (position.Value.X + Config.ItemRadius < 0f)
                {
                    _toDestroy.Add(entity);
                }
            }
        );
        FlushDestroyed();
    }

    private void FlushDestroyed()
    {
        foreach (var entity in _toDestroy)
        {
            _world.Destroy(entity);
        }
        _toDestroy.Clear();
    }

    private static bool Overlaps(Vector2 a, Vector2 b, float range) =>
        Vector2.DistanceSquared(a, b) <= range * range;

    private void Publish<T>(in T evt)
        where T : ICommand
    {
        Router.PublishAsync(evt).GetAwaiter().GetResult();
    }
}
