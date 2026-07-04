using System.Net;
using System.Net.Sockets;
using System.Text;
using Statee.Core;

namespace Statee.Cli;

/// <summary>
/// Statee ターゲットへの TCP クライアント(1リクエスト1接続)。
/// StateeCommands(CLI)と Statee.Scenario(シナリオランナー)が共用する。
/// </summary>
public sealed class StateeClient(int port)
{
    /// <summary>コマンドを送り、成功時の payload(TOON)を返す。</summary>
    /// <exception cref="StateeClientException">エラー応答・接続失敗・プロトコル不整合。</exception>
    public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false))
            {
                AutoFlush = true,
            };

            var id = Guid.NewGuid().ToString("N")[..8];
            var requestLine = StateeJson.Serialize(
                new StateeRequest(
                    id,
                    command,
                    args is null ? null : new Dictionary<string, string>(args)
                )
            );
            WireTrace.Write("→", requestLine);
            writer.WriteLine(requestLine);
            var line = reader.ReadLine();
            if (line is null)
            {
                WireTrace.Write("×", "応答が無いまま切断された");
                throw new StateeClientException("応答が無いまま切断された");
            }

            WireTrace.Write("←", line);

            var response = StateeJson.DeserializeResponse(line);
            if (response is null)
            {
                throw new StateeClientException($"応答を JSON として解釈できない: {line}");
            }

            if (response.Status != StateeResponse.StatusOk)
            {
                throw new StateeClientException(
                    response.Error ?? "不明なエラー",
                    isErrorResponse: true
                );
            }

            return response.Payload ?? "";
        }
        catch (SocketException e)
        {
            WireTrace.Write("×", $"接続できない (port={port}): {e.Message}");
            throw new StateeClientException($"接続できない (port={port}): {e.Message}");
        }
    }
}
