using System.Numerics;

namespace TpsGame.Logic;

/// <summary>
/// 三人称シューティングの規則エンジン。固定タイムステップ(60Hz)の
/// Tick(InputState) でだけ状態が進む完全決定論。運動・衝突は自前の数式で、
/// Godot 物理を使わない。
/// </summary>
public sealed class TpsLogic
{
    private sealed class Bullet
    {
        public required int Id;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
    }

    private sealed class Target
    {
        public required int Id;
        public Vector3 Position;
        public bool Alive = true;
        public int RespawnTicksLeft;
    }

    private readonly List<Bullet> _bullets = [];
    private readonly List<Target> _targets = [];
    private readonly List<GameEvent> _events = [];
    private readonly List<InputState> _inputLog = [];
    private Vector3 _playerPosition;
    private float _playerYaw;
    private int _fireCooldown;
    private int _nextBulletId = 1;
    private int _nextTargetId = 1;

    public TpsLogic(int seed, TpsConfig? config = null)
    {
        Seed = seed;
        Config = config ?? new TpsConfig();
        _playerPosition = Config.PlayerStart;
        _playerYaw = Config.PlayerStartYaw;
        foreach (var spawn in Config.TargetSpawns)
        {
            _targets.Add(
                new Target
                {
                    Id = _nextTargetId++,
                    Position = spawn,
                    Alive = true,
                }
            );
        }
    }

    /// <summary>生成に使ったシード。再現性検証のため State で公開する。</summary>
    public int Seed { get; }

    /// <summary>適用中のルール定数。</summary>
    public TpsConfig Config { get; }

    /// <summary>進んだ Tick 数(60Hz)。</summary>
    public int TickCount { get; private set; }

    /// <summary>プレイヤーの足元位置。</summary>
    public Vector3 PlayerPosition => _playerPosition;

    /// <summary>プレイヤーの向き(ラジアン。0 で +Z 正面)。</summary>
    public float PlayerYaw => _playerYaw;

    /// <summary>破壊した的の累計。</summary>
    public int Score { get; private set; }

    /// <summary>Esc 等で終了要求が出たか。true 以降は Tick が状態を変えない。</summary>
    public bool IsQuitRequested { get; private set; }

    /// <summary>直前の Tick で起きた出来事(毎 Tick 先頭でクリア)。</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>場に出ている弾(Id 昇順)。</summary>
    public IReadOnlyList<BulletSnapshot> Bullets
    {
        get
        {
            var list = new List<BulletSnapshot>(_bullets.Count);
            foreach (var b in _bullets.OrderBy(b => b.Id))
            {
                list.Add(new BulletSnapshot(b.Id, b.Position, b.Yaw));
            }
            return list;
        }
    }

    /// <summary>的一覧(Id 昇順。破壊中も含む)。</summary>
    public IReadOnlyList<TargetSnapshot> Targets
    {
        get
        {
            var list = new List<TargetSnapshot>(_targets.Count);
            foreach (var t in _targets.OrderBy(t => t.Id))
            {
                list.Add(new TargetSnapshot(t.Id, t.Position, t.Alive, t.RespawnTicksLeft));
            }
            return list;
        }
    }

    /// <summary>生存中の的の数。</summary>
    public int AliveTargetCount => _targets.Count(t => t.Alive);

    /// <summary>受け付けた全入力の記録(Tick ごと)。</summary>
    public IReadOnlyList<InputState> InputLog => _inputLog;

    /// <summary>1 Tick 進める。終了要求後は何もしない。</summary>
    public void Tick(InputState input)
    {
        if (IsQuitRequested)
        {
            return;
        }

        _events.Clear();
        _inputLog.Add(input);
        TickCount++;

        if (input.Quit)
        {
            IsQuitRequested = true;
            _events.Add(new GameEvent("QuitRequested", $"tick={TickCount}"));
            return;
        }

        // 復活カウントは破壊より先に進める。破壊した Tick で RespawnTicks が
        // 即減らないようにし、「破壊から N Tick 後に復活」の意味を保つ
        TickRespawns();
        MovePlayer(input);
        TryShoot(input);
        MoveBullets();
        ResolveHits();
    }

