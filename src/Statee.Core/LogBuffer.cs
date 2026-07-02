namespace Statee.Core;

/// <summary>固定容量のログリングバッファ。容量超過時は古いエントリから破棄する。スレッドセーフ。</summary>
public sealed class LogBuffer
{
    public LogBuffer(int capacity) { }

    public int Count => 0;

    public void Add(LogEntry entry) { }

    /// <summary>新しい順ではなく時系列順で、末尾(最新)から最大 count 件を返す。</summary>
    public IReadOnlyList<LogEntry> Tail(int count) => [];
}
