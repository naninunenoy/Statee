namespace Statee.Core;

/// <summary>
/// pause / step 実行の中核(docs/MEMO.md D-003)。エンジン非依存。
/// ゲームループが「ポーズ中でない間」毎フレーム OnFrame() を呼び、
/// ゲーム側は IsPaused を自身のポーズ機構(Godot なら SceneTree.Paused)に写す。
/// Pause/Resume/Step はソケットスレッドから、OnFrame はメインスレッドから呼ばれる。
/// </summary>
public sealed class TimeControl
{
    // Monitor.Wait/PulseAll でフレーム進行を通知するため System.Threading.Lock でなく object
    private readonly object _gate = new();

    // Set 状態 = 「step が進行していない」。step 開始で Reset、完了・打ち切りで Set
    private readonly ManualResetEventSlim _stepIdle = new(true);
    private int _remainingFrames;
    private long _frameCount;
    private volatile bool _isPaused;

    /// <summary>ポーズ中か。ゲームループはこれをエンジンのポーズに反映する。</summary>
    public bool IsPaused => _isPaused;

    /// <summary>OnFrame が呼ばれた累計回数(= 実際に進んだシミュレーションフレーム数)。</summary>
    public long FrameCount => Interlocked.Read(ref _frameCount);

    /// <summary>即時ポーズする。進行中の step は打ち切られる。</summary>
    public void Pause()
    {
        lock (_gate)
        {
            _remainingFrames = 0;
            _isPaused = true;
            _stepIdle.Set();
        }
    }

    /// <summary>ポーズを解除し、通常進行に戻す。</summary>
    public void Resume()
    {
        lock (_gate)
        {
            _remainingFrames = 0;
            _isPaused = false;
            _stepIdle.Set();
        }
    }

    /// <summary>指定フレーム数だけ進めた後、自動で再ポーズする。</summary>
    public void Step(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frames, 1);
        lock (_gate)
        {
            _remainingFrames = frames;
            _isPaused = false;
            _stepIdle.Reset();
        }
    }

    /// <summary>ゲームループが1シミュレーションフレームごとに呼ぶ。ポーズ中の呼び出しは無視する。</summary>
    public void OnFrame()
    {
        lock (_gate)
        {
            if (_isPaused)
            {
                return;
            }

            Interlocked.Increment(ref _frameCount);
            Monitor.PulseAll(_gate);
            if (_remainingFrames > 0 && --_remainingFrames == 0)
            {
                _isPaused = true;
                _stepIdle.Set();
            }
        }
    }

    /// <summary>進行中の step の完了(または非 step 状態)まで呼び出しスレッドをブロックする。</summary>
    /// <returns>タイムアウトした場合 false。</returns>
    public bool WaitForStep(TimeSpan timeout) => _stepIdle.Wait(timeout);

    /// <summary>
    /// observedFrameCount から1フレーム以上進むまで呼び出しスレッドをブロックする(wait コマンド用)。
    /// </summary>
    /// <returns>タイムアウトした場合 false。</returns>
    public bool WaitForNextFrame(long observedFrameCount, TimeSpan timeout)
    {
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        lock (_gate)
        {
            while (Interlocked.Read(ref _frameCount) <= observedFrameCount)
            {
                var remaining = timeout - deadline.Elapsed;
                if (remaining <= TimeSpan.Zero || !Monitor.Wait(_gate, remaining))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
