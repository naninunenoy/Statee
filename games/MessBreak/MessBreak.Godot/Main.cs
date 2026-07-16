using System;
using Godot;
using MessBreak.Logic;
using Microsoft.Extensions.Logging;
using Statee.Core;
using Statee.Godot;
using ZLogger;

namespace MessBreak;

/// <summary>
/// MessBreak の Godot 層エントリポイント。描画・入力→TickInput 変換・Statee 配線
/// だけを担い、ゲームルールはすべて MessBreak.Logic に置く(docs/USING.md「境界の掟」)。
/// 論理は _PhysicsProcess(60Hz)で 1 Tick ずつ進む固定タイムステップ(ShootingGame の D-048 と同型)。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;

    /// <summary>tick コマンド1回で進められる上限(暴走防止。60Hz の1分ぶん)。</summary>
    private const int MaxTickFrames = 3600;

    /// <summary>論理座標(320x180)→描画座標(960x540)の倍率。</summary>
    private const float Zoom = 3f;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();

    private BattleLogic _logic = null!;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = StateeLogging.CreateLoggerFactory(buffer);
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new BattleLogic(new BattleConfig(), CmdlineArgs.ParseInt("--seed=", DefaultSeed));

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"MessBreak 起動 seed={_logic.Seed}");
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
        var config = _logic.Config;

        // 部屋
        DrawRect(
            new Rect2(0, 0, config.RoomWidth * Zoom, config.RoomHeight * Zoom),
            new Color(0.12f, 0.10f, 0.14f)
        );

        // 敵(Windup 中は白く点滅させて予備動作を見せる)。向きはプレイヤーへの視線
        if (_logic.EnemyAction != EnemyAction.Dead)
        {
            var enemyColor =
                _logic.EnemyAction == EnemyAction.Windup
                    ? new Color(0.95f, 0.9f, 0.9f)
                    : new Color(0.55f, 0.25f, 0.6f);
            DrawCircle(ToScreen(_logic.EnemyPos), config.EnemyRadius * Zoom, enemyColor);
            if (_logic.EnemyAction != EnemyAction.Idle)
            {
                var toPlayer = _logic.PlayerPos - _logic.EnemyPos;
                if (toPlayer != System.Numerics.Vector2.Zero)
                {
                    DrawNose(
                        _logic.EnemyPos,
                        System.Numerics.Vector2.Normalize(toPlayer),
                        config.EnemyRadius,
                        new Color(0.9f, 0.75f, 0.95f)
                    );
                }
            }
        }

        // プレイヤー(ドッジ中は半透明)。向き=エイム方向は銃身と細い照準線で見せる
        var playerColor =
            _logic.PlayerAction == PlayerAction.Dodge
                ? new Color(0.85f, 0.29f, 0.37f, 0.5f)
                : new Color(0.85f, 0.29f, 0.37f);
        DrawLine(
            ToScreen(_logic.PlayerPos),
            ToScreen(_logic.PlayerPos + _logic.PlayerFacing * 60f),
            new Color(1f, 1f, 1f, 0.15f),
            width: 1f
        );
        DrawCircle(ToScreen(_logic.PlayerPos), config.PlayerRadius * Zoom, playerColor);
        DrawNose(
            _logic.PlayerPos,
            _logic.PlayerFacing,
            config.PlayerRadius,
            new Color(1f, 0.9f, 0.75f)
        );

        // 弾
        foreach (var bullet in _logic.Bullets)
        {
            DrawCircle(
                ToScreen(bullet.Pos),
                config.BulletRadius * Zoom,
                new Color(1f, 0.85f, 0.4f)
            );
        }

        // HUD
        var phaseText = _logic.Phase switch
        {
            BattlePhase.Victory => "  VICTORY!",
            BattlePhase.Defeat => "  DEFEAT...",
            _ => "",
        };
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 32),
            $"HP {_logic.PlayerHp}/{_logic.Config.PlayerMaxHp}  敵HP {_logic.EnemyHp}/{_logic.Config.EnemyMaxHp}  tick={_logic.TickCount}{phaseText}",
            fontSize: 20
        );
    }

    public override void _ExitTree()
    {
        StopStateeServer();
        _loggerFactory?.Dispose();
    }

    /// <summary>人間プレイの入力(押されているキーの集合)を TickInput へ写す。</summary>
    private TickInput ReadHumanInput()
    {
        var dir = System.Numerics.Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.Left) || Input.IsPhysicalKeyPressed(Key.A))
        {
            dir.X -= 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Right) || Input.IsPhysicalKeyPressed(Key.D))
        {
            dir.X += 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Up) || Input.IsPhysicalKeyPressed(Key.W))
        {
            dir.Y -= 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Down) || Input.IsPhysicalKeyPressed(Key.S))
        {
            dir.Y += 1f;
        }
        // エイムはマウスカーソル方向(ツインスティックの照準軸)
        var aim = ToLogic(GetGlobalMousePosition()) - _logic.PlayerPos;
        return new TickInput(
            dir,
            aim,
            Fire: Input.IsMouseButtonPressed(MouseButton.Left)
                || Input.IsPhysicalKeyPressed(Key.Z)
                || Input.IsPhysicalKeyPressed(Key.J),
            Dodge: Input.IsMouseButtonPressed(MouseButton.Right)
                || Input.IsPhysicalKeyPressed(Key.X)
                || Input.IsPhysicalKeyPressed(Key.K)
        );
    }

    /// <summary>中心から向きを示す短い銃身(ノーズ)を描く。円だけでは向きが分からない対策。</summary>
    private void DrawNose(
        System.Numerics.Vector2 center,
        System.Numerics.Vector2 dir,
        float radius,
        Color color
    )
    {
        DrawLine(
            ToScreen(center + dir * radius * 0.5f),
            ToScreen(center + dir * radius * 1.8f),
            color,
            width: 3f
        );
    }

    /// <summary>描画座標を論理座標へ写す(マウス位置の変換用)。</summary>
    private static System.Numerics.Vector2 ToLogic(Vector2 screen) =>
        new(screen.X / Zoom, screen.Y / Zoom);

    /// <summary>論理座標(320x180・左上原点)を描画座標へ写す。</summary>
    private static Vector2 ToScreen(System.Numerics.Vector2 position) =>
        new(position.X * Zoom, position.Y * Zoom);

    /// <summary>tick 後の状態を State と描画へ反映する。</summary>
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
        // 例: send --command tick --arg frames=30,input=right+fire,aimx=1,aimy=-0.5
        // (CLI の --arg は複数指定をカンマで区切るため、入力トークンは + で連結する)
        host.RegisterMainThreadCommand(
            "tick",
            args =>
            {
                var frames = Math.Clamp(args.GetInt("frames", 1), 1, MaxTickFrames);
                var aim = new System.Numerics.Vector2(
                    ParseFloat(args.GetString("aimx")),
                    ParseFloat(args.GetString("aimy"))
                );
                var input = ParseInput(args.GetString("input") ?? "", aim);
                for (var i = 0; i < frames; i++)
                {
                    _logic.Tick(input);
                    _time.OnFrame();
                }
                RefreshView();
                _logger.ZLogInformation(
                    $"tick {frames} → tick={_logic.TickCount} phase={_logic.Phase}"
                );
                return new
                {
                    _logic.TickCount,
                    Phase = _logic.Phase.ToString(),
                    _logic.PlayerHp,
                    _logic.EnemyHp,
                };
            }
        );
        StartStateeServer(host);
    }

    // TCP 待ち受け(外部 CLI/MCP の入口)は Main.StateeServer.cs に隔離している。
    // ExportRelease ではファイルごとビルドから除外され、この呼び出しは丸ごと消える(D-065)
    partial void StartStateeServer(StateeHost host);

    partial void StopStateeServer();

    /// <summary>tick コマンドの aimx / aimy 引数(未指定は 0)を読む。</summary>
    private static float ParseFloat(string? value) =>
        value is null ? 0f : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"right+fire" のような + 区切りトークンと aim を TickInput へ写す。</summary>
    private static TickInput ParseInput(string tokens, System.Numerics.Vector2 aim)
    {
        var dir = System.Numerics.Vector2.Zero;
        var fire = false;
        var dodge = false;
        foreach (var token in tokens.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "-":
                    break; // 無入力
                case "left":
                    dir.X -= 1f;
                    break;
                case "right":
                    dir.X += 1f;
                    break;
                case "up":
                    dir.Y -= 1f;
                    break;
                case "down":
                    dir.Y += 1f;
                    break;
                case "fire":
                    fire = true;
                    break;
                case "dodge":
                    dodge = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"未知の入力トークン '{token}'(left/right/up/down/fire/dodge)"
                    );
            }
        }
        return new TickInput(dir, aim, fire, dodge);
    }
}
