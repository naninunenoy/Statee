using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// 射撃場ロジック(docs/DESIGN.md「当たる感の検証」)。動かない的を撃ち、撃破すると
/// リスポーンする。勝敗はなく、命中統計(発射数・命中数・撃破数)で手触りを検証する。
/// tick 駆動・決定論。時間経過はすべて Tick 呼び出し回数で表し、実時間に依存しない。
/// Godot 層は入力を <see cref="TickInput"/> に詰めて Tick を呼び、公開プロパティを描画するだけ。
/// </summary>
public sealed class BattleLogic(BattleConfig config, int seed)
{
    public BattleConfig Config { get; } = config;

    /// <summary>生成に使ったシード(リスポーン位置の乱数)。State で公開して再現性を検証する。</summary>
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

    /// <summary>飛翔中のプレイヤーの弾。</summary>
    public IReadOnlyList<Bullet> Bullets => _bullets;

    // 的
    public Vector2 TargetPos { get; private set; } = config.TargetSpawn;
    public int TargetHp { get; private set; } = config.TargetMaxHp;

    /// <summary>リスポーンまでの残り tick 数(0 なら的は生存中)。</summary>
    public int TargetRespawnCooldown { get; private set; }

    // 命中統計(「当たる感」の検証指標)
    public int ShotCount { get; private set; }
    public int HitCount { get; private set; }
    public int KillCount { get; private set; }

    private readonly List<Bullet> _bullets = [];
    private int _nextBulletId = 1;

    /// <summary>ドッジの残り無敵 tick 数。</summary>
    private int _actionTicks;

    /// <summary>ドッジの移動方向(開始時の移動入力。移動入力なしでは発動しない)。</summary>
    private Vector2 _dodgeDir;

    /// <summary>1 tick 進める。</summary>
    public void Tick(TickInput input)
    {
        TickCount++;
        if (DodgeCooldown > 0)
        {
            DodgeCooldown--;
        }
        if (FireCooldown > 0)
        {
            FireCooldown--;
        }
        Aim(input);
        TickPlayer(input);
        TickBullets();
        TickTargetRespawn();
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
                if (input.Fire && FireCooldown == 0)
                {
                    _bullets.Add(new Bullet(_nextBulletId++, PlayerPos, AssistDir(PlayerFacing)));
                    FireCooldown = Config.FireCooldownTicks;
                }
                return;

            case PlayerAction.Dodge:
                TickDodge();
                return;
        }
    }

    /// <summary>エイムアシスト。スケルトン段階では補正しない。</summary>
    private Vector2 AssistDir(Vector2 facing) => facing;

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

    /// <summary>弾を進め、的への命中と部屋外への逸脱で消す。</summary>
    private void TickBullets()
    {
        for (var i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            var pos = bullet.Pos + bullet.Dir * Config.BulletSpeed * Dt;
            if (
                TargetHp > 0
                && (TargetPos - pos).Length() <= Config.BulletRadius + Config.TargetRadius
            )
            {
                TargetHp = Math.Max(0, TargetHp - Config.BulletDamage);
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

    /// <summary>撃破された的のリスポーン。スケルトン段階では復活しない。</summary>
    private void TickTargetRespawn() { }

    private Vector2 ClampToRoom(Vector2 pos, float radius) =>
        new(
            Math.Clamp(pos.X, radius, Config.RoomWidth - radius),
            Math.Clamp(pos.Y, radius, Config.RoomHeight - radius)
        );
}
