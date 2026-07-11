using RaidBoss.Logic;
using Statee.Core;

namespace RaidBoss;

/// <summary>
/// ゲーム状態の State 公開。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// 検証に必要な情報を全公開する(画面上の演出で隠すものも State では隠さない)。
/// </summary>
[StateeState("game/raidboss")]
public partial class GameState
{
    private sealed record Snapshot(
        int Seed,
        int TickCount,
        int BossHp,
        int Player1Hp,
        int Player2Hp,
        GamePhase Phase
    );

    private volatile Snapshot _current = new(
        0,
        0,
        GameLogic.BossMaxHp,
        GameLogic.PlayerMaxHp,
        GameLogic.PlayerMaxHp,
        GamePhase.Playing
    );

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int TickCount => _current.TickCount;

    [StateeField]
    public int BossHp => _current.BossHp;

    [StateeField]
    public int Player1Hp => _current.Player1Hp;

    [StateeField]
    public int Player2Hp => _current.Player2Hp;

    [StateeField]
    public string Phase => _current.Phase.ToString();

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(
        int seed,
        int tickCount,
        int bossHp,
        int player1Hp,
        int player2Hp,
        GamePhase phase
    )
    {
        _current = new Snapshot(seed, tickCount, bossHp, player1Hp, player2Hp, phase);
    }
}
