using LiteNetLib;

namespace Syncee.LiteNetLib;

/// <summary>
/// 実ソケット(LiteNetLib)による <see cref="IServerTransport"/> 実装(D-050)。
/// 新規接続を受け入れるたびに <see cref="LiteNetLibTransport"/> を1本作り、
/// <see cref="ClientConnected"/> で通知する。
/// </summary>
public sealed class LiteNetLibServerTransport : IServerTransport, IDisposable
{
    private readonly NetManager _manager;

    public LiteNetLibServerTransport(int port, string connectionKey = "syncee")
    {
        _manager = new NetManager(new Listener(this, connectionKey));
        _manager.Start(port);
    }

    public event Action<ITransport>? ClientConnected;

    public int Port => _manager.LocalPort;

    /// <summary>受信・接続イベントを処理する。メインループから毎回呼ぶ。</summary>
    public void PollEvents() => _manager.PollEvents();

    public void Dispose() => _manager.Stop();

    private sealed class Listener(LiteNetLibServerTransport owner, string connectionKey)
        : INetEventListener
    {
        public void OnConnectionRequest(ConnectionRequest request) =>
            request.AcceptIfKey(connectionKey);

        public void OnPeerConnected(NetPeer peer) =>
            owner.ClientConnected?.Invoke(new LiteNetLibTransport(peer));

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) =>
            (peer.Tag as LiteNetLibTransport)?.RaiseDisconnected();

        public void OnNetworkReceive(
            NetPeer peer,
            NetPacketReader reader,
            byte channel,
            DeliveryMethod deliveryMethod
        )
        {
            (peer.Tag as LiteNetLibTransport)?.RaiseReceived(reader.GetRemainingBytes());
            reader.Recycle();
        }

        public void OnNetworkError(
            System.Net.IPEndPoint endPoint,
            System.Net.Sockets.SocketError socketError
        ) { }

        public void OnNetworkReceiveUnconnected(
            System.Net.IPEndPoint remoteEndPoint,
            NetPacketReader reader,
            UnconnectedMessageType messageType
        ) { }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    }
}
