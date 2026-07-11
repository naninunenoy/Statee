namespace Syncee;

/// <summary>
/// 1対の通信端点(接続1本)を表す最小抽象(D-050)。フェイク(インプロセス)・
/// LiteNetLib(実ソケット)の両方がこれを実装する。
/// </summary>
public interface ITransport
{
    event Action<byte[]>? Received;
    event Action? Disconnected;
    void Send(byte[] payload);
    void Disconnect();
}
