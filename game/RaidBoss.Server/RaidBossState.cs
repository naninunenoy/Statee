using RaidBoss.Logic;
using Statee.Core;

namespace RaidBoss.Server;

/// <summary>権威 State の公開(game/raidboss)。RaidBoss.Godot 側の State と同じ形にする。</summary>
[StateeState("game/raidboss")]
public partial class RaidBossState
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

    public void Update(GameLogic game) =>
        _current = new Snapshot(
            game.Seed,
            game.TickCount,
            game.BossHp,
            game.Player1Hp,
            game.Player2Hp,
            game.Phase
        );
}
