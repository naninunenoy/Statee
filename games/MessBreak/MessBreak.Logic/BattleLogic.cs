using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// 小部屋の制圧ミッション(docs/DESIGN.md「縦切り3-1」)。敵エリアの雑魚を倒すと制圧=
/// 設置スロットが解放され、タレットを置ける。出現ポイントをアトラクトすると強敵が現れ、
/// 射撃+スキルコンボ+タレットで撃破するとミッション達成。
/// tick 駆動・決定論。時間経過はすべて Tick 呼び出し回数で表し、実時間に依存しない。
/// Godot 層は入力を <see cref="TickInput"/> に詰めて Tick を呼び、公開プロパティを描画するだけ。
/// </summary>
public sealed class BattleLogic(BattleConfig config, int seed)
{
    public BattleConfig Config { get; } = config;

    /// <summary>生成に使ったシード。State で公開して再現性を検証する(現状 乱数は未使用)。</summary>
    public int Seed { get; } = seed;

    /// <summary>経過 tick 数。</summary>
    public int TickCount { get; private set; }

    // プレイヤー
    public Vector2 PlayerPos { get; private set; } = config.PlayerSpawn;
    public Vector2 PlayerFacing { get; private set; } = new(1f, 0f);
    public PlayerAction PlayerAction { get; private set; } = PlayerAction.Free;
    public int DodgeCooldown { get; private set; }

    /// <summary>次の発射まで待つ残り tick 数。</summary>
    public int FireCooldown { get; private set; }

    /// <summary>操作中のキャラクター。</summary>
    public CharacterId ActiveCharacter { get; private set; } = CharacterId.Attacker;

    /// <summary>次の切り替えまで待つ残り tick 数。</summary>
    public int SwitchCooldown { get; private set; }

    /// <summary>操作中キャラのスキルの残りクールダウン tick 数。</summary>
    public int SkillCooldown => SkillCooldownOf(ActiveCharacter);

    /// <summary>指定キャラのスキルの残りクールダウン tick 数(キャラごとに独立)。</summary>
    public int SkillCooldownOf(CharacterId id) => _skillCooldowns[(int)id];

    private readonly int[] _skillCooldowns = new int[2];

    /// <summary>飛翔中の弾(プレイヤー・タレット共用)。</summary>
    public IReadOnlyList<Bullet> Bullets => _bullets;

    /// <summary>生存中の敵。HP 0 になった敵は取り除かれる。</summary>
    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>指定種別の生存中の敵(いなければ null)。</summary>
    public Enemy? EnemyOf(EnemyKind kind) => _enemies.Find(e => e.Kind == kind);

    private readonly List<Enemy> _enemies =
    [
        new Enemy
        {
            Id = 1,
            Kind = EnemyKind.Mob,
            Pos = config.MobSpawn,
            Hp = config.MobMaxHp,
        },
    ];

    /// <summary>敵エリアを制圧済みか(雑魚を倒すと true。設置スロットが解放される)。</summary>
    public bool ZoneCaptured { get; private set; }

    /// <summary>タレットを設置済みか。</summary>
    public bool TurretPlaced { get; private set; }

    /// <summary>タレットの次の発射まで待つ残り tick 数。</summary>
    public int TurretFireCooldown { get; private set; }

    /// <summary>強敵を出現させたか(アトラクトは一度きり)。</summary>
    public bool BossAppeared { get; private set; }

    /// <summary>強敵を撃破してミッション達成したか。</summary>
    public bool MissionCleared { get; private set; }

    // 命中統計(「当たる感」の検証指標。タレットの弾は数えない)
    public int ShotCount { get; private set; }
    public int HitCount { get; private set; }
    public int KillCount { get; private set; }

    /// <summary>直近の Tick で起きた出来事。毎 Tick 先頭でクリアされる。</summary>
    public IReadOnlyList<BattleEvent> Events => _events;

    private readonly List<BattleEvent> _events = [];

    private readonly List<Bullet> _bullets = [];
    private int _nextBulletId = 1;
    private int _nextEnemyId = 2;

