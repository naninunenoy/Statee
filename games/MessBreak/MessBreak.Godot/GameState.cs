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
        string Phase,
        float PlayerX,
        float PlayerY,
        float FacingX,
        float FacingY,
        int PlayerHp,
        string PlayerAction,
        int DodgeCooldown,
        int FireCooldown,
        int BulletCount,
        float EnemyX,
        float EnemyY,
        int EnemyHp,
        string EnemyAction
    );

    private volatile Snapshot _current = new(
        0,
        0,
        "",
        0f,
        0f,
        0f,
        0f,
        0,
        "",
        0,
        0,
        0,
        0f,
        0f,
        0,
        ""
    );

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int TickCount => _current.TickCount;

    [StateeField]
    public string Phase => _current.Phase;

    [StateeField]
    public float PlayerX => _current.PlayerX;

    [StateeField]
    public float PlayerY => _current.PlayerY;

    [StateeField]
    public float FacingX => _current.FacingX;

    [StateeField]
    public float FacingY => _current.FacingY;

    [StateeField]
    public int PlayerHp => _current.PlayerHp;

    [StateeField]
    public string PlayerAction => _current.PlayerAction;

    [StateeField]
    public int DodgeCooldown => _current.DodgeCooldown;

    [StateeField]
    public int FireCooldown => _current.FireCooldown;

    [StateeField]
    public int BulletCount => _current.BulletCount;

    [StateeField]
    public float EnemyX => _current.EnemyX;

    [StateeField]
    public float EnemyY => _current.EnemyY;

    [StateeField]
    public int EnemyHp => _current.EnemyHp;

    [StateeField]
    public string EnemyAction => _current.EnemyAction;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(BattleLogic logic)
    {
        _current = new Snapshot(
            logic.Seed,
            logic.TickCount,
            logic.Phase.ToString(),
            logic.PlayerPos.X,
            logic.PlayerPos.Y,
            logic.PlayerFacing.X,
            logic.PlayerFacing.Y,
            logic.PlayerHp,
            logic.PlayerAction.ToString(),
            logic.DodgeCooldown,
            logic.FireCooldown,
            logic.Bullets.Count,
            logic.EnemyPos.X,
            logic.EnemyPos.Y,
            logic.EnemyHp,
            logic.EnemyAction.ToString()
        );
    }
}
