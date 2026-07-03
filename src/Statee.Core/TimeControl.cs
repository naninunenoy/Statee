namespace Statee.Core;

/// <summary>
/// pause / step 実行の中核(docs/MEMO.md D-003)。エンジン非依存。
/// ゲームループが「ポーズ中でない間」毎フレーム OnFrame() を呼び、
/// ゲーム側は IsPaused を自身のポーズ機構(Godot なら SceneTree.Paused)に写す。
/// Pause/Resume/Step はソケットスレッドから、OnFrame はメインスレッドから呼ばれる。
/// </summary>
public sealed class TimeControl
{
    /// <summary>ポーズ中か。ゲームループはこれをエンジンのポーズに反映する。</summary>
    public bool IsPaused => default;

    /// <summary>即時ポーズする。進行中の step は打ち切られる。</summary>
    public void Pause() { }

    /// <summary>ポーズを解除し、通常進行に戻す。</summary>
    public void Resume() { }

    /// <summary>指定フレーム数だけ進めた後、自動で再ポーズする。</summary>
    public void Step(int frames) { }

    /// <summary>ゲームループが1シミュレーションフレームごとに呼ぶ。ポーズ中の呼び出しは無視する。</summary>
    public void OnFrame() { }

    /// <summary>進行中の step の完了(または非 step 状態)まで呼び出しスレッドをブロックする。</summary>
    /// <returns>タイムアウトした場合 false。</returns>
    public bool WaitForStep(TimeSpan timeout) => default;
}
