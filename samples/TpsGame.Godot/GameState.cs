using System.Collections.Generic;
using System.Linq;
using Statee.Core;
using TpsGame.Logic;

namespace TpsGame;

/// <summary>
/// ゲーム状態の State 公開。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// </summary>
[StateeState("game/tpsgame")]
public partial class GameState
{
    public sealed record BulletEntry(int Id, float X, float Y, float Z, float Yaw);

    public sealed record TargetEntry(
        int Id,
        float X,
        float Y,
        float Z,
        bool Alive,
        int RespawnTicksLeft
    );

    public sealed record EventEntry(string Name, string Detail);

    private sealed record Snapshot(
        int Seed,
        int TickCount,
        float PlayerX,
        float PlayerY,
        float PlayerZ,
        float PlayerYaw,
        int Score,
        bool IsQuitRequested,
        int AliveTargetCount,
        BulletEntry[] Bullets,
        TargetEntry[] Targets,
        EventEntry[] Events
    );

    private volatile Snapshot _current =
        new(0, 0, 0, 0, 0, 0, 0, false, 0, [], [], []);

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int TickCount => _current.TickCount;

    [StateeField]
    public float PlayerX => _current.PlayerX;

    [StateeField]
    public float PlayerY => _current.PlayerY;

    [StateeField]
    public float PlayerZ => _current.PlayerZ;

    [StateeField]
    public float PlayerYaw => _current.PlayerYaw;

    [StateeField]
    public int Score => _current.Score;

    [StateeField]
    public bool IsQuitRequested => _current.IsQuitRequested;

    [StateeField]
    public int AliveTargetCount => _current.AliveTargetCount;

    [StateeField]
    public IReadOnlyList<BulletEntry> Bullets => _current.Bullets;

    [StateeField]
    public IReadOnlyList<TargetEntry> Targets => _current.Targets;

    [StateeField]
    public IReadOnlyList<EventEntry> Events => _current.Events;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(TpsLogic logic)
    {
        _current = new Snapshot(
            logic.Seed,
            logic.TickCount,
            logic.PlayerPosition.X,
            logic.PlayerPosition.Y,
            logic.PlayerPosition.Z,
            logic.PlayerYaw,
            logic.Score,
            logic.IsQuitRequested,
            logic.AliveTargetCount,
            [
                .. logic.Bullets.Select(b => new BulletEntry(
                    b.Id,
                    b.Position.X,
                    b.Position.Y,
                    b.Position.Z,
                    b.Yaw
                )),
            ],
            [
                .. logic.Targets.Select(t => new TargetEntry(
                    t.Id,
                    t.Position.X,
                    t.Position.Y,
                    t.Position.Z,
                    t.Alive,
                    t.RespawnTicksLeft
                )),
            ],
            [.. logic.Events.Select(e => new EventEntry(e.Name, e.Detail))]
        );
    }
}
