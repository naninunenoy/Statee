using System.Collections.Generic;
using Statee.Core;

namespace SuikaGame;

/// <summary>
/// 盤面の State 公開(D-022)。CaptureState はソケットスレッドで走るため(D-019)、
/// メインスレッドが毎物理フレーム差し替える不変スナップショットを読むだけにする。
/// フルーツの位置(Godot 所有)をスレッド境界を越えて安全に観測するための橋。
/// </summary>
[StateeState("game/board")]
public partial class BoardState
{
    /// <summary>盤面上のフルーツ1個。Id はフレームを跨いで安定(GUIDELINE 3.4)。</summary>
    public sealed record FruitEntry(int Id, string Kind, float X, float Y);

    private sealed record Snapshot(
        int Score,
        bool IsGameOver,
        bool Paused,
        string NextKind,
        IReadOnlyList<FruitEntry> Fruits
    );

    private volatile Snapshot _current = new(0, false, false, "", []);

    [StateeField]
    public int Score => _current.Score;

    [StateeField]
    public bool IsGameOver => _current.IsGameOver;

    [StateeField]
    public bool Paused => _current.Paused;

    [StateeField]
    public string NextKind => _current.NextKind;

    [StateeField]
    public int FruitCount => _current.Fruits.Count;

    [StateeField]
    public IReadOnlyList<FruitEntry> Fruits => _current.Fruits;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(
        int score,
        bool isGameOver,
        bool paused,
        string nextKind,
        IReadOnlyList<FruitEntry> fruits
    )
    {
        _current = new Snapshot(score, isGameOver, paused, nextKind, fruits);
    }
}
