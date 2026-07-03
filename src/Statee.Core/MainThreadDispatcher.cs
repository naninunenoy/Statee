using System.Collections.Concurrent;

namespace Statee.Core;

/// <summary>
/// ソケットスレッドから届いたコマンドをメインスレッドで実行するためのディスパッチャ。
/// ゲーム側がメインループ(毎フレーム)から <see cref="Pump"/> を呼び、
/// <see cref="Run"/> は処理の完了までブロックして結果を返す。
/// メインスレッド自身から <see cref="Run"/> を呼ぶとデッドロックするため、
/// ソケットスレッド専用とする(タイムアウトが保険)。
/// </summary>
public sealed class MainThreadDispatcher(TimeSpan? timeout = null)
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(5);

    /// <summary>処理をキューに積み、メインスレッドでの実行完了を待って結果を返す。</summary>
    public object? Run(Func<object?> action)
    {
        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _queue.Enqueue(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        });
        // Task.Wait は失敗時に AggregateException で包むため、包まない WaitHandle で待つ
        if (!((IAsyncResult)completion.Task).AsyncWaitHandle.WaitOne(_timeout))
        {
            throw new TimeoutException(
                $"メインスレッドが {_timeout.TotalSeconds} 秒以内に応答しない (Pump が呼ばれているか確認する)"
            );
        }

        return completion.Task.GetAwaiter().GetResult();
    }

    /// <summary>キューに溜まった処理をすべて実行する。メインスレッドから毎フレーム呼ぶ。</summary>
    public void Pump()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }
}
