using LiteNetLib;

namespace Syncee.LiteNetLib;

/// <summary>
/// 実ソケット(LiteNetLib)による <see cref="ITransport"/> 実装(D-050)。
/// 1本の <see cref="NetPeer"/> を1本の接続として扱う薄いラッパー。
/// サーバ側は <see cref="LiteNetLibServerTransport"/> が接続の都度これを生成する。
/// </summary>
public sealed class LiteNetLibTransport : ITransport
{
    private readonly NetPeer _peer;

    internal LiteNetLibTransport(NetPeer peer)
    {
        _peer = peer;
        peer.Tag = this;
    }

    public event Action<byte[]>? Received;
    public event Action? Disconnected;

    public void Send(byte[] payload) => _peer.Send(payload, DeliveryMethod.ReliableOrdered);

    public void Disconnect() => _peer.Disconnect();

    internal void RaiseReceived(byte[] payload) => Received?.Invoke(payload);

    internal void RaiseDisconnected() => Disconnected?.Invoke();
}
