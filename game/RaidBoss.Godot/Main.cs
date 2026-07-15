using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using RaidBoss.Logic;
using Statee.Core;
using Statee.Godot;
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
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    // ネット対戦(D-054のロックステップ)。接続すると _networkMode が true になり、
    // 攻撃/待機はローカル即時適用でなくサーバへの送信 + 確定Tickバンドル受信での適用に切り替わる。
    // 各クライアントは自分の入力に確定を待たず単調増加するTick番号を採番して送信する
    // (これが D-054 の「入力遅延バッファ」に相当する。確認応答を待たずに先行送信できる)
    private LiteNetLibClientTransport? _network;
    private TickReplicaLog? _replicaLog;
    private bool _networkMode;
    private bool _connectedToServer;
    private int _nextSendTick;
    private AcceptDialog? _errorDialog;

    // リアルタイム化(D-059)。入力を待たず一定間隔でTickを自動進行し、キー入力は
    // 「次のTickで実行する行動」として蓄えて自動Tick時に消費する。freeze中は自動Tickを
    // 止め、従来の step コマンドで決定論的に進められる(シナリオ検証用)
    private const float TickIntervalSeconds = 0.5f;
    private double _tickAccumulator;
    private PlayerAction _pendingPlayer1Action = PlayerAction.Idle;
    private PlayerAction _pendingPlayer2Action = PlayerAction.Idle;

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
                "プレイヤー1が攻撃(ネット対戦では自分の攻撃)",
                () => SetPendingPlayer1(PlayerAction.Attack)
            ),
            new KeyBinding(
                Key.A,
                "left1",
                "プレイヤー1が左のレーンへ移動(ネット対戦では自分)",
                () => SetPendingPlayer1(PlayerAction.MoveLeft)
            ),
            new KeyBinding(
                Key.D,
                "right1",
                "プレイヤー1が右のレーンへ移動(ネット対戦では自分)",
                () => SetPendingPlayer1(PlayerAction.MoveRight)
            ),
            new KeyBinding(
                Key.W,
                "attack2",
                "プレイヤー2が攻撃(ローカル対戦専用)",
                () => SetPendingPlayer2(PlayerAction.Attack)
            ),
            new KeyBinding(
                Key.Left,
                "left2",
                "プレイヤー2が左のレーンへ移動(ローカル対戦専用)",
                () => SetPendingPlayer2(PlayerAction.MoveLeft)
            ),
            new KeyBinding(
                Key.Right,
                "right2",
                "プレイヤー2が右のレーンへ移動(ローカル対戦専用)",
                () => SetPendingPlayer2(PlayerAction.MoveRight)
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
        AutoTick(delta);
    }

    /// <summary>
    /// リアルタイム化の自動Tick(D-059)。プレイ中は一定間隔で蓄えた行動を消費して
    /// Tickを進める(ネット対戦は自分の行動の自動送信。確定はサーバの入力揃い待ち)。
    /// freeze中は止まり、step コマンドによる手動進行だけになる。
    /// </summary>
    private void AutoTick(double delta)
    {
        if (_logic.Phase != GamePhase.Playing || _time.IsFrozen)
        {
            return;
        }
        // 弾・予告の補間描画のため、プレイ中は毎フレーム再描画する
        QueueRedraw();
        // フレームが長時間止まった後にTickが連発しないよう、繰り越しは1Tick分までにする
        _tickAccumulator = Math.Min(_tickAccumulator + delta, TickIntervalSeconds * 2);
        if (_tickAccumulator < TickIntervalSeconds)
        {
            return;
        }
        _tickAccumulator -= TickIntervalSeconds;
        if (_networkMode)
        {
            SendNetworkInput(_pendingPlayer1Action);
        }
        else
        {
            _logic.Step([_pendingPlayer1Action, _pendingPlayer2Action]);
            RefreshView();
        }
        _pendingPlayer1Action = PlayerAction.Idle;
        _pendingPlayer2Action = PlayerAction.Idle;
    }

    /// <summary>キー入力を「次のTickの行動」として蓄える。ローカルはWaitingなら2人対戦を即開始する。</summary>
    private void SetPendingPlayer1(PlayerAction action)
    {
        StartLocalIfWaiting();
        _pendingPlayer1Action = action;
    }

    /// <summary>プレイヤー2のキー入力(ローカル対戦専用)。</summary>
    private void SetPendingPlayer2(PlayerAction action)
    {
        StartLocalIfWaiting();
        _pendingPlayer2Action = action;
    }

    private void StartLocalIfWaiting()
    {
        if (_networkMode || _logic.Phase != GamePhase.Waiting)
        {
            return;
        }
        _logic.Start(playerCount: 2);
        RefreshView();
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

        // ボス攻撃の予告(D-059)。対象レーンを赤い帯で示し、着弾が近づくほど濃くする
        if (_logic.PendingBossAttackLane >= 0)
        {
            var closeness =
                1f - (float)_logic.PendingBossAttackTicks / (GameLogic.BossAttackWindupTicks + 1);
            DrawRect(
                new Rect2(_logic.PendingBossAttackLane * LaneWidth, 220, LaneWidth, 280),
                new Color(1f, 0.1f, 0.1f, 0.15f + 0.25f * closeness)
            );
        }

        for (var i = 0; i < _logic.PlayerCount; i++)
        {
            var position = LanePosition(_logic.PlayerLanes[i]);
            var incapacitated = _logic.IncapacitatedTicks[i] > 0;
            var color = incapacitated ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.3f, 0.5f, 1f);
            DrawCircle(position, PlayerRadius, color);
            DrawString(
                ThemeDB.FallbackFont,
                position + new Vector2(-PlayerRadius, PlayerRadius + 16),
                $"P{i + 1} HP:{_logic.PlayerHps[i]}",
                fontSize: 14
            );
        }

        // 弾はTick間も滑らかに見えるよう、自動Tickの経過割合(_tickAccumulator)で補間する
        var tickFraction = _time.IsFrozen ? 0f : (float)(_tickAccumulator / TickIntervalSeconds);
        foreach (var projectile in _logic.Projectiles)
        {
            var from = LanePosition(_logic.PlayerLanes[projectile.OwnerIndex]);
            var progress =
                (GameLogic.ProjectileTravelTicks - projectile.TicksRemaining + tickFraction)
                / GameLogic.ProjectileTravelTicks;
            var position = from.Lerp(BossPosition, Math.Clamp(progress, 0f, 1f));
            DrawCircle(position, ProjectileRadius, new Color(1f, 1f, 0.2f));
        }

        if (_logic.Phase == GamePhase.Playing)
        {
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(16, 540),
                "Q: 攻撃  A/D: 移動(赤い帯のレーンから逃げる)",
                fontSize: 14
            );
        }
    }

    /// <summary>レーン番号を画面座標へ変換する(描画専用。ロジックは持たない)。</summary>
    private static Vector2 LanePosition(int lane) => new((lane + 0.5f) * LaneWidth, 450f);

    private const float ViewWidth = 800f;
    private const float LaneWidth = ViewWidth / GameLogic.LaneCount;

    public override void _ExitTree()
    {
        _network?.Dispose();
        StopStateeServer();
        _loggerFactory?.Dispose();
    }

    /// <summary>両プレイヤーの行動を1Tick分同時に適用する(ローカル対戦の step コマンド専用)。</summary>
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
        _state.Update(_logic);
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
            _connectedToServer = true;
            _network!.Send(
                SyncWire.Serialize(
                    new CommandRequest(command, new Dictionary<string, string> { ["room"] = room })
                )
            );
            _logger.ZLogInformation($"{command}(room={room}) をサーバへ送信");
        };
        _network.Disconnected += OnNetworkDisconnected;
        var host = CmdlineArgs.ParseString("--game-host=", DefaultGameHost);
        var port = CmdlineArgs.ParseInt("--game-port=", DefaultGamePort);
        _network.Connect(host, port);
        _logger.ZLogInformation($"RaidBoss.Server へ接続 host={host} port={port}");
    }

    /// <summary>
    /// サーバとの切断(接続失敗を含む)を通知して、ロビーからやり直せる状態へ戻す。
    /// LiteNetLib のイベントは _Process の PollEvents から届く(=メインスレッド)が、
    /// トランスポート自身のイベント中に Dispose しないよう後始末は次フレームへ遅延する。
    /// </summary>
    private void OnNetworkDisconnected()
    {
        var message = _connectedToServer
            ? "サーバから切断されました(合言葉が違う/同名の部屋が既にある可能性があります)"
            : "サーバへ接続できませんでした。RaidBoss.Server が起動しているか確認してください";
        _logger.ZLogWarning($"{message}");
        ShowErrorDialog(message);
        Callable.From(ResetNetwork).CallDeferred();
    }

    /// <summary>ネット対戦の状態を破棄してロビー操作をやり直せるようにする。</summary>
    private void ResetNetwork()
    {
        _network?.Dispose();
        _network = null;
        _replicaLog = null;
        _networkMode = false;
        _connectedToServer = false;
        _nextSendTick = 0;
        // _Process は _networkMode 中しかラベルを触らないため、初期文言へ戻すのはここで行う
        if (_lobbyStatusLabel is not null)
        {
            _lobbyStatusLabel.Text = "参加するか、部屋を立ててください";
        }
    }

    /// <summary>エラーダイアログを表示する(初回に生成して使い回す)。</summary>
    private void ShowErrorDialog(string message)
    {
        if (_errorDialog is null)
        {
            _errorDialog = new AcceptDialog { Name = "ErrorDialog", Title = "接続エラー" };
            AddChild(_errorDialog);
        }
        _errorDialog.DialogText = message;
        _errorDialog.PopupCentered();
    }

    /// <summary>部屋作成者が参加人数の確定・開始を要求する(ロビーで人数が揃ったあと)。</summary>
    private void StartRoom()
    {
        if (_network is null)
        {
            if (_lobbyStatusLabel is not null)
            {
                _lobbyStatusLabel.Text = "先に部屋を立てるか、参加してください";
            }
            _logger.ZLogWarning($"未接続のため start を送信できない(先に create/join が必要)");
            return;
        }
        _network.Send(SyncWire.Serialize(new CommandRequest("start", null)));
        _logger.ZLogInformation($"start をサーバへ送信");
    }

    /// <summary>サーバから確定した1Tick分の入力バンドルを適用する(RaidBoss.Server の Committed ハンドラと同型)。</summary>
    private void ApplyBundle(TickBundle bundle)
    {
        if (_logic.Phase == GamePhase.Waiting)
        {
            _logic.Start(bundle.InputsByClient.Count);
        }
        // 負Tickはサーバの開始通知(RaidBossAuthority.StartNotificationTick)。Step はしない
        if (bundle.Tick < 0)
        {
            RefreshView();
            _logger.ZLogInformation($"ゲーム開始通知を受信 players={bundle.InputsByClient.Count}");
            return;
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
        // 自動Tick(D-059)で毎Tick確定するため、全員Idleの確定はログに残さない
        if (actions.Any(a => a != PlayerAction.Idle))
        {
            _logger.ZLogInformation(
                $"確定 tick={bundle.Tick} → boss={_logic.BossHp} players={string.Join(",", _logic.PlayerHps)} phase={_logic.Phase}"
            );
        }
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
                        ["action"] = ActionToWire(action),
                    }
                )
            )
        );
        // 自動Tick(D-059)で毎回送信するため、Idleはログに残さない
        if (action != PlayerAction.Idle)
        {
            _logger.ZLogInformation($"input(tick={tick}, action={action}) をサーバへ送信");
        }
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
        StartStateeServer(host);
    }

    // TCP 待ち受け(外部 CLI/MCP の入口)は Main.StateeServer.cs に隔離している。
    // ExportRelease ではファイルごとビルドから除外され、この呼び出しは丸ごと消える(D-065)
    partial void StartStateeServer(StateeHost host);

    partial void StopStateeServer();

    private static PlayerAction ParseAction(string? name) =>
        name?.ToLowerInvariant() switch
        {
            "attack" => PlayerAction.Attack,
            "left" => PlayerAction.MoveLeft,
            "right" => PlayerAction.MoveRight,
            _ => PlayerAction.Idle,
        };

    private static string ActionToWire(PlayerAction action) =>
        action switch
        {
            PlayerAction.Attack => "attack",
            PlayerAction.MoveLeft => "left",
            PlayerAction.MoveRight => "right",
            _ => "idle",
        };
}
