using Statee.Core;

namespace Statee.Remote;

/// <summary>
/// localhost の TCP で待ち受け、改行区切りの1行 JSON リクエストを StateeHost に委譲する。
/// 1接続で複数リクエスト可。不正な JSON 行には error 応答を返し、接続は維持する(docs/MEMO.md D-018)。
/// </summary>
public sealed class StateeTcpServer : IAsyncDisposable
{
    public StateeTcpServer(StateeHost host, int port) { }

    /// <summary>実際に bind されたポート。port に 0 を指定した場合は OS が割り当てた値になる。</summary>
    public int Port => 0;

    public void Start() { }

    public ValueTask DisposeAsync() => default;
}
