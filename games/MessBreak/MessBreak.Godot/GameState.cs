using MessBreak.Logic;
using Statee.Core;

namespace MessBreak;

/// <summary>
/// ゲーム状態の State 公開。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// 検証に必要な情報を全公開する(画面上の演出で隠すものも State では隠さない)。
/// </summary>
[StateeState("game/messbreak")]
public partial class GameState
{
    private sealed record Snapshot(
        int Seed,
        int TickCount,
        float PlayerX,
        float PlayerY,
        float FacingX,
        float FacingY,
        string PlayerAction,
        int DodgeCooldown,
        int FireCooldown,
        string ActiveCharacter,
        int SwitchCooldown,
        int AttackerSkillCooldown,
        int DebufferSkillCooldown,
        int BulletCount,
        int MobHp,
        int MobDebuffTicks,
        bool ZoneCaptured,
        bool TurretPlaced,
        int TurretFireCooldown,
        bool BossAppeared,
        int BossHp,
        float BossX,
        float BossY,
        int BossDebuffTicks,
        bool MissionCleared,
        int ShotCount,
        int HitCount,
        int KillCount
    );

    private volatile Snapshot _current = new(
        0,
        0,
        0f,
        0f,
        0f,
        0f,
        "",
        0,
        0,
        "",
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        0,
        false,
        0,
        0f,
        0f,
        0,
        false,
        0,
        0,
        0
    );

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int TickCount => _current.TickCount;

    [StateeField]
    public float PlayerX => _current.PlayerX;

    [StateeField]
    public float PlayerY => _current.PlayerY;

    [StateeField]
    public float FacingX => _current.FacingX;

    [StateeField]
    public float FacingY => _current.FacingY;

    [StateeField]
    public string PlayerAction => _current.PlayerAction;

    [StateeField]
    public int DodgeCooldown => _current.DodgeCooldown;

    [StateeField]
    public int FireCooldown => _current.FireCooldown;

    [StateeField]
    public string ActiveCharacter => _current.ActiveCharacter;

    [StateeField]
    public int SwitchCooldown => _current.SwitchCooldown;

    [StateeField]
    public int AttackerSkillCooldown => _current.AttackerSkillCooldown;

    [StateeField]
    public int DebufferSkillCooldown => _current.DebufferSkillCooldown;

    [StateeField]
    public int BulletCount => _current.BulletCount;

    /// <summary>雑魚の残 HP(0 なら撃破済み)。</summary>
    [StateeField]
    public int MobHp => _current.MobHp;

    [StateeField]
    public int MobDebuffTicks => _current.MobDebuffTicks;

    [StateeField]
    public bool ZoneCaptured => _current.ZoneCaptured;

    [StateeField]
    public bool TurretPlaced => _current.TurretPlaced;

    [StateeField]
    public int TurretFireCooldown => _current.TurretFireCooldown;

    [StateeField]
    public bool BossAppeared => _current.BossAppeared;

    /// <summary>強敵の残 HP(未出現・撃破済みは 0)。</summary>
    [StateeField]
    public int BossHp => _current.BossHp;

    [StateeField]
    public float BossX => _current.BossX;

    [StateeField]
    public float BossY => _current.BossY;

    [StateeField]
    public int BossDebuffTicks => _current.BossDebuffTicks;

    [StateeField]
    public bool MissionCleared => _current.MissionCleared;

    [StateeField]
    public int ShotCount => _current.ShotCount;

    [StateeField]
    public int HitCount => _current.HitCount;

    [StateeField]
    public int KillCount => _current.KillCount;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(BattleLogic logic)
    {
        var mob = logic.EnemyOf(EnemyKind.Mob);
        var boss = logic.EnemyOf(EnemyKind.Boss);
        _current = new Snapshot(
            logic.Seed,
            logic.TickCount,
            logic.PlayerPos.X,
            logic.PlayerPos.Y,
            logic.PlayerFacing.X,
            logic.PlayerFacing.Y,
            logic.PlayerAction.ToString(),
            logic.DodgeCooldown,
            logic.FireCooldown,
            logic.ActiveCharacter.ToString(),
            logic.SwitchCooldown,
            logic.SkillCooldownOf(CharacterId.Attacker),
            logic.SkillCooldownOf(CharacterId.Debuffer),
            logic.Bullets.Count,
            mob?.Hp ?? 0,
            mob?.DebuffTicks ?? 0,
            logic.ZoneCaptured,
            logic.TurretPlaced,
            logic.TurretFireCooldown,
            logic.BossAppeared,
            boss?.Hp ?? 0,
            boss?.Pos.X ?? 0f,
            boss?.Pos.Y ?? 0f,
            boss?.DebuffTicks ?? 0,
            logic.MissionCleared,
            logic.ShotCount,
            logic.HitCount,
            logic.KillCount
        );
    }
}
