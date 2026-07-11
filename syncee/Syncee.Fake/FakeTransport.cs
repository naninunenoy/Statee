namespace Syncee.Fake;

/// <summary>
/// インプロセスの <see cref="ITransport"/> 実装(D-050)。ペアになったもう一方の
/// <see cref="FakeTransport"/> に対して同期的に(呼び出しスタック上で即座に)配送する。
/// テストの主戦場(freeze / step / 切断注入が全部効く)として使う。
/// </summary>
public sealed class FakeTransport : ITransport
{
    public event Action<byte[]>? Received
    {
        add { }
        remove { }
    }

    public event Action? Disconnected
    {
        add { }
        remove { }
    }

    public void Send(byte[] payload) { }

    public void Disconnect() { }
}
