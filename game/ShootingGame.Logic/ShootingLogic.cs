using System.Numerics;
using Arch.Core;
using VitalRouter;

namespace ShootingGame.Logic;

/// <summary>
/// 横スクロール STG の規則エンジン(D-048)。固定タイムステップ(60Hz)の
/// Tick(InputState) でだけ状態が進む完全決定論。運動・衝突は自前の数式で、
/// Godot 物理を使わない。弾・敵は Arch の Entity、イベントは VitalRouter で流す。
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

    private struct EnemyComponent
    {
        public int Id;
        public EnemyKind Kind;
        public int Hp;
    }

    private static readonly QueryDescription MovingQuery = new QueryDescription().WithAll<
        PositionComponent,
        VelocityComponent
    >();
    private static readonly QueryDescription BulletQuery = new QueryDescription().WithAll<
        PlayerBulletComponent,
        PositionComponent
    >();
    private static readonly QueryDescription EnemyQuery = new QueryDescription().WithAll<
        EnemyComponent,
        PositionComponent
    >();

    private readonly World _world = World.Create();
    private readonly HashSet<Entity> _toDestroy = [];
    private Vector2 _playerPosition;
    private int _fireCooldown;
    private int _invincibleTicksLeft;
    private int _nextBulletId = 1;
    private int _nextEnemyId = 1;

    public ShootingLogic(int seed, ShootingConfig? config = null)
    {
        Seed = seed;
        Config = config ?? new ShootingConfig();
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

    /// <summary>ゲーム内イベントの発行先。購読者(スコア係・演出係等)はここへ Subscribe する。</summary>
    public Router Router { get; } = new();

    /// <summary>全イベントの記録。State 公開・wait 条件の源。</summary>
    public EventLog EventLog { get; }

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

    /// <summary>1 Tick(1/60 秒)進める。自機移動 → 運動 → 発射 → 衝突 → 画面外掃除の順で解決する。</summary>
    public void Tick(in InputState input)
    {
        if (IsGameOver)
        {
            return;
        }

        TickCount++;
        EventLog.CurrentTick = TickCount;
        if (_invincibleTicksLeft > 0)
        {
            _invincibleTicksLeft--;
        }

        MovePlayer(input);
        MoveEntities();
        Fire(input);
        ResolveBulletHits();
        ResolvePlayerHit();
        CullOffscreen();
    }

    /// <summary>敵を出現させる(ウェーブ生成・テスト用)。EnemySpawned を発行する。</summary>
    public int SpawnEnemy(EnemyKind kind, Vector2 position)
    {
        var id = _nextEnemyId++;
        var velocity = kind switch
        {
            EnemyKind.Straight => new Vector2(-Config.StraightEnemySpeed, 0f),
            _ => Vector2.Zero,
        };
        _world.Create(
            new EnemyComponent
            {
                Id = id,
                Kind = kind,
                Hp = 1,
            },
            new PositionComponent { Value = position },
            new VelocityComponent { Value = velocity }
        );
        Publish(new EnemySpawned(id, kind));
        return id;
    }

    /// <inheritdoc/>
    public void Receive<T>(T command, PublishContext context)
        where T : ICommand
    {
        // スコア係。撃破イベントの購読でスコアを加算する(多対多配線の一端)
        if (command is EnemyDestroyed)
        {
            Score += Config.EnemyScore;
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
        _world.Create(
            new PlayerBulletComponent { Id = _nextBulletId++ },
            new PositionComponent { Value = _playerPosition },
            new VelocityComponent { Value = new Vector2(Config.PlayerBulletSpeed, 0f) }
        );
        _fireCooldown = Config.FireIntervalTicks;
    }

    private void ResolveBulletHits()
    {
        var hitRange = Config.PlayerBulletRadius + Config.EnemyRadius;
        var destroyed = new List<EnemyDestroyed>();
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
                        if (!Overlaps(bulletPosition, enemyPos.Value, hitRange))
                        {
                            return;
                        }
                        hit = true;
                        enemy.Hp--;
                        if (enemy.Hp <= 0)
                        {
                            _toDestroy.Add(enemyEntity);
                            destroyed.Add(new EnemyDestroyed(enemy.Id, enemy.Kind));
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
        foreach (var evt in destroyed)
        {
            Publish(evt);
        }
    }

    private void ResolvePlayerHit()
    {
        if (IsInvincible)
        {
            return;
        }
        var hitRange = Config.PlayerRadius + Config.EnemyRadius;
        var hit = false;
        _world.Query(
            in EnemyQuery,
            (ref EnemyComponent _, ref PositionComponent position) =>
                hit |= Overlaps(_playerPosition, position.Value, hitRange)
        );
        if (!hit)
        {
            return;
        }

        Lives--;
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
