using System;
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

    private readonly RuntimeState _runtime = new();
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;

    public override void _Ready()
    {
        var buffer = new LogBuffer(1024);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new BufferLoggerProvider(buffer));
            builder.AddZLoggerConsole();
        });
        var logger = _loggerFactory.CreateLogger<Main>();

        var port = ParsePort();
        var host = new StateeHost(buffer);

        // 起動時に確定する不変情報 (D-019)。Godot API はメインスレッド以外から触れないため、
        // ここで一度だけスナップショットを構築し、ソケットスレッドからは完成品を返すだけにする
        var platform = new
        {
            Engine = $"Godot {(string)Engine.GetVersionInfo()["string"]}",
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Os = $"{OS.GetName()} {OS.GetVersion()}",
            Headless = DisplayServer.GetName() == "headless",
            ProcessorCount = System.Environment.ProcessorCount,
            Pid = System.Environment.ProcessId,
            Port = port,
            StartedAt = DateTimeOffset.Now,
        };
        host.RegisterStateProvider(new SnapshotStateProvider("system/platform", () => platform));

        // 可変情報 (D-019)。[StateeState] からジェネレータが生成した実装を登録する (D-022)
        host.RegisterStateProvider(_runtime);
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
                    Frame = _runtime.Frame,
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

        _server = new StateeTcpServer(host, port);
        _server.Start();
        logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    public override void _Process(double delta) => _runtime.IncrementFrame();

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

    /// <summary>デリゲートでスナップショットを返す State プロバイダ。ソケットスレッドから呼ばれる。</summary>
    private sealed class SnapshotStateProvider(string path, Func<object> capture) : IStateProvider
    {
        public string Path => path;

        public object CaptureState() => capture();
    }
}
