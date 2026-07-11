namespace Syncee;

/// <summary>
/// クライアント側のレプリカロール(D-054)。サーバから配布された確定Tickバンドルを
/// 受信順に <paramref name="apply"/> で適用する。ReplicaLog(D-050)と同型だが、
/// 確定単位が1コマンドではなく1Tick分の入力バンドルである点が異なる。
/// </summary>
public sealed class TickReplicaLog(Action<TickBundle> apply)
{
    private readonly List<TickBundle> _entries = [];

    public IReadOnlyList<TickBundle> Entries => _entries;

    /// <summary>サーバから確定Tickバンドルを1件受信したときに呼ぶ。</summary>
    public void OnReceived(TickBundle bundle)
    {
        _entries.Add(bundle);
        apply(bundle);
    }
}
