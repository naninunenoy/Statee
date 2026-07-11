using LiteNetLib;

namespace Syncee.LiteNetLib;

/// <summary>
/// 実ソケット(LiteNetLib)によるクライアント側 <see cref="ITransport"/> 実装(D-050)。
/// <see cref="Connect"/> は非同期(接続確立を待たない)。確立を検知したい呼び出し側は
/// <see cref="Connected"/> を購読し、<see cref="PollEvents"/> をポーリングループから呼ぶ。
/// </summary>
public sealed class LiteNetLibClientTransport : ITransport, IDisposable
{
    private readonly NetManager _manager;
    private NetPeer? _peer;

    public LiteNetLibClientTransport() => _manager = new NetManager(new Listener(this));

    public event Action<byte[]>? Received;
    public event Action? Disconnected;

    /// <summary>接続が確立したときに発火する(ITransport の外側の拡張。接続完了検知用)。</summary>
    public event Action? Connected;

    public void Connect(string host, int port, string connectionKey = "syncee")
    {
        _manager.Start();
        _peer = _manager.Connect(host, port, connectionKey);
    }

    /// <summary>受信・接続イベントを処理する。メインループから毎回呼ぶ。</summary>
    public void PollEvents() => _manager.PollEvents();

    public void Send(byte[] payload) => _peer?.Send(payload, DeliveryMethod.ReliableOrdered);

    public void Disconnect() => _peer?.Disconnect();

    public void Dispose() => _manager.Stop();

    internal void RaiseReceived(byte[] payload) => Received?.Invoke(payload);

    internal void RaiseDisconnected() => Disconnected?.Invoke();

    internal void RaiseConnected() => Connected?.Invoke();

    private sealed class Listener(LiteNetLibClientTransport owner) : INetEventListener
    {
        public void OnConnectionRequest(ConnectionRequest request) => request.Reject();

        public void OnPeerConnected(NetPeer peer) => owner.RaiseConnected();

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) =>
            owner.RaiseDisconnected();

        public void OnNetworkReceive(
            NetPeer peer,
            NetPacketReader reader,
            byte channel,
            DeliveryMethod deliveryMethod
        )
        {
            owner.RaiseReceived(reader.GetRemainingBytes());
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
