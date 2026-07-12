namespace Syncee;

/// <summary>
/// 既に受け入れ済みの <see cref="ITransport"/> を後から流し込める <see cref="IServerTransport"/>
/// 実装(D-056)。複数ルームを1プロセスで扱うとき、生の接続受け入れ(ロビー層)と
/// ルームごとの確定モデル(AuthorityLog / TickBundleAuthority)を仲介するために使う。
/// </summary>
public sealed class ManualServerTransport : IServerTransport
{
    public event Action<ITransport>? ClientConnected;

    /// <summary>ロビー層が振り分け先を決めた接続をこのルームへ引き渡す。</summary>
    public void Accept(ITransport transport) => ClientConnected?.Invoke(transport);
}
