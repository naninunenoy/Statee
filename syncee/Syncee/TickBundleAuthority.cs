namespace Syncee;

/// <summary>
/// サーバ側の権威ロール(D-054)。AuthorityLog(D-050。1コマンド=1手を即時確定)とは異なり、
/// Tickごとに期待クライアント数分の入力が揃うまで待ち、揃った時点で
/// <see cref="TickBundle"/> として確定・配布する。物理・当たり判定は持たず、
/// 入力を確定させるだけ(D-054)。ドメインの入力内容はこのクラスは知らない。
/// </summary>
public sealed class TickBundleAuthority(int expectedClientCount)
{
    private readonly Dictionary<
        int,
        Dictionary<string, IReadOnlyDictionary<string, string>?>
    > _pending = [];
    private readonly List<TickBundle> _entries = [];
    private int _expectedClientCount = expectedClientCount;

    public IReadOnlyList<TickBundle> Entries => _entries;

    public event Action<TickBundle>? Committed;

    /// <summary>
    /// 期待クライアント数を後から確定する(D-056。部屋の参加人数がロビー中に変わるため)。
    /// </summary>
    public void SetExpectedClientCount(int count) => _expectedClientCount = count;

    /// <summary>クライアントからTick番号付きの入力を受け取る。確定済みTickへの再送は無視する。</summary>
    public void Submit(int tick, string clientId, IReadOnlyDictionary<string, string>? input)
    {
        if (tick < _entries.Count)
        {
            return;
        }

        if (!_pending.TryGetValue(tick, out var slot))
        {
            slot = [];
            _pending[tick] = slot;
        }
        slot[clientId] = input;

        if (slot.Count < _expectedClientCount)
        {
            return;
        }

        var bundle = new TickBundle(tick, slot);
        _entries.Add(bundle);
        _pending.Remove(tick);
        Committed?.Invoke(bundle);
    }
}
