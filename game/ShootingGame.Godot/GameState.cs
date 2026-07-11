using System.Collections.Generic;
using System.Linq;
using ShootingGame.Logic;
using Statee.Core;

namespace ShootingGame;

/// <summary>
/// ゲーム状態の State 公開。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// 検証に必要な情報を全公開する(画面上の演出で隠すものも State では隠さない)。
/// </summary>
[StateeState("game/shootinggame")]
public partial class GameState
{
    public sealed record BulletEntry(int Id, float X, float Y);

    public sealed record EnemyEntry(int Id, string Kind, float X, float Y, int Hp);

    public sealed record EventEntry(int Sequence, int Tick, string Name, string Detail);

    private sealed record Snapshot(
        int Seed,
        int TickCount,
        float PlayerX,
        float PlayerY,
        int Lives,
        int Score,
        bool IsInvincible,
        bool IsGameOver,
        int Wave,
        bool AllWavesCleared,
        BulletEntry[] PlayerBullets,
        BulletEntry[] EnemyBullets,
        EnemyEntry[] Enemies,
        int EventTotal,
        EventEntry[] Events
    );

    private volatile Snapshot _current = new(
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
        [],
        [],
        [],
        0,
        []
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
    public int Lives => _current.Lives;

    [StateeField]
    public int Score => _current.Score;

    [StateeField]
    public bool IsInvincible => _current.IsInvincible;

    [StateeField]
    public bool IsGameOver => _current.IsGameOver;

    [StateeField]
    public int Wave => _current.Wave;

    [StateeField]
    public bool AllWavesCleared => _current.AllWavesCleared;

    [StateeField]
    public IReadOnlyList<BulletEntry> PlayerBullets => _current.PlayerBullets;

    [StateeField]
    public IReadOnlyList<BulletEntry> EnemyBullets => _current.EnemyBullets;

    [StateeField]
    public IReadOnlyList<EnemyEntry> Enemies => _current.Enemies;

    /// <summary>発生したイベントの総数(リングバッファから消えたぶんも数える)。wait 条件に使う。</summary>
    [StateeField]
    public int EventTotal => _current.EventTotal;

    /// <summary>直近のイベントログ(古い順)。</summary>
    [StateeField]
    public IReadOnlyList<EventEntry> Events => _current.Events;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(ShootingLogic logic)
    {
        _current = new Snapshot(
            logic.Seed,
            logic.TickCount,
            logic.PlayerPosition.X,
            logic.PlayerPosition.Y,
            logic.Lives,
            logic.Score,
            logic.IsInvincible,
            logic.IsGameOver,
            logic.Wave,
            logic.AllWavesCleared,
            [.. logic.PlayerBullets.Select(b => new BulletEntry(b.Id, b.Position.X, b.Position.Y))],
            [.. logic.EnemyBullets.Select(b => new BulletEntry(b.Id, b.Position.X, b.Position.Y))],
            [
                .. logic.Enemies.Select(e => new EnemyEntry(
                    e.Id,
                    e.Kind.ToString(),
                    e.Position.X,
                    e.Position.Y,
                    e.Hp
                )),
            ],
            logic.EventLog.TotalCount,
            [
                .. logic.EventLog.Entries.Select(e => new EventEntry(
                    e.Sequence,
                    e.Tick,
                    e.Name,
                    e.Detail
                )),
            ]
        );
    }
}
