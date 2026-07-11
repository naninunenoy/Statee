namespace Syncee.Fake;

/// <summary>
/// インプロセスの <see cref="IServerTransport"/> 実装(D-050)。
/// <see cref="Connect"/> を呼ぶたびに新しいペアの <see cref="FakeTransport"/> を作り、
/// サーバ側を <see cref="ClientConnected"/> で通知、クライアント側を戻り値で返す。
/// </summary>
public sealed class FakeServerTransport : IServerTransport
{
    public event Action<ITransport>? ClientConnected
    {
        add { }
        remove { }
    }

    /// <summary>新しいクライアント接続を作る。戻り値はクライアント側が使う <see cref="ITransport"/>。</summary>
    public ITransport Connect() => new FakeTransport();
}
