using System.Net;
using System.Net.Sockets;
using System.Text;
using Statee.Core;

namespace Statee.Cli;

/// <summary>
/// Statee を組み込んだターゲット(ゲーム)へ TCP でコマンドを送る汎用 CLI(docs/MEMO.md D-018)。
/// 成功時は payload(TOON)を stdout に出力し exit 0、失敗時は stderr に理由を出力し exit 1。
/// </summary>
public class StateeCommands
{
    private const int DefaultPort = 9310;

    /// <summary>ping を送り、エコー応答を確認する。</summary>
    /// <param name="message">エコーさせるメッセージ。</param>
    /// <param name="port">接続先ポート。</param>
    public int Ping(string message = "ping", int port = DefaultPort) =>
        SendRequest("ping", new Dictionary<string, string> { ["message"] = message }, port);

    /// <summary>State スナップショットを取得する。</summary>
    /// <param name="path">State のパス(例: system)。</param>
    /// <param name="port">接続先ポート。</param>
    public int State(string path = "system", int port = DefaultPort) =>
        SendRequest("state", new Dictionary<string, string> { ["path"] = path }, port);

    /// <summary>ターゲットが保持するログを取得する。</summary>
    /// <param name="tail">新しい方から何件取得するか。</param>
    /// <param name="port">接続先ポート。</param>
    public int Logs(int tail = 50, int port = DefaultPort) =>
        SendRequest("logs", new Dictionary<string, string> { ["tail"] = tail.ToString() }, port);

    /// <summary>任意のコマンドを送る。ターゲット固有コマンドの呼び出しに使う。</summary>
    /// <param name="command">コマンド名。</param>
    /// <param name="arg">key=value 形式の引数(複数指定可)。</param>
    /// <param name="port">接続先ポート。</param>
    public int Send(string command, string[]? arg = null, int port = DefaultPort)
    {
        Dictionary<string, string>? args = null;
        foreach (var pair in arg ?? [])
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                Console.Error.WriteLine($"引数は key=value 形式で指定する: {pair}");
                return 1;
            }

            args ??= [];
            args[pair[..separator]] = pair[(separator + 1)..];
        }

        return SendRequest(command, args, port);
    }

    /// <summary>ターゲットを終了させる。</summary>
    /// <param name="port">接続先ポート。</param>
    public int Quit(int port = DefaultPort) => SendRequest("quit", null, port);

    private static int SendRequest(string command, Dictionary<string, string>? args, int port)
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
            writer.WriteLine(StateeJson.Serialize(new StateeRequest(id, command, args)));
            var line = reader.ReadLine();
            if (line is null)
            {
                Console.Error.WriteLine("応答が無いまま切断された");
                return 1;
            }

            var response = StateeJson.DeserializeResponse(line);
            if (response is null)
            {
                Console.Error.WriteLine($"応答を JSON として解釈できない: {line}");
                return 1;
            }

            if (response.Status != StateeResponse.StatusOk)
            {
                Console.Error.WriteLine($"error: {response.Error}");
                return 1;
            }

            Console.WriteLine(response.Payload);
            return 0;
        }
        catch (SocketException e)
        {
            Console.Error.WriteLine($"接続できない (port={port}): {e.Message}");
            return 1;
        }
    }
}
