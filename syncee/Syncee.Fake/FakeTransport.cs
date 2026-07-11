namespace Syncee.Fake;

/// <summary>
/// インプロセスの <see cref="ITransport"/> 実装(D-050)。ペアになったもう一方の
/// <see cref="FakeTransport"/> に対して同期的に(呼び出しスタック上で即座に)配送する。
/// テストの主戦場(freeze / step / 切断注入が全部効く)として使う。
/// </summary>
public sealed class FakeTransport : ITransport
{
    private FakeTransport? _peer;

    public event Action<byte[]>? Received;
    public event Action? Disconnected;

    /// <summary>もう一方の端点を結びつける。<see cref="FakeServerTransport.Connect"/> がペア生成時に呼ぶ。</summary>
    internal void Pair(FakeTransport peer) => _peer = peer;

    public void Send(byte[] payload) => _peer?.Receive(payload);

    public void Disconnect()
    {
        Disconnected?.Invoke();
        _peer?.ReceiveDisconnect();
    }

    private void Receive(byte[] payload) => Received?.Invoke(payload);

    private void ReceiveDisconnect() => Disconnected?.Invoke();
}
