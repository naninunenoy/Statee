using System;
using System.Diagnostics;
using System.Threading;
using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;
using Statee.Remote;
using ZLogger;

namespace PingTarget;

/// <summary>
/// フレームワーク検証用の最小ダミーターゲット(docs/MEMO.md D-013, D-018)。
/// StateeHost を組み込み、ping / state / logs / quit に応答する。
/// </summary>
public partial class Main : Node
{
    private const int DefaultPort = 9310;

    private long _frame;
    private string _engineVersion = "";
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;

    public override void _Ready()
    {
        // Godot API はメインスレッド以外から触れないため、ここで値を確定させておく
        _engineVersion = (string)Engine.GetVersionInfo()["string"];

        var buffer = new LogBuffer(1024);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new BufferLoggerProvider(buffer));
            builder.AddZLoggerConsole();
        });
        var logger = _loggerFactory.CreateLogger<Main>();

        var host = new StateeHost(buffer);
        host.RegisterStateProvider(new SystemStateProvider(this));
        host.RegisterCommand(
            "ping",
            args =>
            {
                var message = args.GetString("message") ?? "ping";
                logger.ZLogInformation($"ping を受信: {message}");
                return new
                {
                    Pong = true,
                    Message = message,
                    Frame = Interlocked.Read(ref _frame),
                };
            }
        );
        host.RegisterCommand(
            "quit",
            _ =>
            {
                logger.ZLogInformation($"quit を受信。終了する");
                Callable.From(() => GetTree().Quit()).CallDeferred();
                return new { Quitting = true };
            }
        );

        _server = new StateeTcpServer(host, ParsePort());
        _server.Start();
        logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    public override void _Process(double delta) => Interlocked.Increment(ref _frame);

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    private static int ParsePort()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (
                arg.StartsWith("--port=", StringComparison.Ordinal)
                && int.TryParse(arg["--port=".Length..], out var port)
            )
            {
                return port;
            }
        }

        return DefaultPort;
    }

    /// <summary>system パスの State。ソケットスレッドから呼ばれるため Godot API には触れない。</summary>
    private sealed class SystemStateProvider(Main main) : IStateProvider
    {
        public string Path => "system";

        public object CaptureState() =>
            new
            {
                Frame = Interlocked.Read(ref main._frame),
                UptimeSeconds = Math.Round(main._uptime.Elapsed.TotalSeconds, 3),
                Engine = $"Godot {main._engineVersion}",
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            };
    }
}
