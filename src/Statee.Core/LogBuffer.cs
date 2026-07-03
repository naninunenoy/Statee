namespace Statee.Core;

/// <summary>固定容量のログリングバッファ。容量超過時は古いエントリから破棄する。スレッドセーフ。</summary>
public sealed class LogBuffer
{
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries;
    private readonly int _capacity;

    public LogBuffer(int capacity)
    {
        _capacity = capacity;
        _entries = new Queue<LogEntry>(capacity);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public void Add(LogEntry entry)
    {
        lock (_gate)
        {
            if (_entries.Count == _capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }
    }

    /// <summary>末尾(最新)側から最大 count 件を時系列順で返す。</summary>
    public IReadOnlyList<LogEntry> Tail(int count)
    {
        lock (_gate)
        {
            var skip = Math.Max(0, _entries.Count - count);
            return _entries.Skip(skip).ToArray();
        }
    }
}