    /// <summary>ドッジの残り無敵 tick 数。</summary>
    private int _actionTicks;

    /// <summary>ドッジの移動方向(開始時の移動入力。移動入力なしでは発動しない)。</summary>
    private Vector2 _dodgeDir;

    /// <summary>1 tick 進める。</summary>
    public void Tick(TickInput input)
    {
        _events.Clear();
        TickCount++;
        if (DodgeCooldown > 0)
        {
            DodgeCooldown--;
        }
        if (FireCooldown > 0)
        {
            FireCooldown--;
        }
        for (var i = 0; i < _skillCooldowns.Length; i++)
        {
            if (_skillCooldowns[i] > 0)
            {
                _skillCooldowns[i]--;
            }
        }
        if (SwitchCooldown > 0)
        {
            SwitchCooldown--;
        }
        foreach (var enemy in _enemies)
        {
            if (enemy.DebuffTicks > 0)
            {
                enemy.DebuffTicks--;
            }
        }
        // 出現(TickPlayer 内のアトラクト)と同じ tick で動かないよう、追跡は先に行う
        TickBossChase();
        Aim(input);
        TickPlayer(input);
        TickTurret();
        TickBullets();
    }

    /// <summary>
    /// 向きの更新。エイム入力があればその方向(構え=ストレイフ)、
    /// なければ移動方向(非構え)。どちらもなければ前回を維持(docs/DESIGN.md「向き(構え)の仕様」)。
    /// </summary>
    private void Aim(TickInput input)
    {
        if (input.AimDir != Vector2.Zero)
        {
            PlayerFacing = Vector2.Normalize(input.AimDir);
        }
        else if (input.MoveDir != Vector2.Zero)
        {
            PlayerFacing = Vector2.Normalize(input.MoveDir);
        }
    }

    private float Dt => 1f / Config.TicksPerSecond;

    /// <summary>敵種別ごとの当たり判定半径。</summary>
    private float RadiusOf(EnemyKind kind) =>
        kind == EnemyKind.Mob ? Config.MobRadius : Config.BossRadius;

    private void TickPlayer(TickInput input)
    {
        switch (PlayerAction)
        {
            case PlayerAction.Free:
                // 移動入力がなければドッジは不発(その場ドッジ・向き頼りのドッジは作らない)
                if (input.Dodge && DodgeCooldown == 0 && input.MoveDir != Vector2.Zero)
                {
                    PlayerAction = PlayerAction.Dodge;
                    _actionTicks = Config.DodgeTicks;
                    DodgeCooldown = Config.DodgeCooldownTicks;
                    _dodgeDir = Vector2.Normalize(input.MoveDir);
                    TickDodge();
                    return;
                }
                Move(input.MoveDir, input.Sprint ? Config.SprintSpeed : Config.PlayerSpeed);
                if (
                    input.SwitchTo is { } switchTo
                    && switchTo != ActiveCharacter
                    && SwitchCooldown == 0
                )
                {
                    ActiveCharacter = switchTo;
                    SwitchCooldown = Config.SwitchCooldownTicks;
                    _events.Add(new BattleEvent(BattleEventKind.CharacterSwitched, PlayerPos));
                }
                if (
                    input.Place
                    && ZoneCaptured
                    && !TurretPlaced
                    && (Config.TurretSlot - PlayerPos).Length() <= Config.PlaceRange
                )
                {
                    TurretPlaced = true;
                    _events.Add(new BattleEvent(BattleEventKind.TurretPlaced, Config.TurretSlot));
                }
                if (
                    input.Attract
                    && !BossAppeared
                    && (Config.BossSpawn - PlayerPos).Length() <= Config.AttractRange
                )
                {
                    BossAppeared = true;
                    _enemies.Add(
                        new Enemy
                        {
                            Id = _nextEnemyId++,
                            Kind = EnemyKind.Boss,
                            Pos = Config.BossSpawn,
                            Hp = Config.BossMaxHp,
                        }
                    );
                    _events.Add(new BattleEvent(BattleEventKind.BossAppeared, Config.BossSpawn));
                }
                if (input.Skill && SkillCooldown == 0)
                {
                    var character = Config.CharacterOf(ActiveCharacter);
                    _skillCooldowns[(int)ActiveCharacter] = character.SkillCooldownTicks;
                    var center = SkillCenter(input.AimPoint, character.SkillRange);
                    _events.Add(new BattleEvent(BattleEventKind.SkillBurst, center));
                    // ApplyDamage は撃破時にリストから取り除くため、スナップショットを回す
                    foreach (var enemy in _enemies.ToArray())
                    {
                        var inRange =
                            (enemy.Pos - center).Length()
                            <= character.SkillRadius + RadiusOf(enemy.Kind);
                        if (!inRange)
                        {
                            continue;
                        }
                        if (ActiveCharacter == CharacterId.Attacker)
                        {
                            ApplyDamage(enemy, character.SkillDamage, enemy.Pos);
                        }
                        else
                        {
                            // デバッファー: ダメージなしで被ダメージ増幅デバフを付与
                            enemy.DebuffTicks = character.DebuffDurationTicks;
                            enemy.DebuffMultiplier = character.DebuffDamageMultiplier;
                            _events.Add(new BattleEvent(BattleEventKind.EnemyDebuffed, enemy.Pos));
                        }
                    }
                }
                if (input.Fire && FireCooldown == 0)
                {
                    _bullets.Add(new Bullet(_nextBulletId++, PlayerPos, AssistDir(PlayerFacing)));
                    FireCooldown = Config.FireCooldownTicks;
                    ShotCount++;
                    _events.Add(new BattleEvent(BattleEventKind.BulletFired, PlayerPos));
                }
                return;

            case PlayerAction.Dodge:
                TickDodge();
                return;
        }
    }

