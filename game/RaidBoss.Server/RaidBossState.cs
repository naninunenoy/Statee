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
        IReadOnlyList<int> PlayerHps,
        GamePhase Phase
    );

    private volatile Snapshot _current = new(0, 0, GameLogic.BossMaxHp, [], GamePhase.Waiting);

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int TickCount => _current.TickCount;

    [StateeField]
    public int BossHp => _current.BossHp;

    [StateeField]
    public string PlayerHps => string.Join(",", _current.PlayerHps);

    [StateeField]
    public string Phase => _current.Phase.ToString();

    public void Update(GameLogic game) =>
        _current = new Snapshot(game.Seed, game.TickCount, game.BossHp, game.PlayerHps, game.Phase);
}
