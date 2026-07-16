using VitalRouter;

namespace ShootingGame.Logic;

/// <summary>
/// VitalRouter の interceptor として全イベントを横取りし、リングバッファに記録する
/// (D-048。RogueGame の ActionLog =「入力の記録」と対になる「出来事の記録」)。
/// State 公開・wait 条件(例: EnemyDestroyed が N 件)の源になる。
/// </summary>
public sealed class EventLog(int capacity) : ICommandInterceptor
{
    private readonly Queue<EventLogEntry> _entries = new(capacity);

    /// <summary>保持できる最大件数。超えたら古いものから捨てる。</summary>
    public int Capacity { get; } = capacity;

    /// <summary>記録済みイベントの総数(リングバッファから消えたぶんも数える)。</summary>
    public int TotalCount { get; private set; }

    /// <summary>現在保持しているエントリ(古い順)。</summary>
    public IReadOnlyList<EventLogEntry> Entries => [.. _entries];

    /// <summary>記録に使う現在 Tick。ShootingLogic が Tick ごとに設定する。</summary>
    public int CurrentTick { get; set; }

    /// <inheritdoc/>
    public ValueTask InvokeAsync<T>(T command, PublishContext context, PublishContinuation<T> next)
        where T : ICommand
    {
        if (_entries.Count >= Capacity)
        {
            _entries.Dequeue();
        }
        TotalCount++;
        _entries.Enqueue(
            new EventLogEntry(TotalCount, CurrentTick, typeof(T).Name, command.ToString() ?? "")
        );
        return next(command, context);
    }
}