    /// <summary>
    /// エイムアシスト。発射方向との角度差が吸着角以内の敵のうち、最も向きが近いものの
    /// 中心へ吸わせる。該当がなければ補正しない。
    /// </summary>
    private Vector2 AssistDir(Vector2 facing)
    {
        if (Config.AimAssistDegrees <= 0f)
        {
            return facing;
        }
        var cosLimit = MathF.Cos(Config.AimAssistDegrees * MathF.PI / 180f);
        var best = facing;
        var bestDot = cosLimit;
        foreach (var enemy in _enemies)
        {
            var toEnemy = enemy.Pos - PlayerPos;
            if (toEnemy == Vector2.Zero)
            {
                continue;
            }
            var unit = Vector2.Normalize(toEnemy);
            var dot = Vector2.Dot(facing, unit);
            if (dot >= bestDot)
            {
                bestDot = dot;
                best = unit;
            }
        }
        return best;
    }

    private void Move(Vector2 dir, float speed)
    {
        if (dir == Vector2.Zero)
        {
            return;
        }
        var unit = Vector2.Normalize(dir);
        PlayerPos = ClampToRoom(PlayerPos + unit * speed * Dt, Config.PlayerRadius);
    }

    private void TickDodge()
    {
        _actionTicks--;
        PlayerPos = ClampToRoom(
            PlayerPos + _dodgeDir * Config.DodgeSpeed * Dt,
            Config.PlayerRadius
        );
        if (_actionTicks <= 0)
        {
            PlayerAction = PlayerAction.Free;
        }
    }

