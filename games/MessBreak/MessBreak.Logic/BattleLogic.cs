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

    /// <summary>次の発射まで待つ残り tick 数。</summary>
    public int FireCooldown { get; private set; }

    /// <summary>飛翔中のプレイヤーの弾。</summary>
    public IReadOnlyList<Bullet> Bullets => _bullets;

    // 敵
    public Vector2 EnemyPos { get; private set; } = config.EnemySpawn;
    public int EnemyHp { get; private set; } = config.EnemyMaxHp;
    public EnemyAction EnemyAction { get; private set; } = EnemyAction.Idle;

    private readonly List<Bullet> _bullets = [];

    /// <summary>ドッジの残り無敵 tick 数。</summary>
    private int _actionTicks;

    private int _enemyPhaseTicks;

    /// <summary>1 tick 進める。Phase が Playing 以外なら何もしない。</summary>
    public void Tick(TickInput input)
    {
        if (Phase != BattlePhase.Playing)
        {
            return;
        }
        TickCount++;
        if (DodgeCooldown > 0)
        {
            DodgeCooldown--;
        }
        TickPlayer(input);
        TickEnemy();
        TickPhase();
    }

    private float Dt => 1f / Config.TicksPerSecond;

    private void TickPlayer(TickInput input)
    {
        switch (PlayerAction)
        {
            case PlayerAction.Free:
                if (input.Dodge && DodgeCooldown == 0)
                {
                    PlayerAction = PlayerAction.Dodge;
                    _actionTicks = Config.DodgeTicks;
                    DodgeCooldown = Config.DodgeCooldownTicks;
                    TickDodge();
                    return;
                }
                Move(input.MoveDir);
                return;

            case PlayerAction.Dodge:
                TickDodge();
                return;

            case PlayerAction.Dead:
                return;
        }
    }

    private void Move(Vector2 dir)
    {
        if (dir == Vector2.Zero)
        {
            return;
        }
        var unit = Vector2.Normalize(dir);
        PlayerFacing = unit;
        PlayerPos = ClampToRoom(PlayerPos + unit * Config.PlayerSpeed * Dt, Config.PlayerRadius);
    }

    private void TickDodge()
    {
        _actionTicks--;
        PlayerPos = ClampToRoom(
            PlayerPos + PlayerFacing * Config.DodgeSpeed * Dt,
            Config.PlayerRadius
        );
        if (_actionTicks <= 0)
        {
            PlayerAction = PlayerAction.Free;
        }
    }

    private void TickEnemy()
    {
        if (EnemyHp <= 0)
        {
            EnemyAction = EnemyAction.Dead;
            return;
        }
        var toPlayer = PlayerPos - EnemyPos;
        var distance = toPlayer.Length();
        switch (EnemyAction)
        {
            case EnemyAction.Idle:
                if (distance <= Config.EnemyAggroRange)
                {
                    EnemyAction = EnemyAction.Chase;
                }
                return;

            case EnemyAction.Chase:
                if (distance <= Config.EnemyAttackRange)
                {
                    EnemyAction = EnemyAction.Windup;
                    _enemyPhaseTicks = Config.EnemyWindupTicks;
                    return;
                }
                if (distance > 0f)
                {
                    EnemyPos = ClampToRoom(
                        EnemyPos + Vector2.Normalize(toPlayer) * Config.EnemySpeed * Dt,
                        Config.EnemyRadius
                    );
                }
                return;

            case EnemyAction.Windup:
                _enemyPhaseTicks--;
                if (_enemyPhaseTicks <= 0)
                {
                    // ドッジ中は無敵。範囲判定は Windup 完了時点の距離で行う
                    if (distance <= Config.EnemyAttackRange && PlayerAction != PlayerAction.Dodge)
                    {
                        PlayerHp = Math.Max(0, PlayerHp - Config.EnemyAttackDamage);
                    }
                    EnemyAction = EnemyAction.Recovery;
                    _enemyPhaseTicks = Config.EnemyRecoveryTicks;
                }
                return;

            case EnemyAction.Recovery:
                _enemyPhaseTicks--;
                if (_enemyPhaseTicks <= 0)
                {
                    EnemyAction = EnemyAction.Chase;
                }
                return;

            case EnemyAction.Dead:
                return;
        }
    }

    private void TickPhase()
    {
        if (EnemyHp <= 0)
        {
            EnemyAction = EnemyAction.Dead;
            Phase = BattlePhase.Victory;
        }
        else if (PlayerHp <= 0)
        {
            PlayerAction = PlayerAction.Dead;
            Phase = BattlePhase.Defeat;
        }
    }

    private Vector2 ClampToRoom(Vector2 pos, float radius) =>
        new(
            Math.Clamp(pos.X, radius, Config.RoomWidth - radius),
            Math.Clamp(pos.Y, radius, Config.RoomHeight - radius)
        );
}