    private void MovePlayer(InputState input)
    {
        var dx = 0f;
        var dz = 0f;
        if (input.Forward)
        {
            dz += 1f;
        }
        if (input.Back)
        {
            dz -= 1f;
        }
        if (input.Left)
        {
            dx -= 1f;
        }
        if (input.Right)
        {
            dx += 1f;
        }

        if (dx == 0f && dz == 0f)
        {
            return;
        }

        var len = MathF.Sqrt(dx * dx + dz * dz);
        dx /= len;
        dz /= len;

        // ワールド相対の WASD。移動方向へ向きを合わせる(三人称の自然な見た目)
        _playerYaw = MathF.Atan2(dx, dz);
        _playerPosition = ClampToArena(
            new Vector3(
                _playerPosition.X + dx * Config.PlayerSpeed,
                0f,
                _playerPosition.Z + dz * Config.PlayerSpeed
            )
        );
    }

    private void TryShoot(InputState input)
    {
        if (_fireCooldown > 0)
        {
            _fireCooldown--;
        }

        if (!input.Shoot || _fireCooldown > 0)
        {
            return;
        }

        var forward = ForwardFromYaw(_playerYaw);
        var origin =
            _playerPosition
            + new Vector3(0f, Config.BulletHeight, 0f)
            + forward * (Config.PlayerRadius + Config.BulletRadius + 0.05f);
        _bullets.Add(
            new Bullet
            {
                Id = _nextBulletId++,
                Position = origin,
                Velocity = forward * Config.BulletSpeed,
                Yaw = _playerYaw,
            }
        );
        _fireCooldown = Config.FireIntervalTicks;
        _events.Add(new GameEvent("ShotFired", $"bullet={_nextBulletId - 1}"));
    }

    private void MoveBullets()
    {
        for (var i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            b.Position += b.Velocity;
            if (!IsInsideArena(b.Position))
            {
                _bullets.RemoveAt(i);
            }
        }
    }

    private void ResolveHits()
    {
        for (var bi = _bullets.Count - 1; bi >= 0; bi--)
        {
            var bullet = _bullets[bi];
            var hit = false;
            foreach (var target in _targets)
            {
                if (!target.Alive)
                {
                    continue;
                }
                if (!HitsTarget(bullet.Position, target.Position))
                {
                    continue;
                }

                target.Alive = false;
                target.RespawnTicksLeft = Config.TargetRespawnTicks;
                Score++;
                _events.Add(new GameEvent("TargetDestroyed", $"target={target.Id}"));
                hit = true;
                break;
            }

            if (hit)
            {
                _bullets.RemoveAt(bi);
            }
        }
    }

    private void TickRespawns()
    {
        foreach (var target in _targets)
        {
            if (target.Alive)
            {
                continue;
            }
            if (target.RespawnTicksLeft > 0)
            {
                target.RespawnTicksLeft--;
            }
            if (target.RespawnTicksLeft == 0)
            {
                target.Alive = true;
                _events.Add(new GameEvent("TargetRespawned", $"target={target.Id}"));
            }
        }
    }

    private bool HitsTarget(Vector3 bullet, Vector3 target)
    {
        if (bullet.Y < 0f || bullet.Y > Config.TargetHeight)
        {
            return false;
        }
        var dx = bullet.X - target.X;
        var dz = bullet.Z - target.Z;
        var r = Config.TargetRadius + Config.BulletRadius;
        return dx * dx + dz * dz <= r * r;
    }

    private Vector3 ClampToArena(Vector3 p)
    {
        var e = Config.ArenaHalfExtent - Config.PlayerRadius;
        return new Vector3(Math.Clamp(p.X, -e, e), 0f, Math.Clamp(p.Z, -e, e));
    }

    private bool IsInsideArena(Vector3 p)
    {
        var e = Config.ArenaHalfExtent;
        return p.X >= -e && p.X <= e && p.Z >= -e && p.Z <= e;
    }

    private static Vector3 ForwardFromYaw(float yaw) => new(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
}