    /// <summary>設置済みタレットの自動射撃。射程内で最も近い敵へクールダウンごとに 1 発撃つ。</summary>
    private void TickTurret()
    {
        if (!TurretPlaced)
        {
            return;
        }
        if (TurretFireCooldown > 0)
        {
            TurretFireCooldown--;
            return;
        }
        Enemy? nearest = null;
        var nearestDist = Config.TurretRange;
        foreach (var enemy in _enemies)
        {
            var dist = (enemy.Pos - Config.TurretSlot).Length();
            if (dist <= nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }
        if (nearest is null)
        {
            return;
        }
        var dir = Vector2.Normalize(nearest.Pos - Config.TurretSlot);
        _bullets.Add(new Bullet(_nextBulletId++, Config.TurretSlot, dir, FromTurret: true));
        TurretFireCooldown = Config.TurretFireCooldownTicks;
        _events.Add(new BattleEvent(BattleEventKind.TurretFired, Config.TurretSlot));
    }

    /// <summary>弾を進め、敵への命中と部屋外への逸脱で消す。</summary>
    private void TickBullets()
    {
        for (var i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            var pos = bullet.Pos + bullet.Dir * Config.BulletSpeed * Dt;
            var hit = _enemies.Find(e =>
                (e.Pos - pos).Length() <= Config.BulletRadius + RadiusOf(e.Kind)
            );
            if (hit is not null)
            {
                var damage = bullet.FromTurret ? Config.TurretBulletDamage : Config.BulletDamage;
                if (!bullet.FromTurret)
                {
                    HitCount++;
                }
                ApplyDamage(hit, damage, pos);
                _bullets.RemoveAt(i);
                continue;
            }
            if (pos.X < 0f || pos.X > Config.RoomWidth || pos.Y < 0f || pos.Y > Config.RoomHeight)
            {
                _bullets.RemoveAt(i);
                continue;
            }
            _bullets[i] = bullet with { Pos = pos };
        }
    }

    /// <summary>強敵はプレイヤーを追跡する。接触距離(半径の和)までは詰めるが重ならない。</summary>
    private void TickBossChase()
    {
        var boss = EnemyOf(EnemyKind.Boss);
        if (boss is null)
        {
            return;
        }
        var toPlayer = PlayerPos - boss.Pos;
        var dist = toPlayer.Length();
        var contact = Config.BossRadius + Config.PlayerRadius;
        if (dist <= contact)
        {
            return;
        }
        var step = MathF.Min(Config.BossSpeed * Dt, dist - contact);
        boss.Pos += Vector2.Normalize(toPlayer) * step;
    }

    /// <summary>
    /// スキルの爆心。照準点(レティクル位置)があればそこ(射程上限でクランプ)、
    /// なければ向いている方向の射程いっぱい(ゲームパッド等のフォールバック)。
    /// </summary>
    private Vector2 SkillCenter(Vector2? aimPoint, float range)
    {
        if (aimPoint is not { } point)
        {
            return PlayerPos + PlayerFacing * range;
        }
        var toPoint = point - PlayerPos;
        if (toPoint.Length() <= range)
        {
            return point;
        }
        return PlayerPos + Vector2.Normalize(toPoint) * range;
    }

    /// <summary>
    /// 敵へのダメージ適用と撃破処理(弾・スキル共通)。命中統計は呼び出し側で数える。
    /// 雑魚の撃破はエリア制圧、強敵の撃破はミッション達成になる。
    /// </summary>
    private void ApplyDamage(Enemy enemy, int damage, Vector2 hitPos)
    {
        if (enemy.DebuffTicks > 0)
        {
            damage *= enemy.DebuffMultiplier;
        }
        enemy.Hp = Math.Max(0, enemy.Hp - damage);
        _events.Add(new BattleEvent(BattleEventKind.EnemyHit, hitPos));
        if (enemy.Hp > 0)
        {
            return;
        }
        KillCount++;
        _enemies.Remove(enemy);
        _events.Add(new BattleEvent(BattleEventKind.EnemyKilled, enemy.Pos));
        if (enemy.Kind == EnemyKind.Mob)
        {
            ZoneCaptured = true;
            _events.Add(new BattleEvent(BattleEventKind.ZoneCaptured, enemy.Pos));
        }
        else
        {
            MissionCleared = true;
            _events.Add(new BattleEvent(BattleEventKind.MissionCleared, enemy.Pos));
        }
    }

    private Vector2 ClampToRoom(Vector2 pos, float radius) =>
        new(
            Math.Clamp(pos.X, radius, Config.RoomWidth - radius),
            Math.Clamp(pos.Y, radius, Config.RoomHeight - radius)
        );
}
