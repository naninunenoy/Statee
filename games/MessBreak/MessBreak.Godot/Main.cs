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

    private Texture2D _playerTexture = null!;
    private AudioStreamPlayer _shotPlayer = null!;

    /// <summary>効果音の発火検出用。これより大きい Id の弾が現れたら発射音を鳴らす。</summary>
    private int _lastBulletId;

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = StateeLogging.CreateLoggerFactory(buffer);
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new BattleLogic(new BattleConfig(), CmdlineArgs.ParseInt("--seed=", DefaultSeed));

        LoadAssets();
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

        // 的(残 HP で色を濃くする。リスポーン待ち中は非表示)
        if (_logic.TargetHp > 0)
        {
            var hpRatio = _logic.TargetHp / (float)config.TargetMaxHp;
            DrawCircle(
                ToScreen(_logic.TargetPos),
                config.TargetRadius * Zoom,
                new Color(0.55f, 0.25f, 0.6f) * hpRatio + new Color(0.3f, 0.15f, 0.3f)
            );
        }

        // プレイヤー(ドッジ中は半透明)。向き=エイム方向は銃身と細い照準線で見せる
        var playerTint =
            _logic.PlayerAction == PlayerAction.Dodge ? new Color(1f, 1f, 1f, 0.5f) : Colors.White;
        DrawLine(
            ToScreen(_logic.PlayerPos),
            ToScreen(_logic.PlayerPos + _logic.PlayerFacing * 60f),
            new Color(1f, 1f, 1f, 0.15f),
            width: 1f
        );
        var spriteSize = _playerTexture.GetSize() * Zoom;
        DrawTextureRect(
            _playerTexture,
            new Rect2(ToScreen(_logic.PlayerPos) - spriteSize / 2f, spriteSize),
            tile: false,
            playerTint
        );
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

        // HUD(命中統計。「当たる感」検証の指標)
        var accuracy = _logic.ShotCount == 0 ? 0f : 100f * _logic.HitCount / _logic.ShotCount;
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 32),
            $"shots={_logic.ShotCount}  hits={_logic.HitCount}  kills={_logic.KillCount}  acc={accuracy:0.#}%  tick={_logic.TickCount}",
            fontSize: 20
        );
    }

    public override void _ExitTree()
    {
        StopStateeServer();
        _loggerFactory?.Dispose();
    }

    /// <summary>
    /// スプライトと効果音を実行時ロードする。定義テキスト(art/*.sprite.txt, audio/*.sfx.txt)が
    /// 単一ソースで、生成物をゲームディレクトリから直接読む(Godot の import 経路を使わない)。
    /// </summary>
    private void LoadAssets()
    {
        // ドット絵は最近傍拡大で描く(にじみ防止)
        TextureFilter = TextureFilterEnum.Nearest;
        _playerTexture = ImageTexture.CreateFromImage(
            Image.LoadFromFile(ProjectSettings.GlobalizePath("res://../art/attacker.png"))
        );
        _shotPlayer = new AudioStreamPlayer
        {
            Stream = AudioStreamWav.LoadFromFile(
                ProjectSettings.GlobalizePath("res://../audio/shot.wav")
            ),
        };
        AddChild(_shotPlayer);
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
        // 構え(右クリック保持)中だけマウスカーソル方向を AimDir として送る。
        // 非構えは零を送り、ロジック側で移動方向を向く(docs/DESIGN.md「向き(構え)の仕様」)
        var aim = Input.IsMouseButtonPressed(MouseButton.Right)
            ? ToLogic(GetGlobalMousePosition()) - _logic.PlayerPos
            : System.Numerics.Vector2.Zero;
        return new TickInput(
            dir,
            aim,
            Fire: Input.IsMouseButtonPressed(MouseButton.Left)
                || Input.IsPhysicalKeyPressed(Key.Z)
                || Input.IsPhysicalKeyPressed(Key.J),
            Dodge: Input.IsPhysicalKeyPressed(Key.Space),
            Sprint: Input.IsPhysicalKeyPressed(Key.Shift)
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

    /// <summary>tick 後の状態を State と描画へ反映し、新規の弾があれば発射音を鳴らす。</summary>
    private void RefreshView()
    {
        foreach (var bullet in _logic.Bullets)
        {
            if (bullet.Id > _lastBulletId)
            {
                _lastBulletId = bullet.Id;
                _shotPlayer.Play();
            }
        }
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
                    $"tick {frames} → tick={_logic.TickCount} hits={_logic.HitCount}/{_logic.ShotCount}"
                );
                return new
                {
                    _logic.TickCount,
                    _logic.ShotCount,
                    _logic.HitCount,
                    _logic.KillCount,
                    _logic.TargetHp,
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
        var sprint = false;
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
                case "sprint":
                    sprint = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"未知の入力トークン '{token}'(left/right/up/down/fire/dodge/sprint)"
                    );
            }
        }
        return new TickInput(dir, aim, fire, dodge, sprint);
    }
}
