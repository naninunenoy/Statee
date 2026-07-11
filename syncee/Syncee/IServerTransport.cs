namespace Syncee;

/// <summary>
/// クライアント接続の受け入れ口(D-050)。実装は「新しい接続が来たら
/// <see cref="ClientConnected"/> で <see cref="ITransport"/> を1本渡す」だけを保証する。
/// </summary>
public interface IServerTransport
{
    event Action<ITransport>? ClientConnected;
}
