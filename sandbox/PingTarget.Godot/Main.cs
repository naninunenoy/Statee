using System;
using Declaree;
using Declaree.Godot;
using Declaree.Statee;
using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;
using Statee.Remote;
using ZLogger;
using Button = Declaree.Button;
using Label = Declaree.Label;

namespace PingTarget;

/// <summary>
/// フレームワーク検証用の最小ダミーターゲット(docs/adr/D-013.md, D-018)。
/// StateeHost を組み込み、ping / state / logs / quit に応答する。
/// </summary>
public partial class Main : Node
{
    private const int DefaultPort = 9310;

    private readonly RuntimeState _runtime = new();
    private readonly MainThreadDispatcher _dispatcher = new();
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;

    // Declaree 検証用の最小 UI(D-035)。カウンタとボタンだけを宣言的に持つ。
    // _uiSnapshot はレイアウト確定後の Rect 込み記述子。メインスレッドが毎フレーム
    // 差し替え、ソケットスレッド(CaptureState)は読むだけ(SuikaGame UiState と同型)
    private int _count;
    private UiNode _uiTree = BuildUi(0);
    private Control? _uiRoot;
    private volatile UiDescriptor _uiSnapshot = UiTree.Describe(BuildUi(0));

    public override void _Ready()
    {
        // headless では project.godot の window サイズが反映されず 64x64 になり、
        // UI が画面外に出るとクリックのヒットテストが外れる。実行時に明示する
        GetWindow().Size = new Vector2I(640, 360);
        var buffer = new LogBuffer(1024);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new BufferLoggerProvider(buffer));
            builder.AddZLoggerConsole();
        });
        var logger = _loggerFactory.CreateLogger<Main>();

        var port = ParsePort();
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };

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
        // メインスレッドディスパッチの実証。ハンドラ内から Godot API を直接触れる
        host.RegisterMainThreadCommand(
            "mainthread",
            _ => new
            {
                CallerThreadId = (long)OS.GetThreadCallerId(),
                MainThreadId = (long)OS.GetMainThreadId(),
            }
        );
        // Declaree の UI(幾何 Rect 込みスナップショット)を State として公開する(D-035)
        host.RegisterStateProvider(new UiStateProvider("ui/tree", () => _uiSnapshot));
        // 実際の入力経路で左クリックを再現する(GUIDELINE 3.2)
        host.RegisterMainThreadCommand(
            "click",
            args =>
            {
                var position = new Vector2(
                    float.Parse(
                        args.GetString("x")
                            ?? throw new InvalidOperationException("x を指定すること"),
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    float.Parse(
                        args.GetString("y")
                            ?? throw new InvalidOperationException("y を指定すること"),
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                );
                PushClick(position);
                logger.ZLogInformation($"click x={position.X} y={position.Y}");
                return new { X = position.X, Y = position.Y };
            }
        );
        host.RegisterMainThreadCommand(
            "quit",
            _ =>
            {
                logger.ZLogInformation($"quit を受信。終了する");
                GetTree().Quit();
                return new { Quitting = true };
            }
        );

        RebuildUi();

        _server = new StateeTcpServer(host, port);
        _server.Start();
        logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    /// <summary>状態(カウンタ)から UI ツリーを導出する純関数。
    /// Margin / MinWidth / Disabled は語彙拡張の E2E 検証を兼ねる。</summary>
    private static UiNode BuildUi(int count) =>
        new Margin(
            16,
            new VBox(
                new Label($"Count: {count}"),
                new Button("Increment", OnClick: "ui/increment") { MinWidth = 120 },
                new Button("Locked", OnClick: "ui/increment") { Disabled = true }
            )
        );

    /// <summary>全破棄・全再構築(D-035)。UI イベントで呼ばれるためメインスレッド前提。</summary>
    private void RebuildUi()
    {
        _uiRoot?.QueueFree();
        _uiRoot = UiRenderer.Render(_uiTree, Dispatch);
        AddChild(_uiRoot);
    }

    private void Dispatch(string eventId)
    {
        if (eventId == "ui/increment")
        {
            _count++;
            _uiTree = BuildUi(_count);
            RebuildUi();
        }
    }

    /// <summary>実際の入力経路で左クリックを再現する(GUIDELINE 3.2, SuikaGame と同型)。</summary>
    private void PushClick(Vector2 position)
    {
        var viewport = GetViewport();
        viewport.PushInput(
            new InputEventMouseMotion { Position = position, GlobalPosition = position }
        );
        viewport.PushInput(
            new InputEventMouseButton
            {
                Position = position,
                GlobalPosition = position,
                ButtonIndex = MouseButton.Left,
                Pressed = true,
            }
        );
        viewport.PushInput(
            new InputEventMouseButton
            {
                Position = position,
                GlobalPosition = position,
                ButtonIndex = MouseButton.Left,
                Pressed = false,
            }
        );
    }

    public override void _Process(double delta)
    {
        _runtime.IncrementFrame();
        _dispatcher.Pump();
        if (_uiRoot is not null)
        {
            _uiSnapshot = UiSnapshot.Capture(_uiTree, _uiRoot);
        }
    }

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
