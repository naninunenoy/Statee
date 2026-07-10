using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using RogueGame.Logic;
using Statee.Core;
using Statee.Remote;
using ZLogger;

namespace RogueGame;

/// <summary>
/// RogueGame の Godot 層エントリポイント。描画(emoji タイル+FoW)と入力だけを担い、
/// 規則(移動・戦闘・アイテム・クリア)はすべて RogueGame.Logic に委ねる(D-044)。
/// Statee を組み込み、move / use コマンドと State(game/rogue)を外部へ公開する。
/// ターン制のため物理は使わず、描画は1ターン1回の全再描画で足りる。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;
    private const int TileSize = 24;
    private const int HudHeight = 48;
    private const int EmojiFontSize = 19;
    private const int HudFontSize = 20;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly RogueState _state = new();
    private readonly TimeControl _time = new();

    /// <summary>フロアごとの探索済みマス。FoW は描画層の演出(State は全公開)。</summary>
    private readonly Dictionary<int, HashSet<GridPos>> _explored = [];

    private RogueLogic _logic = null!;
    private HashSet<GridPos> _visible = [];
    private FontVariation _font = null!;
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    /// <summary>キー入力 → アクションの配線1件(D-039)。この表が
    /// _UnhandledInput の処理と game/input State の両方の情報源になる。</summary>
    private sealed record KeyBinding(Key Key, string Publishes, string Explain, Action Publish);

    private KeyBinding[] _keyBindings = [];

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new BufferLoggerProvider(buffer));
            builder.AddZLoggerConsole();
        });
        _logger = _loggerFactory.CreateLogger<Main>();

        // headless では project.godot の window サイズが反映されないため実行時に明示する
        GetWindow().Size = new Vector2I(
            RogueConfig.MapWidth * TileSize,
            HudHeight + RogueConfig.MapHeight * TileSize
        );

        // 同梱の Noto Color Emoji(サブセット)を既定フォントのフォールバックに重ねる。
        // 地形・HUD 文字は既定フォント、絵文字だけがフォールバックで描かれる
        var emoji = GD.Load<FontFile>("res://assets/fonts/NotoColorEmoji.subset.ttf");
        _font = new FontVariation { BaseFont = ThemeDB.FallbackFont, Fallbacks = [emoji] };

        _logic = new RogueLogic(ParseIntArg("--seed=", DefaultSeed));
        _keyBindings =
        [
            BindKey(Key.Up, "move north", "北(上)へ1マス移動・攻撃", () => Act(Direction.North)),
            BindKey(Key.Down, "move south", "南(下)へ1マス移動・攻撃", () => Act(Direction.South)),
            BindKey(Key.Left, "move west", "西(左)へ1マス移動・攻撃", () => Act(Direction.West)),
            BindKey(Key.Right, "move east", "東(右)へ1マス移動・攻撃", () => Act(Direction.East)),
            BindKey(Key.W, "move north", "北(上)へ1マス移動・攻撃", () => Act(Direction.North)),
            BindKey(Key.S, "move south", "南(下)へ1マス移動・攻撃", () => Act(Direction.South)),
            BindKey(Key.A, "move west", "西(左)へ1マス移動・攻撃", () => Act(Direction.West)),
            BindKey(Key.D, "move east", "東(右)へ1マス移動・攻撃", () => Act(Direction.East)),
            BindKey(
                Key.U,
                "use potion",
                "🧪 ポーションを使う(HP 回復)",
                () => ActUse(ItemKind.Potion)
            ),
        ];

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation(
            $"RogueGame 起動 seed={ParseIntArg("--seed=", DefaultSeed)} floor={_logic.CurrentFloor}"
        );
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
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }
        foreach (var binding in _keyBindings)
        {
            if (binding.Key == keyEvent.Keycode)
            {
                binding.Publish();
                return;
            }
        }
    }

    public override void _Draw()
    {
        DrawHud();
        DrawDungeon();
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    /// <summary>プレイヤーのアクション(移動・攻撃)1ターン。</summary>
    private void Act(Direction direction)
    {
        _logic.Move(direction);
        RefreshView();
    }

    /// <summary>プレイヤーのアクション(アイテム使用)1ターン。</summary>
    private void ActUse(ItemKind kind)
    {
        _logic.UseItem(kind);
        RefreshView();
    }

    /// <summary>ターン結果を視界・State・描画へ反映する。</summary>
    private void RefreshView()
    {
        _visible = ComputeVisible();
        Explored().UnionWith(_visible);
        UpdateState();
        QueueRedraw();
    }

    private HashSet<GridPos> ComputeVisible()
    {
        var map = _logic.Map;
        var visible = new HashSet<GridPos>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var pos = new GridPos(x, y);
                if (LineOfSight.CanSee(map, _logic.PlayerPos, pos))
                {
                    visible.Add(pos);
                }
            }
        }
        return visible;
    }

    private HashSet<GridPos> Explored() =>
        _explored.TryGetValue(_logic.CurrentFloor, out var explored)
            ? explored
            : _explored[_logic.CurrentFloor] = [];

    private void UpdateState()
    {
        var map = _logic.Map;
        var rows = new string[map.Height];
        for (var y = 0; y < map.Height; y++)
        {
            var row = new char[map.Width];
            for (var x = 0; x < map.Width; x++)
            {
                row[x] = map[new GridPos(x, y)] switch
                {
                    Tile.Wall => '#',
                    Tile.Floor => '.',
                    Tile.StairsUp => '<',
                    Tile.StairsDown => '>',
                    _ => '?',
                };
            }
            rows[y] = new string(row);
        }
        _state.Update(
            _logic.CurrentFloor,
            _logic.PlayerPos.X,
            _logic.PlayerPos.Y,
            _logic.PlayerHp,
            _logic.PlayerAttack,
            [.. _logic.Inventory.Select(kind => kind.ToString())],
            _logic.HasGem,
            _logic.IsCleared,
            _logic.IsGameOver,
            rows,
            [
                .. _logic.Enemies.Select(enemy => new RogueState.EnemyEntry(
                    enemy.Id.AsPrimitive(),
                    enemy.Pos.X,
                    enemy.Pos.Y,
                    enemy.Hp
                )),
            ],
            [
                .. _logic.Items.Select(item => new RogueState.ItemEntry(
                    item.Id.AsPrimitive(),
                    item.Kind.ToString(),
                    item.Pos.X,
                    item.Pos.Y
                )),
            ]
        );
    }

    private void DrawHud()
    {
        var potions = _logic.Inventory.Count(kind => kind == ItemKind.Potion);
        var status =
            _logic.IsCleared ? "🎉 CLEAR!"
            : _logic.IsGameOver ? "💀 GAME OVER"
            : "";
        var gem = _logic.HasGem ? "💎" : "";
        DrawString(
            _font,
            new Vector2(8, 32),
            $"B{_logic.CurrentFloor}F  ❤ {_logic.PlayerHp}/{RogueConfig.PlayerHp}  ⚔ {_logic.PlayerAttack}  🧪x{potions}  {gem}  {status}",
            fontSize: HudFontSize
        );
    }

    private void DrawDungeon()
    {
        var map = _logic.Map;
        var explored = Explored();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var pos = new GridPos(x, y);
                if (!explored.Contains(pos))
                {
                    continue;
                }
                var inSight = _visible.Contains(pos);
                DrawTile(pos, map[pos], inSight);
            }
        }
        // Entity は視界内のみ描く(FoW)。地形の記憶とは扱いが違う(D-044)
        foreach (var item in _logic.Items.Where(item => _visible.Contains(item.Pos)))
        {
            DrawCellText(item.Pos, EmojiOf(item.Kind));
        }
        foreach (var enemy in _logic.Enemies.Where(enemy => _visible.Contains(enemy.Pos)))
        {
            DrawCellText(enemy.Pos, _logic.HasGem ? "👹" : "🐀");
        }
        DrawCellText(_logic.PlayerPos, "🧙");
    }

    private void DrawTile(GridPos pos, Tile tile, bool inSight)
    {
        var rect = new Rect2(pos.X * TileSize, HudHeight + pos.Y * TileSize, TileSize, TileSize);
        // 視界外の探索済み地形は暗く描く(記憶表示)
        var dim = inSight ? 1f : 0.45f;
        var background =
            tile == Tile.Wall
                ? new Color(0.32f, 0.32f, 0.40f, dim)
                : new Color(0.13f, 0.14f, 0.17f, dim);
        DrawRect(rect, background);
        var glyph = tile switch
        {
            Tile.StairsUp => "<",
            Tile.StairsDown => ">",
            _ => null,
        };
        if (glyph is not null)
        {
            DrawCellText(pos, glyph, new Color(1f, 1f, 1f, dim));
        }
    }

    private void DrawCellText(GridPos pos, string text, Color? color = null)
    {
        DrawString(
            _font,
            new Vector2(pos.X * TileSize + 2, HudHeight + pos.Y * TileSize + TileSize - 5),
            text,
            fontSize: EmojiFontSize,
            modulate: color ?? Colors.White
        );
    }

    private static string EmojiOf(ItemKind kind) =>
        kind switch
        {
            ItemKind.Potion => "🧪",
            ItemKind.Sword => "🗡️",
            ItemKind.Gem => "💎",
            _ => "?",
        };

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        // キーバインドの State 公開(D-039)。配線表から導出するため実装と乖離しない
        var keyEntries = Array.ConvertAll(
            _keyBindings,
            binding => new
            {
                Key = binding.Key.ToString(),
                Publishes = binding.Publishes,
                Explain = binding.Explain,
            }
        );
        host.RegisterStateProvider(
            new SnapshotStateProvider("game/input", () => new { Keys = keyEntries })
        );
        host.RegisterTimeControl(_time);
        host.RegisterMainThreadCommand(
            "move",
            args =>
            {
                var name =
                    args.GetString("dir")
                    ?? throw new InvalidOperationException(
                        "dir を指定すること(north/south/west/east)"
                    );
                var direction = Enum.Parse<Direction>(name, ignoreCase: true);
                Act(direction);
                _logger.ZLogInformation(
                    $"move {direction} → ({_logic.PlayerPos.X},{_logic.PlayerPos.Y}) floor={_logic.CurrentFloor}"
                );
                return ActionResult();
            }
        );
        host.RegisterMainThreadCommand(
            "use",
            args =>
            {
                var name = args.GetString("item") ?? nameof(ItemKind.Potion);
                var kind = Enum.Parse<ItemKind>(name, ignoreCase: true);
                ActUse(kind);
                _logger.ZLogInformation($"use {kind} hp={_logic.PlayerHp}");
                return ActionResult();
            }
        );
        host.RegisterMainThreadCommand(
            "key",
            args =>
            {
                var name =
                    args.GetString("key")
                    ?? throw new InvalidOperationException("key を指定すること(例: up)");
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

    /// <summary>アクション系コマンドの応答。1ターン後の要点を返す。</summary>
    private object ActionResult() =>
        new
        {
            Floor = _logic.CurrentFloor,
            X = _logic.PlayerPos.X,
            Y = _logic.PlayerPos.Y,
            Hp = _logic.PlayerHp,
            HasGem = _logic.HasGem,
            IsCleared = _logic.IsCleared,
            IsGameOver = _logic.IsGameOver,
        };

    private KeyBinding BindKey(Key key, string publishes, string explain, Action publish) =>
        new(key, publishes, explain, publish);

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
}
