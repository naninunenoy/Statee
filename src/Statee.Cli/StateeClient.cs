namespace Statee.Cli;

/// <summary>
/// Statee ターゲットへの TCP クライアント(1リクエスト1接続)。
/// StateeCommands(CLI)と Statee.Scenario(シナリオランナー)が共用する。
/// </summary>
public sealed class StateeClient(int port)
{
    private readonly int _port = port;

    /// <summary>コマンドを送り、成功時の payload(TOON)を返す。</summary>
    /// <exception cref="StateeClientException">エラー応答・接続失敗・プロトコル不整合。</exception>
    public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null) =>
        default!;
}
