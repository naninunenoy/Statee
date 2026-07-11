using System;
using Godot;
using Microsoft.Extensions.Logging;
using Reversi.Logic;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using ZLogger;

namespace Reversi;

/// <summary>
/// Reversi の Godot 層エントリポイント。描画・入力・Statee 配線だけを担い、
/// ゲームルールはすべて Reversi.Logic に置く(docs/USING.md「境界の掟」)。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();

    private GameLogic _logic = null!;
    private KeyBinding[] _keyBindings = [];
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = StateeLogging.CreateLoggerFactory(buffer);
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new GameLogic(CmdlineArgs.ParseInt("--seed=", DefaultSeed));
        _keyBindings =
        [
            new KeyBinding(Key.Space, "step", "1ターン進める(プレースホルダ)", ActStep),
        ];

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"Reversi 起動 seed={_logic.Seed}");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        if (!_time.IsFrozen)
        {
            _time.OnFrame();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        KeyBindingTable.TryHandle(_keyBindings, @event);
    }

    public override void _Draw()
    {
        // プレースホルダ描画。実ゲームの描画に置き換える
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 32),
            $"Reversi  seed={_logic.Seed}  step={_logic.StepCount}",
            fontSize: 20
        );
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    /// <summary>プレイヤーのアクション(プレースホルダ)。</summary>
    private void ActStep()
    {
        _logic.Step();
        RefreshView();
    }

    /// <summary>アクション後の状態を State と描画へ反映する。</summary>
    private void RefreshView()
    {
        _state.Update(_logic.Seed, _logic.StepCount);
        QueueRedraw();
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        host.RegisterStateProvider(KeyBindingTable.CreateInputStateProvider(_keyBindings));
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        // ゲーム状態を変えるコマンドはメインスレッドで実行する
        host.RegisterMainThreadCommand(
            "step",
            _ =>
            {
                ActStep();
                _logger.ZLogInformation($"step → {_logic.StepCount}");
                return new { StepCount = _logic.StepCount };
            }
        );
        _server = new StateeTcpServer(host, CmdlineArgs.ParseInt("--port=", DefaultPort));
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }
}
