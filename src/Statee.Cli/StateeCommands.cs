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
    /// <param name="path">State のパス(例: system/runtime, system/platform)。</param>
    /// <param name="port">接続先ポート。</param>
    public int State(string path = "system/runtime", int port = DefaultPort) =>
        SendRequest("state", new Dictionary<string, string> { ["path"] = path }, port);

    /// <summary>ターゲットが保持するログを取得する。</summary>
    /// <param name="tail">新しい方から何件取得するか。</param>
    /// <param name="port">接続先ポート。</param>
    public int Logs(int tail = 50, int port = DefaultPort) =>
        SendRequest("logs", new Dictionary<string, string> { ["tail"] = tail.ToString() }, port);

    /// <summary>任意のコマンドを送る。ターゲット固有コマンドの呼び出しに使う。</summary>
    /// <param name="command">コマンド名。</param>
    /// <param name="arg">key=value 形式の引数(複数はカンマ区切り)。</param>
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
            Console.WriteLine(new StateeClient(port).Invoke(command, args));
            return 0;
        }
        catch (StateeClientException e)
        {
            Console.Error.WriteLine(e.IsErrorResponse ? $"error: {e.Message}" : e.Message);
            return 1;
        }
    }
}
