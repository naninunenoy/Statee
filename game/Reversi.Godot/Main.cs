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
/// R-3 段階: コマンド駆動(start / place / back)のみ。描画・クリック入力は R-4 で作る。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly ReversiGame _game = new();
    private readonly BoardState _boardState = new();
    private readonly TurnState _turnState = new();

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

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"Reversi 起動");
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
        // R-4 までのプレースホルダ描画: 盤面をテキストで出す
        var font = ThemeDB.FallbackFont;
        DrawString(
            font,
            new Vector2(16, 32),
            $"Reversi  phase={_game.Phase}  turn={_game.CurrentPlayer}  move={_game.MoveCount}",
            fontSize: 20
        );
        if (_game.Phase != GamePhase.Title)
        {
            for (var y = 0; y < Board.Size; y++)
            {
                var line = "";
                for (var x = 0; x < Board.Size; x++)
                {
                    line += _game.Board[x, y] switch
                    {
                        Disc.Black => "B ",
                        Disc.White => "W ",
                        _ => ". ",
                    };
                }
                DrawString(font, new Vector2(16, 72 + y * 24), line, fontSize: 20);
            }
        }
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    /// <summary>状態変更後に State と描画へ反映する。</summary>
    private void RefreshView()
    {
        _boardState.Update(_game.Board);
        _turnState.Update(_game);
        QueueRedraw();
    }

    private object TurnResult() =>
        new
        {
            Phase = _game.Phase.ToString(),
            CurrentPlayer = _game.CurrentPlayer.ToString(),
            MoveCount = _game.MoveCount,
            Winner = _game.Winner.ToString(),
        };

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_boardState);
        host.RegisterStateProvider(_turnState);
        host.RegisterStateProvider(KeyBindingTable.CreateInputStateProvider(_keyBindings));
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        // ゲーム状態を変えるコマンドはメインスレッドで実行する
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
                    throw new InvalidOperationException(
                        "ネット対戦は未実装(D-050)。LocalTwoPlayer を指定すること"
                    );
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
        _server = new StateeTcpServer(host, CmdlineArgs.ParseInt("--port=", DefaultPort));
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }
}
