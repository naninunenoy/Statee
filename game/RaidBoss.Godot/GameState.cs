using System.Collections.Generic;
using System.Linq;
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
        IReadOnlyList<int> PlayerHps,
        IReadOnlyList<int> IncapacitatedTicks,
        IReadOnlyList<int> PlayerLanes,
        IReadOnlyList<Projectile> Projectiles,
        int PendingBossAttackLane,
        int PendingBossAttackTicks,
        GamePhase Phase
    );

    private volatile Snapshot _current = new(
        0,
        0,
        GameLogic.BossMaxHp,
        [],
        [],
        [],
        [],
        -1,
        0,
        GamePhase.Waiting
    );

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int TickCount => _current.TickCount;

    [StateeField]
    public int BossHp => _current.BossHp;

    [StateeField]
    public string PlayerHps => string.Join(",", _current.PlayerHps);

    [StateeField]
    public string IncapacitatedTicks => string.Join(",", _current.IncapacitatedTicks);

    [StateeField]
    public string PlayerLanes => string.Join(",", _current.PlayerLanes);

    [StateeField]
    public string Projectiles =>
        string.Join(";", _current.Projectiles.Select(p => $"{p.OwnerIndex}:{p.TicksRemaining}"));

    /// <summary>予告中のボス攻撃(D-059)。「レーン:残りTick」。予告がなければ空文字。</summary>
    [StateeField]
    public string PendingBossAttack =>
        _current.PendingBossAttackLane >= 0
            ? $"{_current.PendingBossAttackLane}:{_current.PendingBossAttackTicks}"
            : "";

    [StateeField]
    public string Phase => _current.Phase.ToString();

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える(配列はコピーして固定する)。</summary>
    public void Update(GameLogic game) =>
        _current = new Snapshot(
            game.Seed,
            game.TickCount,
            game.BossHp,
            game.PlayerHps.ToArray(),
            game.IncapacitatedTicks.ToArray(),
            game.PlayerLanes.ToArray(),
            game.Projectiles.ToArray(),
            game.PendingBossAttackLane,
            game.PendingBossAttackTicks,
            game.Phase
        );
}
