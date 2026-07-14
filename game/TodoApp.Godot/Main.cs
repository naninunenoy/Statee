using System;
using System.Globalization;
using System.Linq;
using Declaree;
using Declaree.Godot;
using Declaree.Statee;
using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using TodoApp.Logic;
using ZLogger;
using Button = Declaree.Button;
using CheckBox = Declaree.CheckBox;
using Label = Declaree.Label;
using LineEdit = Declaree.LineEdit;
using Slider = Declaree.Slider;

namespace TodoApp;

/// <summary>
/// TodoApp の Godot 層エントリポイント。描画・入力・Statee 配線だけを担い、
/// アプリのルールはすべて TodoApp.Logic に置く(docs/USING.md「境界の掟」)。
/// UI は全面 Declaree(D-035/D-060)。状態 → BuildUi(純関数)→ 全再構築、で駆動する。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;

    // UI イベント ID(Declaree の Dispatch と対応)。":" 区切りでパラメータを埋め込む(D-060)
    private const string EvAdd = "Add";
    private const string EvToggle = "Toggle"; // Toggle:<id>
    private const string EvEdit = "Edit"; // Edit:<id>
    private const string EvCommitEdit = "CommitEdit";
    private const string EvCancelEdit = "CancelEdit";
    private const string EvDelete = "Delete"; // Delete:<id>
    private const string EvConfirmDelete = "ConfirmDelete";
    private const string EvCancelDelete = "CancelDelete";
    private const string EvFilter = "Filter"; // Filter:<All|Active|Completed>
    private const string EvReorder = "Reorder"; // Reorder:<from>:<to>(ReorderList が付与)
    private const string EvFontSize = "FontSizeChanged";

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly TodoLogic _logic = new();
    private readonly GameState _state = new();

    private KeyBinding[] _keyBindings = [];
    private CanvasLayer _uiLayer = null!;
    private UiNode _uiTree = null!;
    private Control? _uiRoot;
    private volatile UiDescriptor _uiSnapshot = null!;
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

        // headless では project.godot の window サイズが反映されず 64x64 になり、
        // UI が画面外に出るとクリックのヒットテストが外れる。実行時に明示する
        GetWindow().Size = new Vector2I(960, 540);

        _uiLayer = new CanvasLayer { Name = "Ui" };
        AddChild(_uiLayer);

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"TodoApp 起動");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
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
        KeyBindingTable.TryHandle(_keyBindings, @event);
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    /// <summary>
    /// 状態から UI ツリーを導出する純関数(D-035)。文字サイズはロジックの FontSize を
    /// 全テキスト要素へ適用する(スライダー1つで全体が変わる。D-060)。
    /// </summary>
    private static UiNode BuildUi(TodoLogic logic)
    {
        var fs = logic.FontSize;
        var main = new Margin(
            16,
            new VBox(
                new Label("TODO") { Name = "TitleLabel", FontSize = fs + 8 },
                new HBox(
                    new LineEdit("")
                    {
                        Name = "NewTaskInput",
                        PlaceholderText = "新しいタスク",
                        MinWidth = 400,
                        FontSize = fs,
                        Explain = "追加するタスクのタイトルを入力する",
                    },
                    new Button("追加", OnClick: EvAdd)
                    {
                        Name = "AddButton",
                        FontSize = fs,
                        Explain = "入力したタイトルでタスクを追加するボタン",
                    }
                ),
                new HBox(
                    FilterButton(logic, TodoFilter.All, "すべて"),
                    FilterButton(logic, TodoFilter.Active, "未完了"),
                    FilterButton(logic, TodoFilter.Completed, "完了済み")
                ),
                new ReorderList(
                    EvReorder,
                    [.. logic.VisibleItems.Select(item => TaskRow(logic, item))]
                )
                {
                    Name = "TaskList",
                    Explain = "タスク一覧。行をドラッグで並び替えできる",
                },
                new HBox(
                    new Label("文字サイズ") { Name = "FontSizeLabel", FontSize = fs },
                    new Slider(
                        TodoLogic.MinFontSize,
                        TodoLogic.MaxFontSize,
                        logic.FontSize,
                        OnChange: EvFontSize
                    )
                    {
                        Name = "FontSizeSlider",
                        MinWidth = 200,
                        Explain = "アプリ全体の文字サイズを変えるスライダー",
                    }
                )
            )
        );

        if (logic.PendingDeleteId is not { } deleteId)
        {
            return new Stack(main);
        }

        // 削除確認モーダル。Overlay が背面 UI へのマウス入力を遮断する(D-060)
        var target = logic.Items.First(x => x.Id == deleteId);
        var dialog = new Overlay(
            new Center(
                new VBox(
                    new Label($"「{target.Title}」を削除しますか?")
                    {
                        Name = "ConfirmLabel",
                        FontSize = fs,
                    },
                    new HBox(
                        new Button("削除する", OnClick: EvConfirmDelete)
                        {
                            Name = "ConfirmDeleteButton",
                            FontSize = fs,
                            Explain = "タスクを削除してダイアログを閉じるボタン",
                        },
                        new Button("やめる", OnClick: EvCancelDelete)
                        {
                            Name = "CancelDeleteButton",
                            FontSize = fs,
                            Explain = "削除せずダイアログを閉じるボタン",
                        }
                    )
                )
            )
        )
        {
            Name = "DeleteDialog",
        };
        return new Stack(main, dialog);
    }

    private static Button FilterButton(TodoLogic logic, TodoFilter filter, string text) =>
        new(text, OnClick: $"{EvFilter}:{filter}")
        {
            Name = $"Filter{filter}Button",
            Disabled = logic.Filter == filter,
            FontSize = logic.FontSize,
            Explain = $"表示フィルタを {filter} に切り替えるボタン(現在のフィルタは押せない)",
        };

    /// <summary>タスク1行。編集中の行は編集フォームに置き換わる。</summary>
    private static UiNode TaskRow(TodoLogic logic, TodoItem item)
    {
        var fs = logic.FontSize;
        if (logic.EditingId == item.Id)
        {
            return new HBox(
                new LineEdit(item.Title)
                {
                    Name = "EditInput",
                    MinWidth = 400,
                    FontSize = fs,
                    Explain = "編集中のタイトル",
                },
                new Button("保存", OnClick: EvCommitEdit)
                {
                    Name = "SaveEditButton",
                    FontSize = fs,
                    Explain = "編集中のタイトルを確定するボタン",
                },
                new Button("キャンセル", OnClick: EvCancelEdit)
                {
                    Name = "CancelEditButton",
                    FontSize = fs,
                    Explain = "編集を破棄するボタン",
                }
            )
            {
                Name = $"TaskRow{item.Id}",
            };
        }
        return new HBox(
            new CheckBox(item.Title, OnToggle: $"{EvToggle}:{item.Id}")
            {
                Name = $"TaskCheck{item.Id}",
                Checked = item.Completed,
                MinWidth = 400,
                FontSize = fs,
                Explain = "タスクの完了トグル",
            },
            new Button("編集", OnClick: $"{EvEdit}:{item.Id}")
            {
                Name = $"EditButton{item.Id}",
                FontSize = fs,
                Explain = "このタスクの編集フォームを開くボタン",
            },
            new Button("削除", OnClick: $"{EvDelete}:{item.Id}")
            {
                Name = $"DeleteButton{item.Id}",
                FontSize = fs,
                Explain = "このタスクの削除確認ダイアログを開くボタン",
            }
        )
        {
            Name = $"TaskRow{item.Id}",
        };
    }

    /// <summary>UI イベント(Dispatch の ID)をロジック操作へ変換する。</summary>
    private void Dispatch(string eventId)
    {
        var parts = eventId.Split(':');
        switch (parts[0])
        {
            case EvAdd:
                // 再構築で LineEdit が消える前に値を読む(D-035 の LineEdit 方針)
                AddTask(ReadLineEdit("NewTaskInput"));
                break;
            case EvToggle:
                ToggleTask(int.Parse(parts[1], CultureInfo.InvariantCulture));
                break;
            case EvEdit:
                if (_logic.BeginEdit(int.Parse(parts[1], CultureInfo.InvariantCulture)))
                {
                    RefreshView();
                }
                break;
            case EvCommitEdit:
                CommitEdit(ReadLineEdit("EditInput"));
                break;
            case EvCancelEdit:
                _logic.CancelEdit();
                RefreshView();
                break;
            case EvDelete:
                RequestDelete(int.Parse(parts[1], CultureInfo.InvariantCulture));
                break;
            case EvConfirmDelete:
                ConfirmDelete();
                break;
            case EvCancelDelete:
                _logic.CancelDelete();
                _logger.ZLogInformation($"削除をキャンセル");
                RefreshView();
                break;
            case EvFilter:
                SetFilter(Enum.Parse<TodoFilter>(parts[1]));
                break;
            case EvReorder:
                ReorderVisible(
                    int.Parse(parts[1], CultureInfo.InvariantCulture),
                    int.Parse(parts[2], CultureInfo.InvariantCulture)
                );
                break;
            case EvFontSize:
                // 値はイベントに載らないので、破棄前のスライダーから直接読む(D-060)
                if (_uiRoot?.FindChild("FontSizeSlider", true, false) is Godot.Range slider)
                {
                    SetFontSize((int)slider.Value);
                }
                break;
        }
    }

    // ---- ロジック操作(UI イベントと Statee コマンドの共通経路) ----

    private void AddTask(string title)
    {
        var id = _logic.Add(title);
        if (id is not null)
        {
            _logger.ZLogInformation($"追加 #{id}「{_logic.Items[^1].Title}」");
            RefreshView();
        }
    }

    private void ToggleTask(int id)
    {
        if (_logic.Toggle(id))
        {
            var item = _logic.Items.First(x => x.Id == id);
            _logger.ZLogInformation($"トグル #{id} → {(item.Completed ? "done" : "todo")}");
            RefreshView();
        }
    }

    private void CommitEdit(string title)
    {
        var id = _logic.EditingId;
        if (_logic.CommitEdit(title))
        {
            _logger.ZLogInformation($"編集確定 #{id}「{title.Trim()}」");
            RefreshView();
        }
    }

    private void RequestDelete(int id)
    {
        if (_logic.RequestDelete(id))
        {
            _logger.ZLogInformation($"削除確認ダイアログを表示 #{id}");
            RefreshView();
        }
    }

    private void ConfirmDelete()
    {
        var id = _logic.PendingDeleteId;
        if (_logic.ConfirmDelete())
        {
            _logger.ZLogInformation($"削除 #{id}");
            RefreshView();
        }
    }

    private void SetFilter(TodoFilter filter)
    {
        if (_logic.SetFilter(filter))
        {
            _logger.ZLogInformation($"フィルタ変更 → {filter}");
            RefreshView();
        }
    }

    private void SetFontSize(int size)
    {
        _logic.SetFontSize(size);
        _logger.ZLogInformation($"文字サイズ変更 → {_logic.FontSize}");
        RefreshView();
    }

    /// <summary>表示リスト内の並び替え(from/to は VisibleItems のインデックス)を Items の移動へ写す。</summary>
    private void ReorderVisible(int from, int to)
    {
        var visible = _logic.VisibleItems;
        if (from < 0 || from >= visible.Count || to < 0 || to >= visible.Count)
        {
            return;
        }
        var id = visible[from].Id;
        var toIndex = _logic.Items.ToList().FindIndex(x => x.Id == visible[to].Id);
        if (_logic.Move(id, toIndex))
        {
            _logger.ZLogInformation($"並び替え #{id} → {toIndex}");
            RefreshView();
        }
    }

    /// <summary>状態変更後に State・UI へ反映する。UI は全破棄・全再構築(D-035)。</summary>
    private void RefreshView()
    {
        _state.Update(_logic);
        _uiTree = BuildUi(_logic);
        _uiRoot?.QueueFree();
        _uiRoot = UiRenderer.Render(_uiTree, Dispatch);
        _uiLayer.AddChild(_uiRoot);
        // アンカーはツリーに入って親サイズが確定してから設定する
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiSnapshot = UiTree.Describe(_uiTree);
    }

    /// <summary>Name で LineEdit の現在値を読む(D-038。Declaree は値バインディングを持たない)。</summary>
    private string ReadLineEdit(string name) =>
        (_uiRoot?.FindChild(name, true, false) as Godot.LineEdit)?.Text ?? "";

    private object TodoResult() =>
        new
        {
            Count = _logic.Items.Count,
            VisibleCount = _logic.VisibleItems.Count,
            Filter = _logic.Filter.ToString(),
            IsDialogOpen = _logic.IsDialogOpen,
            FontSize = _logic.FontSize,
        };

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        // Declaree の UI(幾何 Rect 込みスナップショット)を State として公開する(D-035)
        host.RegisterStateProvider(new UiStateProvider("ui/tree", () => _uiSnapshot));
        host.RegisterStateProvider(KeyBindingTable.CreateInputStateProvider(_keyBindings));
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        // 状態を変えるコマンドはメインスレッドで実行する
        host.RegisterMainThreadCommand(
            "add",
            args =>
            {
                AddTask(args.GetString("title") ?? "");
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "toggle",
            args =>
            {
                ToggleTask(args.GetInt("id", -1));
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "filter",
            args =>
            {
                SetFilter(Enum.Parse<TodoFilter>(args.GetString("filter") ?? "All", true));
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "move",
            args =>
            {
                if (_logic.Move(args.GetInt("id", -1), args.GetInt("to", -1)))
                {
                    RefreshView();
                }
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "edit",
            args =>
            {
                if (_logic.BeginEdit(args.GetInt("id", -1)))
                {
                    RefreshView();
                }
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "commit",
            args =>
            {
                CommitEdit(args.GetString("title") ?? "");
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "delete",
            args =>
            {
                RequestDelete(args.GetInt("id", -1));
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "confirm",
            _ =>
            {
                ConfirmDelete();
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "cancel",
            _ =>
            {
                _logic.CancelDelete();
                RefreshView();
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "fontsize",
            args =>
            {
                SetFontSize(args.GetInt("size", TodoLogic.DefaultFontSize));
                return TodoResult();
            }
        );
        host.RegisterMainThreadCommand(
            "type",
            args =>
            {
                // LineEdit へ直接文字列を入れる(name 指定)。IME を含む生キー注入は対象外
                var name = args.GetString("name") ?? "NewTaskInput";
                if (_uiRoot?.FindChild(name, true, false) is not Godot.LineEdit lineEdit)
                {
                    throw new InvalidOperationException($"LineEdit が見つからない: {name}");
                }
                lineEdit.Text = args.GetString("text") ?? "";
                return new { Name = name, Text = lineEdit.Text };
            }
        );
        host.RegisterMainThreadCommand(
            "click",
            args =>
            {
                // name 指定なら ui/tree の Rect から中心を導出する(D-038)。
                // 実際の入力経路(PushInput)を通るため、モーダルに隠れた UI には正しく「効かない」
                var position = args.GetString("name") is { } name
                    ? CenterOf(name)
                    : new Vector2(args.GetInt("x", 0), args.GetInt("y", 0));
                PushClick(position);
                _logger.ZLogInformation($"click x={position.X} y={position.Y}");
                return new { X = position.X, Y = position.Y };
            }
        );
        host.RegisterMainThreadCommand(
            "drag",
            args =>
            {
                // name 指定の2要素間をドラッグする(ReorderList の実入力経路検証用)
                var fromName =
                    args.GetString("from")
                    ?? throw new InvalidOperationException("from を指定すること");
                var toName =
                    args.GetString("to")
                    ?? throw new InvalidOperationException("to を指定すること");
                var from = CenterOf(fromName);
                var to = CenterOf(toName);
                PushDrag(from, to);
                _logger.ZLogInformation($"drag {fromName} → {toName}");
                return new
                {
                    FromX = from.X,
                    FromY = from.Y,
                    ToX = to.X,
                    ToY = to.Y,
                };
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
    /// UI のヒットテストと Overlay の入力遮断の両方を通る。
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

    /// <summary>実際の入力経路でドラッグ(押下 → 移動 → 解放)を再現する。</summary>
    private void PushDrag(Vector2 from, Vector2 to)
    {
        var viewport = GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = from, GlobalPosition = from });
        viewport.PushInput(
            new InputEventMouseButton
            {
                Position = from,
                GlobalPosition = from,
                ButtonIndex = MouseButton.Left,
                Pressed = true,
            }
        );
        viewport.PushInput(new InputEventMouseMotion { Position = to, GlobalPosition = to });
        viewport.PushInput(
            new InputEventMouseButton
            {
                Position = to,
                GlobalPosition = to,
                ButtonIndex = MouseButton.Left,
                Pressed = false,
            }
        );
    }
}
