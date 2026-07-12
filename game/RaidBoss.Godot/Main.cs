using System;
using System.Collections.Generic;
using System.Linq;
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

    // ロビーUI(D-056)。参加/部屋を立てるの選択→合言葉入力→待機、を画面上のボタンで操作できるようにする。
    // ゲームが始まる(_logic.Phase が Waiting でなくなる)と自動的に隠れる
    private Control? _lobbyRoot;
    private Label? _lobbyStatusLabel;

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

        BuildLobbyUi();
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
        if (_lobbyRoot is not null)
        {
            _lobbyRoot.Visible = _logic.Phase == GamePhase.Waiting;
            if (_lobbyStatusLabel is not null && _networkMode)
            {
                _lobbyStatusLabel.Text = "部屋で待機中...(参加人数が揃ったら開始を押してください)";
            }
        }
    }

    /// <summary>
    /// 参加/部屋を立てるの選択・合言葉入力・開始ボタンを持つロビーUI(D-056)。
    /// UI はコード(C#)で構築する(D-033)。ゲームが始まると自動的に隠れる。
    /// </summary>
    private void BuildLobbyUi()
    {
        var root = new VBoxContainer { Name = "LobbyRoot" };
        root.SetAnchorsPreset(Control.LayoutPreset.Center);
        _lobbyRoot = root;

        var roomInput = new LineEdit
        {
            Name = "RoomKeywordInput",
            Text = DefaultRoom,
            PlaceholderText = "合言葉",
        };

        var statusLabel = new Label
        {
            Name = "LobbyStatusLabel",
            Text = "参加するか、部屋を立ててください",
        };
        _lobbyStatusLabel = statusLabel;

        var createButton = new Button { Name = "CreateRoomButton", Text = "部屋を立てる" };
        createButton.Pressed += () => ConnectAndEnterRoom("create", roomInput.Text);

        var joinButton = new Button { Name = "JoinRoomButton", Text = "参加する" };
        joinButton.Pressed += () => ConnectAndEnterRoom("join", roomInput.Text);

        var startButton = new Button { Name = "StartRoomButton", Text = "開始" };
        startButton.Pressed += StartRoom;

        root.AddChild(statusLabel);
        root.AddChild(roomInput);
        root.AddChild(createButton);
        root.AddChild(joinButton);
        root.AddChild(startButton);
        AddChild(root);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        KeyBindingTable.TryHandle(_keyBindings, @event);
    }

    private static readonly Vector2 BossPosition = new(400, 150);
    private const float BossRadius = 60f;
    private const float PlayerRadius = 24f;
    private const float ProjectileRadius = 6f;

    public override void _Draw()
    {
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 32),
            $"RaidBoss  tick={_logic.TickCount}  {_logic.Phase}",
            fontSize: 20
        );

        var bossHpFraction = Math.Clamp((float)_logic.BossHp / GameLogic.BossMaxHp, 0f, 1f);
        DrawCircle(
            BossPosition,
            BossRadius,
            new Color(1f, 1f - bossHpFraction, 1f - bossHpFraction)
        );
        DrawString(
            ThemeDB.FallbackFont,
            BossPosition + new Vector2(-BossRadius, -BossRadius - 10),
            $"Boss HP: {_logic.BossHp}",
            fontSize: 16
        );

        var playerPositions = GetPlayerPositions(Math.Max(_logic.PlayerCount, 1));
        for (var i = 0; i < _logic.PlayerCount; i++)
        {
            var incapacitated = _logic.IncapacitatedTicks[i] > 0;
            var color = incapacitated ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.3f, 0.5f, 1f);
            DrawCircle(playerPositions[i], PlayerRadius, color);
            DrawString(
                ThemeDB.FallbackFont,
                playerPositions[i] + new Vector2(-PlayerRadius, PlayerRadius + 16),
                $"P{i + 1} HP:{_logic.PlayerHps[i]}",
                fontSize: 14
            );
        }

        foreach (var projectile in _logic.Projectiles)
        {
            var from = playerPositions[projectile.OwnerIndex];
            var progress = 1f - (float)projectile.TicksRemaining / GameLogic.ProjectileTravelTicks;
            var position = from.Lerp(BossPosition, Math.Clamp(progress, 0f, 1f));
            DrawCircle(position, ProjectileRadius, new Color(1f, 1f, 0.2f));
        }
    }

    /// <summary>プレイヤーを画面下部に等間隔で並べた座標を返す(描画専用。ロジックは持たない)。</summary>
    private static Vector2[] GetPlayerPositions(int playerCount)
    {
        const float y = 450f;
        const float spacing = 150f;
        var startX = 400f - (spacing * (playerCount - 1) / 2f);
        return Enumerable
            .Range(0, playerCount)
            .Select(i => new Vector2(startX + spacing * i, y))
            .ToArray();
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
        if (_logic.Phase == GamePhase.Waiting)
        {
            _logic.Start(playerCount: 2);
        }
        _logic.Step([player1Action, player2Action]);
        RefreshView();
    }

    /// <summary>アクション後の状態を State と描画へ反映する。</summary>
    private void RefreshView()
    {
        _state.Update(
            _logic.Seed,
            _logic.TickCount,
            _logic.BossHp,
            _logic.PlayerHps,
            _logic.IncapacitatedTicks,
            _logic.Projectiles,
            _logic.Phase
        );
        QueueRedraw();
    }

    /// <summary>
    /// RaidBoss.Server へ接続し、最初の1通で部屋を作る/参加する(まだ未接続の場合のみ)。
    /// 以後の入力はサーバ送信になる(D-056。接続ゲートは持たず、この最初のコマンドで
    /// 合言葉ごとの部屋へ振り分けられる)。
    /// </summary>
    private void ConnectAndEnterRoom(string command, string room)
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
            _replicaLog!.OnReceived(SyncWire.DeserializeTickBundle(bytes));
        };
        _network.Connected += () =>
        {
            _network!.Send(
                SyncWire.Serialize(
                    new CommandRequest(command, new Dictionary<string, string> { ["room"] = room })
                )
            );
            _logger.ZLogInformation($"{command}(room={room}) をサーバへ送信");
        };
        var host = CmdlineArgs.ParseString("--game-host=", DefaultGameHost);
        var port = CmdlineArgs.ParseInt("--game-port=", DefaultGamePort);
        _network.Connect(host, port);
        _logger.ZLogInformation($"RaidBoss.Server へ接続 host={host} port={port}");
    }

    /// <summary>部屋作成者が参加人数の確定・開始を要求する(ロビーで人数が揃ったあと)。</summary>
    private void StartRoom()
    {
        _network!.Send(SyncWire.Serialize(new CommandRequest("start", null)));
        _logger.ZLogInformation($"start をサーバへ送信");
    }

    /// <summary>サーバから確定した1Tick分の入力バンドルを適用する(RaidBoss.Server の Committed ハンドラと同型)。</summary>
    private void ApplyBundle(TickBundle bundle)
    {
        if (_logic.Phase == GamePhase.Waiting)
        {
            _logic.Start(bundle.InputsByClient.Count);
        }
        var actions = Enumerable
            .Range(1, _logic.PlayerCount)
            .Select(n =>
                ParseAction(
                    bundle
                        .InputsByClient.GetValueOrDefault($"client-{n}")
                        ?.GetValueOrDefault("action")
                )
            )
            .ToArray();
        _logic.Step(actions);
        RefreshView();
        _logger.ZLogInformation(
            $"確定 tick={bundle.Tick} → boss={_logic.BossHp} players={string.Join(",", _logic.PlayerHps)} phase={_logic.Phase}"
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
            "create",
            args =>
            {
                ConnectAndEnterRoom("create", args.GetString("room") ?? DefaultRoom);
                return new { Created = true };
            }
        );
        host.RegisterMainThreadCommand(
            "join",
            args =>
            {
                ConnectAndEnterRoom("join", args.GetString("room") ?? DefaultRoom);
                return new { Joined = true };
            }
        );
        host.RegisterMainThreadCommand(
            "start",
            _ =>
            {
                StartRoom();
                return new { Started = true };
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
                    PlayerHps = string.Join(",", _logic.PlayerHps),
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
