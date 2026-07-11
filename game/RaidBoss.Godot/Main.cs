using System;
using Godot;
using Microsoft.Extensions.Logging;
using RaidBoss.Logic;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using ZLogger;

namespace RaidBoss;

/// <summary>
/// RaidBoss の Godot 層エントリポイント。描画・入力・Statee 配線だけを担い、
/// ゲームルールはすべて RaidBoss.Logic に置く(docs/USING.md「境界の掟」)。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9311;
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
            new KeyBinding(
                Key.Q,
                "attack1",
                "プレイヤー1がボスを攻撃",
                () => ActStep(PlayerAction.Attack, PlayerAction.Idle)
            ),
            new KeyBinding(
                Key.W,
                "attack2",
                "プレイヤー2がボスを攻撃",
                () => ActStep(PlayerAction.Idle, PlayerAction.Attack)
            ),
            new KeyBinding(
                Key.Space,
                "wait",
                "何もせず1Tick進める",
                () => ActStep(PlayerAction.Idle, PlayerAction.Idle)
            ),
        ];

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"RaidBoss 起動 seed={_logic.Seed}");
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
            $"RaidBoss  boss={_logic.BossHp}  p1={_logic.Player1Hp}  p2={_logic.Player2Hp}  tick={_logic.TickCount}  {_logic.Phase}",
            fontSize: 20
        );
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    /// <summary>両プレイヤーの行動を1Tick分同時に適用する。</summary>
    private void ActStep(PlayerAction player1Action, PlayerAction player2Action)
    {
        _logic.Step(player1Action, player2Action);
        RefreshView();
    }

    /// <summary>アクション後の状態を State と描画へ反映する。</summary>
    private void RefreshView()
    {
        _state.Update(
            _logic.Seed,
            _logic.TickCount,
            _logic.BossHp,
            _logic.Player1Hp,
            _logic.Player2Hp,
            _logic.Phase
        );
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
            args =>
            {
                var player1Action = ParseAction(args.GetString("player1"));
                var player2Action = ParseAction(args.GetString("player2"));
                ActStep(player1Action, player2Action);
                _logger.ZLogInformation(
                    $"step({player1Action}, {player2Action}) → tick={_logic.TickCount}"
                );
                return new
                {
                    _logic.TickCount,
                    _logic.BossHp,
                    _logic.Player1Hp,
                    _logic.Player2Hp,
                    Phase = _logic.Phase.ToString(),
                };
            }
        );
        _server = new StateeTcpServer(host, CmdlineArgs.ParseInt("--port=", DefaultPort));
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    private static PlayerAction ParseAction(string? name) =>
        name?.Equals("attack", StringComparison.OrdinalIgnoreCase) == true
            ? PlayerAction.Attack
            : PlayerAction.Idle;
}
