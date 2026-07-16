using System;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using ZLogger;

namespace RaidBoss;

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
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    partial void StopStateeServer()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
    }
}
