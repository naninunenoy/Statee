using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace Statee.Mcp;

/// <summary>
/// Statee CLI を起動するだけの汎用ツール(docs/adr/D-001.md, D-018)。
/// ゲーム依存の知識を持たないため、ゲーム側が変わっても MCP サーバーの再ビルドは不要。
/// </summary>
[McpServerToolType]
public static class StateeCliTool
{
    [McpServerTool(Name = "statee_cli")]
    [Description(
        "Statee CLI を実行して、Statee を組み込んだターゲット(ゲーム)を操作する。"
            + "args の例: [\"ping\",\"--message\",\"hello\"] / [\"state\",\"--path\",\"system\"] / "
            + "[\"logs\",\"--tail\",\"20\"] / [\"send\",\"--command\",\"任意コマンド\",\"--arg\",\"key=value\"] / [\"quit\"]。"
            + "成功時の出力は TOON 形式。"
    )]
    public static string Run([Description("Statee.Cli に渡す引数の配列")] string[] args)
    {
        var cliPath = Environment.GetEnvironmentVariable("STATEE_CLI");
        if (string.IsNullOrEmpty(cliPath))
        {
            return "error: 環境変数 STATEE_CLI に Statee.Cli 実行ファイルのパスを設定してください";
        }

        // 相対パス指定(カレントディレクトリ基準)を許容するため絶対化する
        var startInfo = new ProcessStartInfo(Path.GetFullPath(cliPath))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                return "error: CLI が 30 秒以内に終了しなかった";
            }

            var result = $"exit: {process.ExitCode}\n{stdout}";
            return stderr.Length > 0 ? $"{result}\nstderr: {stderr}" : result;
        }
        catch (Exception e)
        {
            return $"error: CLI を起動できない ({startInfo.FileName}): {e.Message}";
        }
    }
}
