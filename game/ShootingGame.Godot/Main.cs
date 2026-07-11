using System;
using Godot;
using Microsoft.Extensions.Logging;
using ShootingGame.Logic;
using Statee.Core;
using Statee.Godot;
using Statee.Remote;
using ZLogger;

namespace ShootingGame;

/// <summary>
/// ShootingGame の Godot 層エントリポイント。描画・入力→InputState 変換・Statee 配線
/// だけを担い、ゲームルールはすべて ShootingGame.Logic に置く(docs/USING.md「境界の掟」)。
/// 論理は _PhysicsProcess(60Hz)で 1 Tick ずつ進む固定タイムステップ(D-048)。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;

    /// <summary>tick コマンド1回で進められる上限(暴走防止。60Hz の1分ぶん)。</summary>
    private const int MaxTickFrames = 3600;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();

    private ShootingLogic _logic = null!;
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

        _logic = new ShootingLogic(CmdlineArgs.ParseInt("--seed=", DefaultSeed));

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"ShootingGame 起動 seed={_logic.Seed}");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_time.IsFrozen)
        {
            return;
        }
        _logic.Tick(ReadHumanInput());
        _time.OnFrame();
        RefreshView();
    }

    public override void _Draw()
    {
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 32),
            $"score={_logic.Score}  lives={_logic.Lives}  tick={_logic.TickCount}"
                + (_logic.IsGameOver ? "  GAME OVER" : ""),
            fontSize: 20
        );
        DrawCircle(ToScreen(_logic.PlayerPosition), _logic.Config.PlayerRadius, Colors.Cyan);
        foreach (var bullet in _logic.PlayerBullets)
        {
            DrawCircle(ToScreen(bullet.Position), _logic.Config.PlayerBulletRadius, Colors.White);
        }
        foreach (var enemy in _logic.Enemies)
        {
            DrawCircle(ToScreen(enemy.Position), _logic.Config.EnemyRadius, Colors.Orange);
        }
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
        _logic.Dispose();
    }

    /// <summary>人間プレイの入力(押されているキーの集合)を InputState へ写す。</summary>
    private static InputState ReadHumanInput() =>
        new(
            Left: Input.IsPhysicalKeyPressed(Key.Left),
            Right: Input.IsPhysicalKeyPressed(Key.Right),
            Up: Input.IsPhysicalKeyPressed(Key.Up),
            Down: Input.IsPhysicalKeyPressed(Key.Down),
            Shoot: Input.IsPhysicalKeyPressed(Key.Z) || Input.IsPhysicalKeyPressed(Key.Space)
        );

    /// <summary>論理座標(960x540・左上原点)を描画座標へ写す。現状は等倍。</summary>
    private static Vector2 ToScreen(System.Numerics.Vector2 position) =>
        new(position.X, position.Y);

    /// <summary>アクション後の状態を State と描画へ反映する。</summary>
    private void RefreshView()
    {
        _state.Update(_logic);
        QueueRedraw();
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        // 継続入力つきで論理を進めるコマンド(エージェントのプレイ経路)。
        // freeze と組み合わせて「入力を指定して N Tick 進める」を実現する。
        // 例: send --command tick --arg frames=30,input=right+shoot
        // (CLI の --arg は複数指定をカンマで区切るため、入力トークンは + で連結する)
        host.RegisterMainThreadCommand(
            "tick",
            args =>
            {
                var frames = Math.Clamp(args.GetInt("frames", 1), 1, MaxTickFrames);
                var input = ParseInput(args.GetString("input") ?? "");
                for (var i = 0; i < frames; i++)
                {
                    _logic.Tick(input);
                    _time.OnFrame();
                }
                RefreshView();
                _logger.ZLogInformation($"tick {frames} input={input} → tick={_logic.TickCount}");
                return new
                {
                    _logic.TickCount,
                    _logic.Score,
                    _logic.Lives,
                    _logic.IsGameOver,
                };
            }
        );
        _server = new StateeTcpServer(host, CmdlineArgs.ParseInt("--port=", DefaultPort));
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    /// <summary>"right+shoot" のような + 区切りトークンを InputState へ写す。</summary>
    private static InputState ParseInput(string tokens)
    {
        var input = new InputState();
        foreach (var token in tokens.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            input = token.Trim().ToLowerInvariant() switch
            {
                "left" => input with { Left = true },
                "right" => input with { Right = true },
                "up" => input with { Up = true },
                "down" => input with { Down = true },
                "shoot" => input with { Shoot = true },
                _ => throw new ArgumentException(
                    $"未知の入力トークン '{token}'(left/right/up/down/shoot)"
                ),
            };
        }
        return input;
    }
}
