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
        int SkillCooldown,
        int BulletCount,
        float TargetX,
        float TargetY,
        int TargetHp,
        int TargetRespawnCooldown,
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
        0,
        0,
        0f,
        0f,
        0,
        0,
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
    public int SkillCooldown => _current.SkillCooldown;

    [StateeField]
    public int BulletCount => _current.BulletCount;

    [StateeField]
    public float TargetX => _current.TargetX;

    [StateeField]
    public float TargetY => _current.TargetY;

    [StateeField]
    public int TargetHp => _current.TargetHp;

    [StateeField]
    public int TargetRespawnCooldown => _current.TargetRespawnCooldown;

    [StateeField]
    public int ShotCount => _current.ShotCount;

    [StateeField]
    public int HitCount => _current.HitCount;

    [StateeField]
    public int KillCount => _current.KillCount;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(BattleLogic logic)
    {
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
            logic.SkillCooldown,
            logic.Bullets.Count,
            logic.TargetPos.X,
            logic.TargetPos.Y,
            logic.TargetHp,
            logic.TargetRespawnCooldown,
            logic.ShotCount,
            logic.HitCount,
            logic.KillCount
        );
    }
}
