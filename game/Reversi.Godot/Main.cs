using System;
using System.Collections.Generic;
using System.Globalization;
using Declaree;
using Declaree.Godot;
using Declaree.Statee;
using Godot;
using Microsoft.Extensions.Logging;
using Reversi.Logic;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using Syncee;
using Syncee.LiteNetLib;
using ZLogger;
using Button = Declaree.Button;
using Label = Declaree.Label;
using LineEdit = Declaree.LineEdit;

namespace Reversi;

/// <summary>
/// Reversi の Godot 層エントリポイント。描画・入力・Statee 配線だけを担い、
/// ゲームルールはすべて Reversi.Logic に置く(docs/USING.md「境界の掟」)。
/// 盤は Node2D 直描画(座標→マス変換は BoardGeometry を純C#でテスト)、
/// タイトル/結果画面は Declaree で宣言する(REVERSI_ROADMAP.md R-4 の判断)。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;
    private const string DefaultGameHost = "127.0.0.1";
    private const int DefaultGamePort = 9410;
    private const string DefaultRoom = "reversi";

    // ボタンの OnClick ID(Declaree の Dispatch と対応)
    private const string EvStartLocal = "StartLocal";
    private const string EvStartNetwork = "StartNetwork";
    private const string EvBackToTitle = "BackToTitle";
    private const string EvExit = "Exit";

    private static readonly BoardGeometry Geometry = new(OriginX: 40f, OriginY: 60f, CellSize: 52f);

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly ReversiGame _game = new();
    private readonly BoardState _boardState = new();
    private readonly TurnState _turnState = new();

    private KeyBinding[] _keyBindings = [];
    private CanvasLayer _uiLayer = null!;
    private UiNode _uiTree = null!;
    private Control? _uiRoot;
    private volatile UiDescriptor _uiSnapshot = null!;
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    // ネット対戦(N-4)。接続すると _networkMode が true になり、start/place は
    // ローカル即時適用でなくサーバへの送信 + 確定コマンド受信での適用に切り替わる
    private LiteNetLibClientTransport? _network;
    private ReplicaLog? _replicaLog;
    private bool _networkMode;

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = StateeLogging.CreateLoggerFactory(buffer);
        _logger = _loggerFactory.CreateLogger<Main>();

        // headless では project.godot の window サイズが反映されず 64x64 になり、
        // UI が画面外に出るとクリックのヒットテストが外れる。実行時に明示する
        GetWindow().Size = new Vector2I(960, 540);

        _uiLayer = new CanvasLayer { Name = "Ui" };
        AddChild(_uiLayer);

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"Reversi 起動");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        _network?.PollEvents();
        if (_uiRoot is not null)
        {
            _uiSnapshot = UiSnapshot.Capture(_uiTree, _uiRoot);
        }
        if (!_time.IsFrozen)
        {
            _time.OnFrame();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (KeyBindingTable.TryHandle(_keyBindings, @event))
        {
            return;
        }
        // 盤クリック。ローカル2人対戦は同じ入力経路で黒番と白番が交互に着手する
        if (
            _game.Phase == GamePhase.Playing
            && @event
                is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
            && Geometry.CellAt(click.Position.X, click.Position.Y) is { } cell
        )
        {
            if (_networkMode)
            {
                SendNetworkCommand(
                    "place",
                    new Dictionary<string, string>
                    {
                        ["x"] = cell.X.ToString(),
                        ["y"] = cell.Y.ToString(),
                    }
                );
                _logger.ZLogInformation($"place {cell.X} {cell.Y} をサーバへ送信");
                return;
            }
            var player = _game.CurrentPlayer;
            if (_game.TryPlace(cell.X, cell.Y))
            {
                _logger.ZLogInformation(
                    $"place {cell.X} {cell.Y} {player} → turn={_game.CurrentPlayer}"
                );
                RefreshView();
            }
        }
    }

    public override void _Draw()
    {
        if (_game.Phase == GamePhase.Title)
        {
            return;
        }

        // 盤(結果画面でも終局盤面を見せる)
        var boardColor = new Color(0.1f, 0.45f, 0.2f);
        var lineColor = new Color(0.05f, 0.2f, 0.1f);
        var length = Geometry.BoardLength;
        DrawRect(new Rect2(Geometry.OriginX, Geometry.OriginY, length, length), boardColor);
        for (var i = 0; i <= Board.Size; i++)
        {
            var offset = i * Geometry.CellSize;
            DrawLine(
                new Vector2(Geometry.OriginX + offset, Geometry.OriginY),
                new Vector2(Geometry.OriginX + offset, Geometry.OriginY + length),
                lineColor,
                2f
            );
            DrawLine(
                new Vector2(Geometry.OriginX, Geometry.OriginY + offset),
                new Vector2(Geometry.OriginX + length, Geometry.OriginY + offset),
                lineColor,
                2f
            );
        }

        // 石と、現在手番の合法手ヒント
        var radius = Geometry.CellSize * 0.4f;
        for (var y = 0; y < Board.Size; y++)
        {
            for (var x = 0; x < Board.Size; x++)
            {
                var disc = _game.Board[x, y];
                if (disc == Disc.None)
                {
                    continue;
                }
                var (cx, cy) = Geometry.CenterOf(x, y);
                DrawCircle(
                    new Vector2(cx, cy),
                    radius,
                    disc == Disc.Black ? Colors.Black : Colors.White
                );
            }
        }
        if (_game.Phase == GamePhase.Playing)
        {
            foreach (var (x, y) in _game.Board.GetLegalMoves(_game.CurrentPlayer))
            {
                var (cx, cy) = Geometry.CenterOf(x, y);
                DrawCircle(new Vector2(cx, cy), 5f, new Color(1f, 1f, 0.3f, 0.8f));
            }
        }
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _network?.Dispose();
        _loggerFactory?.Dispose();
    }

    /// <summary>
    /// 状態(フェーズ・手番・石数)から UI ツリーを導出する純関数(D-035)。
    /// 盤そのものは Node2D 直描画のため UI ツリーには含めない。
    /// </summary>
    private static UiNode BuildUi(ReversiGame game) =>
        game.Phase switch
        {
            GamePhase.Title => new Center(
                new VBox(
                    new Label("リバーシ") { Name = "TitleLabel", Explain = "タイトルロゴ" },
                    new Button("ローカル2人対戦", OnClick: EvStartLocal)
                    {
                        Name = "StartLocalButton",
                        Explain = "1画面で2人が交互に着手する対局を開始するボタン",
                    },
                    new LineEdit("")
                    {
                        Name = "RoomInput",
                        PlaceholderText = "合言葉(未入力なら既定値)",
                        Explain = "ネット対戦の合言葉。同じ合言葉を入力した相手とだけ対戦できる",
                    },
                    new Button("ネット対戦", OnClick: EvStartNetwork)
                    {
                        Name = "StartNetworkButton",
                        Explain =
                            "ネット対戦(D-050)。合言葉が一致する Reversi.Server に接続して対局を開始する",
                    },
                    new Button("おわる", OnClick: EvExit)
                    {
                        Name = "ExitButton",
                        Explain = "ゲームを終了するボタン",
                    }
                )
            ),
            GamePhase.Result => new Margin(
                16,
                new VBox(
                    new Label(WinnerText(game))
                    {
                        Name = "WinnerLabel",
                        Explain = "勝敗の表示(黒/白の勝ち・引き分け)",
                    },
                    new Label(
                        $"黒 {game.Board.Count(Disc.Black)} - 白 {game.Board.Count(Disc.White)}"
                    )
                    {
                        Name = "ScoreLabel",
                        Explain = "終局時の石数",
                    },
                    new Button("タイトルへ", OnClick: EvBackToTitle)
                    {
                        Name = "BackToTitleButton",
                        Explain = "タイトルへ戻るボタン",
                    }
                )
            ),
            // Playing: 手番と石数の表示のみ(着手は盤クリック)
            _ => new Margin(
                8,
                new VBox(
                    new Label(
                        $"手番: {(game.CurrentPlayer == Disc.Black ? "黒" : "白")}  "
                            + $"黒 {game.Board.Count(Disc.Black)} - 白 {game.Board.Count(Disc.White)}"
                    )
                    {
                        Name = "TurnLabel",
                        Explain = "現在の手番と石数の表示",
                    }
                )
            ),
        };

    private static string WinnerText(ReversiGame game)
    {
        var suffix = game.EndReason == GameEndReason.Disconnected ? "(相手の切断による不戦勝)" : "";
        return game.Winner switch
        {
            Disc.Black => $"黒の勝ち{suffix}",
            Disc.White => $"白の勝ち{suffix}",
            _ => "引き分け",
        };
    }

    /// <summary>UI イベント(OnClick の ID)をゲーム操作へ変換する。</summary>
    private void Dispatch(string eventId)
    {
        switch (eventId)
        {
            case EvStartLocal:
                if (_game.Phase == GamePhase.Title)
                {
                    _game.Start(GameMode.LocalTwoPlayer);
                    _logger.ZLogInformation($"対局開始 mode={_game.Mode}");
                    RefreshView();
                }
                break;
            case EvStartNetwork:
                if (_game.Phase == GamePhase.Title)
                {
                    ConnectNetwork(CurrentRoomInput());
                    SendNetworkCommand("start", null);
                    _logger.ZLogInformation($"ネット対戦の開始要求をサーバへ送信");
                }
                break;
            case EvBackToTitle:
                _game.BackToTitle();
                _logger.ZLogInformation($"タイトルへ戻る");
                RefreshView();
                break;
            case EvExit:
                _logger.ZLogInformation($"終了要求を受信。終了する");
                GetTree().Quit();
                break;
        }
    }

    /// <summary>状態変更後に State・UI・描画へ反映する。UI は全破棄・全再構築(D-035)。</summary>
    private void RefreshView()
    {
        _boardState.Update(_game.Board);
        _turnState.Update(_game);
        _uiTree = BuildUi(_game);
        _uiRoot?.QueueFree();
        _uiRoot = UiRenderer.Render(_uiTree, Dispatch);
        _uiLayer.AddChild(_uiRoot);
        // アンカーはツリーに入って親サイズが確定してから設定する
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // 全面に広がるルートが盤クリック(_UnhandledInput)を飲み込まないようにする
        _uiRoot.MouseFilter = Control.MouseFilterEnum.Pass;
        _uiSnapshot = UiTree.Describe(_uiTree);
        QueueRedraw();
    }

    private object TurnResult() =>
        new
        {
            Phase = _game.Phase.ToString(),
            CurrentPlayer = _game.CurrentPlayer.ToString(),
            MoveCount = _game.MoveCount,
            Winner = _game.Winner.ToString(),
            EndReason = _game.EndReason.ToString(),
        };

    /// <summary>
    /// ui/tree の RoomInput(LineEdit)に入力された合言葉を読む。空なら既定値。
    /// LineEdit は値を運ぶイベントを持たない(Declaree の方針)ため、Godot コントロールを
    /// Name(D-038)で直接参照する。
    /// </summary>
    private string CurrentRoomInput()
    {
        var text = (_uiRoot?.FindChild("RoomInput", true, false) as Godot.LineEdit)?.Text;
        return string.IsNullOrWhiteSpace(text) ? DefaultRoom : text;
    }

    /// <summary>Reversi.Server へ接続する(まだ未接続の場合のみ)。以後 start/place はサーバ送信になる。</summary>
    private void ConnectNetwork(string room)
    {
        if (_network is not null)
        {
            return;
        }
        _networkMode = true;
        _replicaLog = new ReplicaLog(ApplyEnvelope);
        _network = new LiteNetLibClientTransport();
        _network.Received += bytes =>
        {
            _replicaLog.OnReceived(SyncWire.DeserializeEnvelope(bytes));
            RefreshView();
        };
        var host = CmdlineArgs.ParseString("--game-host=", DefaultGameHost);
        var port = CmdlineArgs.ParseInt("--game-port=", DefaultGamePort);
        _network.Connect(host, port, room);
        _logger.ZLogInformation($"Reversi.Server へ接続 host={host} port={port} room={room}");
    }

    /// <summary>サーバから確定した1コマンドを適用する(Reversi.Server の Committed ハンドラと同型)。</summary>
    private void ApplyEnvelope(CommandEnvelope envelope)
    {
        switch (envelope.Command)
        {
            case "start":
                _game.Start(GameMode.Network);
                break;
            case "place":
                var x = int.Parse(envelope.Args!["x"], CultureInfo.InvariantCulture);
                var y = int.Parse(envelope.Args!["y"], CultureInfo.InvariantCulture);
                _game.TryPlace(x, y);
                break;
            case "disconnect":
                _game.EndByDisconnect(Enum.Parse<Disc>(envelope.Args!["seat"]));
                break;
        }
        _logger.ZLogInformation(
            $"確定 #{envelope.Sequence} {envelope.Command} → phase={_game.Phase} turn={_game.CurrentPlayer}"
        );
    }

    private void SendNetworkCommand(string command, Dictionary<string, string>? args) =>
        _network!.Send(SyncWire.Serialize(new CommandRequest(command, args)));

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_boardState);
        host.RegisterStateProvider(_turnState);
        // Declaree の UI(幾何 Rect 込みスナップショット)を State として公開する(D-035)
        host.RegisterStateProvider(new UiStateProvider("ui/tree", () => _uiSnapshot));
        host.RegisterStateProvider(KeyBindingTable.CreateInputStateProvider(_keyBindings));
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        // ゲーム状態を変えるコマンドはメインスレッドで実行する
        host.RegisterMainThreadCommand(
            "connect",
            args =>
            {
                // start と違い接続だけを行う。複数クライアントを揃えてから start する
                // シナリオ(N-6)で、全クライアントの接続完了を待ってから開始するために使う。
                // room 未指定なら UI(RoomInput)の入力値、それも空なら既定値を使う
                ConnectNetwork(args.GetString("room") ?? CurrentRoomInput());
                return new { Connected = true };
            }
        );
        host.RegisterMainThreadCommand(
            "start",
            args =>
            {
                if (_game.Phase != GamePhase.Title)
                {
                    throw new InvalidOperationException("タイトル画面ではないので開始できない");
                }
                var modeName = args.GetString("mode") ?? nameof(GameMode.LocalTwoPlayer);
                var mode = Enum.Parse<GameMode>(modeName, ignoreCase: true);
                if (mode == GameMode.Network)
                {
                    ConnectNetwork(args.GetString("room") ?? CurrentRoomInput());
                    SendNetworkCommand("start", null);
                    _logger.ZLogInformation($"ネット対戦の開始要求をサーバへ送信");
                    return TurnResult();
                }
                _game.Start(mode);
                RefreshView();
                _logger.ZLogInformation($"対局開始 mode={mode}");
                return TurnResult();
            }
        );
        host.RegisterMainThreadCommand(
            "place",
            args =>
            {
                var x = args.GetInt("x", -1);
                var y = args.GetInt("y", -1);
                if (_networkMode)
                {
                    SendNetworkCommand(
                        "place",
                        new Dictionary<string, string>
                        {
                            ["x"] = x.ToString(),
                            ["y"] = y.ToString(),
                        }
                    );
                    _logger.ZLogInformation($"place {x} {y} をサーバへ送信");
                    return TurnResult();
                }
                var player = _game.CurrentPlayer;
                if (!_game.TryPlace(x, y))
                {
                    throw new InvalidOperationException(
                        $"({x},{y}) は {player} の合法手ではない(phase={_game.Phase})"
                    );
                }
                RefreshView();
                _logger.ZLogInformation($"place {x} {y} {player} → turn={_game.CurrentPlayer}");
                return TurnResult();
            }
        );
        host.RegisterMainThreadCommand(
            "back",
            _ =>
            {
                if (_game.Phase != GamePhase.Result)
                {
                    throw new InvalidOperationException("結果画面ではないので戻れない");
                }
                _game.BackToTitle();
                RefreshView();
                _logger.ZLogInformation($"タイトルへ戻る");
                return TurnResult();
            }
        );
        host.RegisterMainThreadCommand(
            "click",
            args =>
            {
                // name 指定なら ui/tree の Rect から中心を、cell 指定なら盤マスの中心を導出する(D-038)。
                // どちらも実際の入力経路(PushInput)を通るため、非表示・無効な UI には正しく「効かない」
                Vector2 position;
                if (args.GetString("name") is { } name)
                {
                    position = CenterOf(name);
                }
                else if (args.GetString("cell") is { } cellText)
                {
                    // 例: cell=2-3(x-y)
                    var parts = cellText.Split('-');
                    var (cx, cy) = Geometry.CenterOf(int.Parse(parts[0]), int.Parse(parts[1]));
                    position = new Vector2(cx, cy);
                }
                else
                {
                    position = new Vector2(
                        float.Parse(
                            args.GetString("x")
                                ?? throw new InvalidOperationException(
                                    "name か cell か x/y を指定すること"
                                ),
                            CultureInfo.InvariantCulture
                        ),
                        float.Parse(
                            args.GetString("y")
                                ?? throw new InvalidOperationException("y を指定すること"),
                            CultureInfo.InvariantCulture
                        )
                    );
                }
                PushClick(position);
                _logger.ZLogInformation($"click x={position.X} y={position.Y}");
                return new { X = position.X, Y = position.Y };
            }
        );
        _server = new StateeTcpServer(host, CmdlineArgs.ParseInt("--port=", DefaultPort));
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    /// <summary>ui/tree のスナップショットから name の要素を探し、Rect の中心を返す。</summary>
    private Vector2 CenterOf(string name)
    {
        var found =
            UiTree.FindByName(_uiSnapshot, name)
            ?? throw new InvalidOperationException($"UI 要素が見つからない: {name}");
        var rect =
            found.Rect ?? throw new InvalidOperationException($"UI 要素の Rect が未確定: {name}");
        return new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
    }

    /// <summary>
    /// 実際の入力経路で左クリックを再現する(GUIDELINE 3.2)。
    /// UI のヒットテストと _UnhandledInput の両方を通る。
    /// </summary>
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
}
