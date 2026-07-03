using System.Net;
using System.Net.Sockets;
using System.Text;
using Statee.Core;

namespace Statee.Remote;

/// <summary>
/// localhost の TCP で待ち受け、改行区切りの1行 JSON リクエストを StateeHost に委譲する。
/// 1接続で複数リクエスト可。不正な JSON 行には error 応答を返し、接続は維持する(docs/MEMO.md D-018)。
/// </summary>
public sealed class StateeTcpServer(StateeHost host, int port) : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, port);
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;

    /// <summary>実際に bind されたポート。port に 0 を指定した場合は OS が割り当てた値になる。</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        _acceptLoop = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 停止要求。正常終了
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                var request = StateeJson.DeserializeRequest(line);
                var response = request is null
                    ? StateeResponse.Fail("?", "リクエストを JSON として解釈できない")
                    : host.HandleRequest(request);
                await writer.WriteLineAsync(StateeJson.Serialize(response).AsMemory(), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 停止要求。正常終了
        }
        catch (IOException)
        {
            // クライアント切断。接続単位の異常はサーバー全体に影響させない
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (SocketException)
            {
                // Stop によって Accept が中断された場合
            }
        }

        _cts.Dispose();
    }
}
