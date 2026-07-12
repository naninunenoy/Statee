using Statee.Core;

namespace TodoApp;

/// <summary>
/// ゲーム状態の State 公開。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// 実ゲームではフィールドを増やし、検証に必要な情報を全公開する
/// (画面上の演出で隠すものも State では隠さない)。
/// </summary>
[StateeState("game/todoapp")]
public partial class GameState
{
    private sealed record Snapshot(int Seed, int StepCount);

    private volatile Snapshot _current = new(0, 0);

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int StepCount => _current.StepCount;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(int seed, int stepCount)
    {
        _current = new Snapshot(seed, stepCount);
    }
}
