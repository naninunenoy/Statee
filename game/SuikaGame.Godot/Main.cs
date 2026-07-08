using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Declaree;
using Declaree.Godot;
using Declaree.Statee;
using Godot;
using Microsoft.Extensions.Logging;
using R3;
using Statee.Core;
using Statee.Remote;
using SuikaGame.Logic;
using VitalRouter;
using ZLogger;
using Button = Declaree.Button;
using Label = Declaree.Label;

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
    private GameCommandRouter _commands = null!;

    // Declaree による宣言的 UI(D-035)。ツリーは (Phase, Score) の純関数で、
    // 状態が変わるたびに全再構築する。_uiSnapshot はレイアウト確定後の Rect 込み記述子。
    // メインスレッドが毎物理フレーム差し替え、ソケットスレッド(CaptureState)は読むだけ
    private StaticBody2D? _container;
    private CanvasLayer _uiLayer = null!;
    private UiNode _uiTree = BuildUi(GamePhase.Title, 0);
    private Control? _uiRoot;
    private volatile UiDescriptor _uiSnapshot = UiTree.Describe(BuildUi(GamePhase.Title, 0));
    private IDisposable _subscriptions = null!;
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;
    private float _dropX = (WallLeft + WallRight) / 2f;

    /// <summary>キー入力 → コマンド発行の配線1件(D-039)。Publishes は配線から導出され、
    /// この表が _UnhandledInput の処理と game/input State の両方の情報源になる。</summary>
    private sealed record KeyBinding(
        Key Key,
        GamePhase ActiveIn,
        string Publishes,
        string Explain,
        Action Publish
    );

    private KeyBinding[] _keyBindings = [];

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)と State 更新を動かし続ける。
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

        // headless では project.godot の window サイズが反映されず 64x64 になり、
        // UI が画面外に出るとクリックのヒットテストが外れる。実行時に明示する
        GetWindow().Size = new Vector2I(600, 800);

        _logic = new SuikaLogic(ParseIntArg("--seed=", DefaultSeed));
        _commands = new GameCommandRouter(_flow);
        _keyBindings =
        [
            BindKey(Key.Escape, GamePhase.Playing, new PauseGameCommand(), "ポーズする"),
            BindKey(
                Key.Escape,
                GamePhase.Paused,
                new ResumeGameCommand(),
                "ポーズを解除して再開する"
            ),
        ];
        _uiLayer = new CanvasLayer { Name = "Ui" };
        AddChild(_uiLayer);
        RebuildUi();
        var subscriptions = Disposable.CreateBuilder();
        _logic.Merges.Subscribe(Logic_MergeOccurred).AddTo(ref subscriptions);
        _logic
            .Score.Subscribe(score =>
            {
                RebuildUi();
                _logger.ZLogInformation($"スコア: {score}");
            })
            .AddTo(ref subscriptions);
        _logic
            .IsGameOver.Where(gameOver => gameOver)
            .Subscribe(_ =>
            {
                _flow.EndGame();
                _logger.ZLogInformation($"ゲームオーバー");
            })
            .AddTo(ref subscriptions);
        _flow.Phase.Subscribe(Flow_PhaseChanged).AddTo(ref subscriptions);
        _commands.RestartRequests.Subscribe(_ => RestartBoard()).AddTo(ref subscriptions);
        _commands
            .ExitRequests.Subscribe(_ =>
            {
                _logger.ZLogInformation($"終了要求を受信。終了する");
                GetTree().Quit();
            })
            .AddTo(ref subscriptions);
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
        // 物理を止める要因は2つ: Statee の時間制御(freeze/step)とゲーム内ポーズ
        GetTree().Paused = _time.IsFrozen || _flow.Phase.CurrentValue == GamePhase.Paused;
    }

    public override void _PhysicsProcess(double delta)
    {
        // step のフレーム計数は「実際に物理が動いたフレーム」だけを数える
        // (IsFrozen の変更がツリーへ反映されるまでの1フレームのずれを数えない)
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
        if (_uiRoot is not null)
        {
            _uiSnapshot = UiSnapshot.Capture(_uiTree, _uiRoot);
        }
        if (!GetTree().Paused)
        {
            _time.OnFrame();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // キー入力はバインド表(D-039)経由でコマンドを発行する。
        // 遷移の成否は GameFlow の規則に委ねる
        if (@event is InputEventKey { Pressed: true } keyEvent)
        {
            var phase = _flow.Phase.CurrentValue;
            foreach (var binding in _keyBindings)
            {
                if (binding.Key == keyEvent.Keycode && binding.ActiveIn == phase)
                {
                    binding.Publish();
                    return;
                }
            }
        }

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
        // GameOver 中も盤面(壁・フルーツ)は残るため、壁は描き続ける
        if (_flow.Phase.CurrentValue is not (GamePhase.Playing or GamePhase.GameOver))
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
        _commands.Dispose();
        _flow.Dispose();
        _logic.Dispose();
        _loggerFactory?.Dispose();
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_board);
        host.RegisterStateProvider(_scene);
        // Declaree の UI(幾何 Rect 込みスナップショット)を State として公開する(D-035)
        host.RegisterStateProvider(new UiStateProvider("ui/tree", () => _uiSnapshot));
        // キーバインドの State 公開(D-039)。配線表 _keyBindings から導出するため
        // 実装と乖離しない。起動後は不変なので一度だけ構築する
        var keyEntries = Array.ConvertAll(
            _keyBindings,
            binding => new
            {
                Key = binding.Key.ToString(),
                ActiveIn = binding.ActiveIn.ToString(),
                Publishes = binding.Publishes,
                Explain = binding.Explain,
            }
        );
        host.RegisterStateProvider(
            new SnapshotStateProvider("game/input", () => new { Keys = keyEntries })
        );
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
                if (_flow.Phase.CurrentValue != GamePhase.Playing)
                {
                    throw new InvalidOperationException("プレイ中ではないので投下できない");
                }

                if (args.GetString("x") is { } xText)
                {
                    _dropX = float.Parse(xText, CultureInfo.InvariantCulture);
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
            "click",
            args =>
            {
                // name 指定なら ui/tree の Rect から中心を導出する(D-038)。
                // どちらの形でも実際の入力経路(PushClick)を通るため、
                // 非表示・無効なボタンには正しく「効かない」
                var position = args.GetString("name") is { } name
                    ? CenterOf(name)
                    : new Vector2(
                        float.Parse(
                            args.GetString("x")
                                ?? throw new InvalidOperationException("x か name を指定すること"),
                            CultureInfo.InvariantCulture
                        ),
                        float.Parse(
                            args.GetString("y")
                                ?? throw new InvalidOperationException("y を指定すること"),
                            CultureInfo.InvariantCulture
                        )
                    );
                PushClick(position);
                _logger.ZLogInformation($"click x={position.X} y={position.Y}");
                return new { X = position.X, Y = position.Y };
            }
        );
        host.RegisterMainThreadCommand(
            "key",
            args =>
            {
                var name =
                    args.GetString("key")
                    ?? throw new InvalidOperationException("key を指定すること(例: escape)");
                var key = Enum.Parse<Key>(name, ignoreCase: true);
                var viewport = GetViewport();
                viewport.PushInput(new InputEventKey { Keycode = key, Pressed = true });
                viewport.PushInput(new InputEventKey { Keycode = key, Pressed = false });
                _logger.ZLogInformation($"key {key}");
                return new { Key = key.ToString() };
            }
        );
        host.RegisterMainThreadCommand(
            "screenshot",
            args =>
            {
                var path =
                    args.GetString("path")
                    ?? throw new InvalidOperationException("path を指定すること");
                var image =
                    GetViewport().GetTexture()?.GetImage()
                    ?? throw new InvalidOperationException(
                        "描画が無いため撮影できない(headless では screenshot は使えない。D-034)"
                    );
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
                var error = image.SavePng(path);
                if (error != Error.Ok)
                {
                    throw new InvalidOperationException($"スクリーンショット保存失敗: {error}");
                }

                _logger.ZLogInformation($"screenshot path={path}");
                return new { Path = Path.GetFullPath(path) };
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

    /// <summary>
    /// キーを「押下 → コマンド発行」で配線する(D-039)。State に公開される
    /// Publishes(作用)はこの配線から導出されるため、実装と乖離しない(D-032 と同じ原則)。
    /// </summary>
    private KeyBinding BindKey<TCommand>(
        Key key,
        GamePhase activeIn,
        TCommand command,
        string explain
    )
        where TCommand : ICommand =>
        new(
            key,
            activeIn,
            typeof(TCommand).Name,
            explain,
            () => _ = _commands.Router.PublishAsync(command)
        );

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
    /// 実際の入力経路で左クリックを再現する(GUIDELINE 3.2, D-031 スライス③)。
    /// UI のヒットテストを通るため、非表示・無効なボタンへのクリックは正しく「効かない」。
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
            _time.IsFrozen,
            _logic.PeekNext().ToString(),
            entries
        );
    }

    /// <summary>
    /// 状態(フェーズ・スコア)から UI ツリーを導出する純関数(D-035)。
    /// OnClick の ID は発行される VitalRouter コマンド型名に一致させる(D-032 の Publishes 相当)。
    /// </summary>
    private static UiNode BuildUi(GamePhase phase, int score) =>
        phase switch
        {
            GamePhase.Title => new Center(
                new VBox(
                    new Label("スイカゲーム") { Name = "TitleLabel", Explain = "タイトルロゴ" },
                    new Button("はじめる", OnClick: nameof(StartGameCommand))
                    {
                        Name = "StartButton",
                        Explain = "ゲームを開始するボタン",
                    },
                    new Button("おわる", OnClick: nameof(ExitGameCommand))
                    {
                        Name = "ExitButton",
                        Explain = "ゲームを終了するボタン",
                    }
                )
            ),
            GamePhase.Paused => new Center(
                new VBox(
                    new Label("ポーズ中")
                    {
                        Name = "PausedLabel",
                        Explain = "ポーズ中であることの表示",
                    },
                    new Button("続ける", OnClick: nameof(ResumeGameCommand))
                    {
                        Name = "ResumeButton",
                        Explain = "ポーズを解除して再開するボタン(ESC でも解除できる)",
                    },
                    new Button("やり直す", OnClick: nameof(RestartGameCommand))
                    {
                        Name = "RestartButton",
                        Explain = "フルーツとスコアをリセットして再開するボタン",
                    },
                    new Button("終了", OnClick: nameof(ExitGameCommand))
                    {
                        Name = "ExitButton",
                        Explain = "ゲームを終了するボタン",
                    }
                )
            ),
            GamePhase.GameOver => new Center(
                new VBox(
                    new Label("GameOver")
                    {
                        Name = "GameOverLabel",
                        Explain = "ゲームオーバーであることの表示",
                    },
                    new Label($"スコア: {score}")
                    {
                        Name = "FinalScoreLabel",
                        Explain = "最終スコアの表示",
                    },
                    new Button("タイトルへ", OnClick: nameof(GoToTitleCommand))
                    {
                        Name = "ToTitleButton",
                        Explain = "盤面とスコアをリセットしてタイトルへ戻るボタン",
                    }
                )
            ),
            // Margin 直下の Label は縦センターに置かれるため、VBox で包んで上寄せにする
            _ => new Margin(
                16,
                new VBox(
                    new Label($"スコア: {score}")
                    {
                        Name = "ScoreLabel",
                        Explain = "現在のスコアの表示",
                    }
                )
            ),
        };

    /// <summary>全破棄・全再構築(D-035)。状態変更(フェーズ・スコア)の購読から呼ばれる。</summary>
    private void RebuildUi()
    {
        _uiTree = BuildUi(_flow.Phase.CurrentValue, _logic.Score.CurrentValue);
        _uiRoot?.QueueFree();
        _uiRoot = UiRenderer.Render(_uiTree, Dispatch);
        _uiLayer.AddChild(_uiRoot);
        // アンカーはツリーに入って親サイズが確定してから設定する
        // (親なしで呼ぶとサイズ 0 のまま確定し、中央寄せが効かない)
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // 全面に広がるルートがプレイ中のクリック(_UnhandledInput → Drop)を飲み込まないようにする
        _uiRoot.MouseFilter = Control.MouseFilterEnum.Pass;
    }

    /// <summary>UI イベント(OnClick の ID)を VitalRouter コマンド発行に変換する(D-032)。</summary>
    private void Dispatch(string eventId)
    {
        switch (eventId)
        {
            case nameof(StartGameCommand):
                _ = _commands.Router.PublishAsync(new StartGameCommand());
                break;
            case nameof(ResumeGameCommand):
                _ = _commands.Router.PublishAsync(new ResumeGameCommand());
                break;
            case nameof(RestartGameCommand):
                _ = _commands.Router.PublishAsync(new RestartGameCommand());
                break;
            case nameof(ExitGameCommand):
                _ = _commands.Router.PublishAsync(new ExitGameCommand());
                break;
            case nameof(GoToTitleCommand):
                _ = _commands.Router.PublishAsync(new GoToTitleCommand());
                break;
        }
    }

    private void Flow_PhaseChanged(GamePhase phase)
    {
        _scene.Update(phase.ToString());
        RebuildUi();
        if (phase == GamePhase.Playing)
        {
            BuildContainer();
        }
        // タイトルへ戻ったときに壁の描画を消すため、遷移のたびに再描画する
        QueueRedraw();
    }

    /// <summary>やり直し(D-037)。物理ボディを破棄し、規則エンジンをリセットする。</summary>
    private void RestartBoard()
    {
        foreach (var fruit in _fruits.Values)
        {
            fruit.QueueFree();
        }
        _fruits.Clear();
        _logic.Reset();
        _logger.ZLogInformation($"やり直し: フルーツとスコアをリセット");
    }

    private void BuildContainer()
    {
        // やり直し等で Playing に再入しても容器は一度だけ作る
        if (_container is not null)
        {
            return;
        }

        var container = new StaticBody2D { Name = "Container" };
        container.AddChild(Wall(new Vector2(WallLeft, 0f), new Vector2(WallLeft, FloorY)));
        container.AddChild(Wall(new Vector2(WallRight, 0f), new Vector2(WallRight, FloorY)));
        container.AddChild(Wall(new Vector2(WallLeft, FloorY), new Vector2(WallRight, FloorY)));
        AddChild(container);
        _container = container;

        static CollisionShape2D Wall(Vector2 a, Vector2 b) =>
            new()
            {
                Shape = new SegmentShape2D { A = a, B = b },
            };
    }

    /// <summary>デリゲートでスナップショットを返す State プロバイダ。ソケットスレッドから呼ばれる。</summary>
    private sealed class SnapshotStateProvider(string path, Func<object> capture) : IStateProvider
    {
        public string Path => path;

        public object CaptureState() => capture();
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
