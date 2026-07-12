using System;
using System.Collections.Generic;
using Godot;
using Microsoft.Extensions.Logging;
using RaidBoss.Logic;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using Syncee;
using Syncee.LiteNetLib;
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
    private const string DefaultGameHost = "127.0.0.1";
    private const int DefaultGamePort = 9412;
    private const string DefaultRoom = "raidboss";

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();

    private GameLogic _logic = null!;
    private KeyBinding[] _keyBindings = [];
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    // ネット対戦(D-054のロックステップ)。接続すると _networkMode が true になり、
    // 攻撃/待機はローカル即時適用でなくサーバへの送信 + 確定Tickバンドル受信での適用に切り替わる。
    // 各クライアントは自分の入力に確定を待たず単調増加するTick番号を採番して送信する
    // (これが D-054 の「入力遅延バッファ」に相当する。確認応答を待たずに先行送信できる)
    private LiteNetLibClientTransport? _network;
    private TickReplicaLog? _replicaLog;
    private bool _networkMode;
    private int _nextSendTick;

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
                "プレイヤー1がボスを攻撃(ネット対戦では自分の攻撃)",
                () => OnAttackPressed(PlayerAction.Attack, PlayerAction.Idle)
            ),
            new KeyBinding(
                Key.W,
                "attack2",
                "プレイヤー2がボスを攻撃(ローカル対戦専用)",
                () => OnAttackPressed(PlayerAction.Idle, PlayerAction.Attack)
            ),
            new KeyBinding(
                Key.Space,
                "wait",
                "何もせず1Tick進める",
                () => OnAttackPressed(PlayerAction.Idle, PlayerAction.Idle)
            ),
        ];

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"RaidBoss 起動 seed={_logic.Seed}");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        _network?.PollEvents();
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
        _network?.Dispose();
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    /// <summary>
    /// キー入力・step コマンドからの共通入口。ネット対戦中はサーバへの送信に振り替え、
    /// ローカル対戦中は両プレイヤー分の行動を1Tick分同時に適用する。
    /// </summary>
    private void OnAttackPressed(PlayerAction player1Action, PlayerAction player2Action)
    {
        if (_networkMode)
        {
            var thisClientAction =
                player1Action == PlayerAction.Attack || player2Action == PlayerAction.Attack
                    ? PlayerAction.Attack
                    : PlayerAction.Idle;
            SendNetworkInput(thisClientAction);
            return;
        }
        ActStep(player1Action, player2Action);
    }

    /// <summary>両プレイヤーの行動を1Tick分同時に適用する(ローカル対戦専用)。</summary>
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

    /// <summary>RaidBoss.Server へ接続する(まだ未接続の場合のみ)。以後の入力はサーバ送信になる。</summary>
    private void ConnectNetwork(string room)
    {
        if (_network is not null)
        {
            return;
        }
        _networkMode = true;
        _replicaLog = new TickReplicaLog(ApplyBundle);
        _network = new LiteNetLibClientTransport();
        _network.Received += bytes =>
        {
            _replicaLog.OnReceived(SyncWire.DeserializeTickBundle(bytes));
        };
        var host = CmdlineArgs.ParseString("--game-host=", DefaultGameHost);
        var port = CmdlineArgs.ParseInt("--game-port=", DefaultGamePort);
        _network.Connect(host, port, room);
        _logger.ZLogInformation($"RaidBoss.Server へ接続 host={host} port={port} room={room}");
    }

    /// <summary>サーバから確定した1Tick分の入力バンドルを適用する(RaidBoss.Server の Committed ハンドラと同型)。</summary>
    private void ApplyBundle(TickBundle bundle)
    {
        var player1Action = ParseAction(
            bundle.InputsByClient.GetValueOrDefault("client-1")?.GetValueOrDefault("action")
        );
        var player2Action = ParseAction(
            bundle.InputsByClient.GetValueOrDefault("client-2")?.GetValueOrDefault("action")
        );
        _logic.Step(player1Action, player2Action);
        RefreshView();
        _logger.ZLogInformation(
            $"確定 tick={bundle.Tick} → boss={_logic.BossHp} p1={_logic.Player1Hp} p2={_logic.Player2Hp} phase={_logic.Phase}"
        );
    }

    /// <summary>
    /// このクライアントの行動をサーバへ送信する。確定を待たず単調増加するTick番号を
    /// 採番するため、複数手を確認応答なしに先行送信できる(D-054)。
    /// </summary>
    private void SendNetworkInput(PlayerAction action)
    {
        var tick = _nextSendTick++;
        _network!.Send(
            SyncWire.Serialize(
                new CommandRequest(
                    "input",
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["tick"] = tick.ToString(),
                        ["action"] = action == PlayerAction.Attack ? "attack" : "idle",
                    }
                )
            )
        );
        _logger.ZLogInformation($"input(tick={tick}, action={action}) をサーバへ送信");
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
            "connect",
            args =>
            {
                ConnectNetwork(args.GetString("room") ?? DefaultRoom);
                return new { Connected = true };
            }
        );
        host.RegisterMainThreadCommand(
            "step",
            args =>
            {
                if (_networkMode)
                {
                    var thisClientAction = ParseAction(args.GetString("action"));
                    SendNetworkInput(thisClientAction);
                    return new { Sent = thisClientAction.ToString() };
                }
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
