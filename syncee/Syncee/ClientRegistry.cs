namespace Syncee;

/// <summary>
/// サーバ側の接続管理+ブロードキャストの共通実装(D-055)。接続順の client-N 採番、
/// 切断時の登録解除、全クライアントへの送信という、確定モデル(即時確定の
/// AuthorityLog か、バッチ確定の TickBundleAuthority か)によらず共通のドメイン非依存部分を
/// 切り出したもの。確定ロジック自体はこのクラスの外側(呼び出し側)の責務。
/// </summary>
public sealed class ClientRegistry
{
    private readonly Dictionary<ITransport, string> _clients = [];
    private int _nextClientNumber;

    public ClientRegistry(IServerTransport transport)
    {
        transport.ClientConnected += OnClientConnected;
    }

    public int ConnectedClientCount => _clients.Count;

    /// <summary>クライアントが接続したときに発火する(座席割当等、呼び出し側の初期化用)。</summary>
    public event Action<string, ITransport>? ClientConnected;

    /// <summary>クライアントが切断したときに発火する(不戦勝判定等、呼び出し側の後始末用)。</summary>
    public event Action<string, ITransport>? ClientDisconnected;

    /// <summary>クライアントからの受信をclientId付きで通知する。</summary>
    public event Action<string, byte[]>? Received;

    /// <summary>接続中の全クライアントへ送信する。</summary>
    public void Broadcast(byte[] payload)
    {
        foreach (var client in _clients.Keys)
        {
            client.Send(payload);
        }
    }

    private void OnClientConnected(ITransport clientTransport)
    {
        var clientId = $"client-{++_nextClientNumber}";
        _clients[clientTransport] = clientId;

        clientTransport.Received += bytes => Received?.Invoke(clientId, bytes);
        clientTransport.Disconnected += () =>
        {
            _clients.Remove(clientTransport);
            ClientDisconnected?.Invoke(clientId, clientTransport);
        };
        ClientConnected?.Invoke(clientId, clientTransport);
    }
}
