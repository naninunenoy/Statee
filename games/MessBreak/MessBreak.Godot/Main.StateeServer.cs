using System;
using Godot;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using ZLogger;

namespace MessBreak;

/// <summary>
/// Statee の TCP 待ち受け(外部 CLI/MCP の入口)。本番ビルド(ExportRelease)では
/// csproj の条件でこのファイルと Statee.Remote 参照ごと除外される(D-065)。
/// </summary>
public partial class Main
{
    private StateeTcpServer? _server;

    partial void StartStateeServer(StateeHost host)
    {
        _server = new StateeTcpServer(host, CmdlineArgs.ParseInt("--port=", DefaultPort));
        try
        {
            _server.Start();
        }
        catch (System.Net.Sockets.SocketException e)
        {
            // ポートを取れないまま動き続けると、CLI が別プロセス(古いバイナリ等)に繋がる
            // 事故に気づけない。検証用途では「静かに劣化」より「即死」が正しい(D-075)
            GD.PushError($"Statee ポート {DefaultPort} を確保できないため終了する: {e.Message}");
            GetTree().Quit(1);
            return;
        }
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    partial void StopStateeServer()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
    }
}
