namespace Statee.Core;

/// <summary>
/// freeze / step 実行の中核(docs/adr/D-003.md, D-040)。エンジン非依存。
/// ゲームループが「凍結中でない間」毎フレーム OnFrame() を呼び、
/// ゲーム側は IsFrozen を自身のポーズ機構(Godot なら SceneTree.Paused)に写す。
/// Freeze/Unfreeze/Step はソケットスレッドから、OnFrame はメインスレッドから呼ばれる。
/// </summary>
public sealed class TimeControl
{
    // Monitor.Wait/PulseAll でフレーム進行を通知するため System.Threading.Lock でなく object
    private readonly object _gate = new();

    // Set 状態 = 「step が進行していない」。step 開始で Reset、完了・打ち切りで Set
    private readonly ManualResetEventSlim _stepIdle = new(true);
    private int _remainingFrames;
    private long _frameCount;
    private volatile bool _isFrozen;

    /// <summary>凍結中か。ゲームループはこれをエンジンのポーズに反映する。</summary>
    public bool IsFrozen => _isFrozen;

    /// <summary>OnFrame が呼ばれた累計回数(= 実際に進んだシミュレーションフレーム数)。</summary>
    public long FrameCount => Interlocked.Read(ref _frameCount);

    /// <summary>即時凍結する。進行中の step は打ち切られる。</summary>
    public void Freeze()
    {
        lock (_gate)
        {
            _remainingFrames = 0;
            _isFrozen = true;
            _stepIdle.Set();
        }
    }

    /// <summary>凍結を解除し、通常進行に戻す。</summary>
    public void Unfreeze()
    {
        lock (_gate)
        {
            _remainingFrames = 0;
            _isFrozen = false;
            _stepIdle.Set();
        }
    }

    /// <summary>指定フレーム数だけ進めた後、自動で再凍結する。</summary>
    public void Step(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frames, 1);
        lock (_gate)
        {
            _remainingFrames = frames;
            _isFrozen = false;
            _stepIdle.Reset();
        }
    }

    /// <summary>ゲームループが1シミュレーションフレームごとに呼ぶ。凍結中の呼び出しは無視する。</summary>
    public void OnFrame()
    {
        lock (_gate)
        {
            if (_isFrozen)
            {
                return;
            }

            Interlocked.Increment(ref _frameCount);
            Monitor.PulseAll(_gate);
            if (_remainingFrames > 0 && --_remainingFrames == 0)
            {
                _isFrozen = true;
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
