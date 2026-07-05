using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Microsoft.Extensions.Logging;
using R3;
using Statee.Core;
using Statee.Remote;
using SuikaGame.Logic;
using ZLogger;

namespace SuikaGame;

/// <summary>
/// スイカゲームの Godot 層エントリポイント。物理・描画・入力だけを担い、
/// 規則(合体・スコア・ゲームオーバー)は SuikaGame.Logic に委ねる(D-011, D-024)。
/// 境界: 接触 → ReportContact / 溢れ → SetOverflowing / 時間 → Tick、
/// 逆向きは Merges 購読で物理ボディを差し替える。
/// Statee を組み込み、start / drop コマンドと State(game/board, game/scene)を外部へ公開する。
/// 画面遷移(タイトル → プレイ)の規則は GameFlow が持ち、ここは Phase 購読で UI を差し替えるだけ。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;
    private const float WallLeft = 100f;
    private const float WallRight = 500f;
    private const float FloorY = 760f;
    private const float OverflowLineY = 260f;
    private const float DropY = 120f;

    private readonly Dictionary<FruitId, Fruit> _fruits = [];
    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly BoardState _board = new();
    private readonly SceneState _scene = new();
    private readonly TimeControl _time = new();
    private readonly GameFlow _flow = new();
    private SuikaLogic _logic = null!;
    private Control _titleScreen = null!;
    private Label _scoreLabel = null!;
    private IDisposable _subscriptions = null!;
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;
    private float _dropX = (WallLeft + WallRight) / 2f;

    public override void _Ready()
    {
        // pause 中も Statee のコマンド処理(Pump)と State 更新を動かし続ける。
        // 物理を止める役は Pausable な子(フルーツ)が担う
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new BufferLoggerProvider(buffer));
            builder.AddZLoggerConsole();
        });
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new SuikaLogic(ParseIntArg("--seed=", DefaultSeed));
        BuildUi();
        var subscriptions = Disposable.CreateBuilder();
        _logic.Merges.Subscribe(Logic_MergeOccurred).AddTo(ref subscriptions);
        _logic
            .Score.Subscribe(score =>
            {
                _scoreLabel.Text = $"スコア: {score}";
                _logger.ZLogInformation($"スコア: {score}");
            })
            .AddTo(ref subscriptions);
        _logic
            .IsGameOver.Where(gameOver => gameOver)
            .Subscribe(_ => _logger.ZLogInformation($"ゲームオーバー"))
            .AddTo(ref subscriptions);
        _flow.Phase.Subscribe(Flow_PhaseChanged).AddTo(ref subscriptions);
        _subscriptions = subscriptions.Build();

        StartStatee(buffer);
        _logger.ZLogInformation($"SuikaGame 起動 next={_logic.PeekNext()}");

        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0)
        {
            RunSmoke();
        }
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        GetTree().Paused = _time.IsPaused;
    }

    public override void _PhysicsProcess(double delta)
    {
        // step のフレーム計数は「実際に物理が動いたフレーム」だけを数える
        // (IsPaused の変更がツリーへ反映されるまでの1フレームのずれを数えない)
        if (!GetTree().Paused)
        {
            // 溢れ判定は Area2D でなく毎フレームの位置走査で行う(理由は docs/adr/notes/suika-physics-boundary.md)。
            // 落下直後の誤検知を避けるため、一度でも接触したフルーツだけを対象にする
            foreach (var (id, fruit) in _fruits)
            {
                var overflowing =
                    fruit.HasContacted
                    && fruit.Position.Y - Fruit.RadiusOf(fruit.Kind) < OverflowLineY;
                _logic.SetOverflowing(id, overflowing);
            }

            _logic.Tick(delta);
        }

        // OnFrame は wait 中のソケットスレッドを起こすため、State 更新の後に呼ぶ
        // (先に呼ぶと wait が1フレーム古い盤面を読む)
        UpdateBoardState();
        if (!GetTree().Paused)
        {
            _time.OnFrame();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_flow.Phase.CurrentValue != GamePhase.Playing)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion motion:
                _dropX = motion.Position.X;
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                Drop();
                break;
        }
    }

    public override void _Draw()
    {
        if (_flow.Phase.CurrentValue != GamePhase.Playing)
        {
            return;
        }

        var wallColor = new Color(0.35f, 0.3f, 0.25f);
        DrawLine(
            new Vector2(WallLeft, OverflowLineY),
            new Vector2(WallLeft, FloorY),
            wallColor,
            4f
        );
        DrawLine(
            new Vector2(WallRight, OverflowLineY),
            new Vector2(WallRight, FloorY),
            wallColor,
            4f
        );
        DrawLine(new Vector2(WallLeft, FloorY), new Vector2(WallRight, FloorY), wallColor, 4f);
        DrawDashedLine(
            new Vector2(WallLeft, OverflowLineY),
            new Vector2(WallRight, OverflowLineY),
            new Color(0.9f, 0.2f, 0.2f, 0.6f),
            2f
        );
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _subscriptions.Dispose();
        _flow.Dispose();
        _logic.Dispose();
        _loggerFactory?.Dispose();
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_board);
        host.RegisterStateProvider(_scene);
        host.RegisterTimeControl(_time);
        host.RegisterMainThreadCommand(
            "start",
            _ =>
            {
                if (!_flow.StartGame())
                {
                    throw new InvalidOperationException("タイトル画面ではないので開始できない");
                }

                _logger.ZLogInformation($"ゲーム開始");
                return new { Phase = _flow.Phase.CurrentValue.ToString() };
            }
        );
        host.RegisterMainThreadCommand(
            "drop",
            args =>
            {
                if (args.GetString("x") is { } xText)
                {
                    _dropX = float.Parse(xText, CultureInfo.InvariantCulture);
                }

                if (_flow.Phase.CurrentValue != GamePhase.Playing)
                {
                    throw new InvalidOperationException("プレイ中ではないので投下できない");
                }

                var dropped =
                    Drop() ?? throw new InvalidOperationException("ゲームオーバー中は投下できない");
                _logger.ZLogInformation(
                    $"drop id={dropped.Id.AsPrimitive()} kind={dropped.Kind} x={dropped.X}"
                );
                return new
                {
                    Id = dropped.Id.AsPrimitive(),
                    Kind = dropped.Kind.ToString(),
                    X = dropped.X,
                    Next = _logic.PeekNext().ToString(),
                };
            }
        );
        host.RegisterMainThreadCommand(
            "quit",
            _ =>
            {
                _logger.ZLogInformation($"quit を受信。終了する");
                GetTree().Quit();
                return new { Quitting = true };
            }
        );
        _server = new StateeTcpServer(host, ParseIntArg("--port=", DefaultPort));
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    private (FruitId Id, FruitKind Kind, float X)? Drop()
    {
        if (_flow.Phase.CurrentValue != GamePhase.Playing || _logic.IsGameOver.CurrentValue)
        {
            return null;
        }

        var kind = _logic.PeekNext();
        var radius = Fruit.RadiusOf(kind);
        var x = Mathf.Clamp(_dropX, WallLeft + radius, WallRight - radius);
        var id = _logic.SpawnNext();
        SpawnBody(id, kind, new Vector2(x, DropY));
        return (id, kind, x);
    }

    private void SpawnBody(FruitId id, FruitKind kind, Vector2 position)
    {
        var fruit = Fruit.Create(id, kind);
        fruit.Position = position;
        fruit.Contacted += Fruit_Contacted;
        _fruits[id] = fruit;
        AddChild(fruit);
    }

    private void Fruit_Contacted(Fruit self, Fruit other)
    {
        _logic.ReportContact(self.FruitId, other.FruitId);
    }

    private void Logic_MergeOccurred(MergeEvent merge)
    {
        var midpoint = (PositionOf(merge.RemovedA) + PositionOf(merge.RemovedB)) / 2f;
        RemoveBody(merge.RemovedA);
        RemoveBody(merge.RemovedB);
        if (merge is { Created: { } created, CreatedKind: { } createdKind })
        {
            // 接触シグナル(物理フラッシュ中)から呼ばれるため、ボディの追加は次フレームへ遅延する
            Callable.From(() => SpawnBody(created, createdKind, midpoint)).CallDeferred();
        }
    }

    private Vector2 PositionOf(FruitId id) => _fruits[id].Position;

    private void RemoveBody(FruitId id)
    {
        _fruits[id].QueueFree();
        _fruits.Remove(id);
    }

    private void UpdateBoardState()
    {
        var entries = new BoardState.FruitEntry[_fruits.Count];
        var index = 0;
        foreach (var (id, fruit) in _fruits)
        {
            entries[index++] = new BoardState.FruitEntry(
                id.AsPrimitive(),
                fruit.Kind.ToString(),
                fruit.Position.X,
                fruit.Position.Y
            );
        }

        _board.Update(
            _logic.Score.CurrentValue,
            _logic.IsGameOver.CurrentValue,
            _time.IsPaused,
            _logic.PeekNext().ToString(),
            entries
        );
    }

    /// <summary>
    /// タイトル画面とプレイ中 HUD を構築する。ノード名は安定 ID(GUIDELINE 3.4)。
    /// </summary>
    private void BuildUi()
    {
        var startButton = new Button { Name = "StartButton", Text = "はじめる" };
        startButton.Pressed += () => _flow.StartGame();
        var exitButton = new Button { Name = "ExitButton", Text = "おわる" };
        exitButton.Pressed += () => GetTree().Quit();

        var menu = new VBoxContainer { Name = "TitleMenu" };
        menu.SetAnchorsPreset(Control.LayoutPreset.Center);
        menu.AddChild(new Label { Name = "TitleLabel", Text = "スイカゲーム" });
        menu.AddChild(startButton);
        menu.AddChild(exitButton);

        _titleScreen = new Control { Name = "TitleScreen" };
        _titleScreen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _titleScreen.AddChild(menu);

        _scoreLabel = new Label
        {
            Name = "ScoreLabel",
            Text = "スコア: 0",
            Position = new Vector2(16f, 16f),
            Visible = false,
        };

        var ui = new CanvasLayer { Name = "Ui" };
        ui.AddChild(_titleScreen);
        ui.AddChild(_scoreLabel);
        AddChild(ui);
    }

    private void Flow_PhaseChanged(GamePhase phase)
    {
        _scene.Update(phase.ToString());
        _titleScreen.Visible = phase == GamePhase.Title;
        _scoreLabel.Visible = phase == GamePhase.Playing;
        if (phase == GamePhase.Playing)
        {
            BuildContainer();
            QueueRedraw();
        }
    }

    private void BuildContainer()
    {
        var container = new StaticBody2D { Name = "Container" };
        container.AddChild(Wall(new Vector2(WallLeft, 0f), new Vector2(WallLeft, FloorY)));
        container.AddChild(Wall(new Vector2(WallRight, 0f), new Vector2(WallRight, FloorY)));
        container.AddChild(Wall(new Vector2(WallLeft, FloorY), new Vector2(WallRight, FloorY)));
        AddChild(container);

        static CollisionShape2D Wall(Vector2 a, Vector2 b) =>
            new()
            {
                Shape = new SegmentShape2D { A = a, B = b },
            };
    }

    private static int ParseIntArg(string prefix, int defaultValue)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (
                arg.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(arg[prefix.Length..], out var value)
            )
            {
                return value;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// headless での配線確認(`--smoke`)。同種2個を隣接落下させ、
    /// 物理接触 → ReportContact → Merges の経路が通ったら終了する。固定時間待機はしない。
    /// </summary>
    private void RunSmoke()
    {
        _flow.StartGame();
        _logic
            .Merges.Take(1)
            .Subscribe(merge =>
            {
                GD.Print($"SMOKE: merge 検出 created={merge.CreatedKind}");
                Callable.From(() => GetTree().Quit()).CallDeferred();
            });
        // 左右に離すと着地後に届かないことがあるため、縦に積んで確実に衝突させる
        var center = (WallLeft + WallRight) / 2f;
        SpawnBody(_logic.Spawn(FruitKind.Cherry), FruitKind.Cherry, new Vector2(center, 400f));
        SpawnBody(_logic.Spawn(FruitKind.Cherry), FruitKind.Cherry, new Vector2(center, 300f));
        GD.Print("SMOKE: チェリー2個を投下");
    }
}
