using System;
using System.Collections.Generic;
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

    /// <summary>論理座標→描画座標の基本倍率(カメラズーム 1.0 のとき)。</summary>
    private const float Zoom = 3f;

    /// <summary>画面中心の描画座標(ウィンドウ 960x540)。</summary>
    private static readonly Vector2 ScreenCenter = new(480f, 270f);

    /// <summary>カメラをプレイヤーからカーソル側へ寄せる割合(非構え / 構え)。</summary>
    private const float LookAheadWeight = 0.25f;
    private const float LookAheadWeightAds = 0.45f;

    /// <summary>構え(右クリック)中のズーム倍率。覗き込みの 2D 翻訳。</summary>
    private const float AdsZoom = 1.15f;

    /// <summary>ヒットマーカーの表示フレーム数。</summary>
    private const int HitMarkerFrames = 12;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();

    private BattleLogic _logic = null!;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    private Texture2D _playerTexture = null!;
    private AudioStreamPlayer _shotPlayer = null!;

    // カメラ(論理座標系。エイム側へ寄り、構えで少し拡大する)
    private System.Numerics.Vector2 _camPos;
    private float _camZoom = 1f;

    // ヒット演出(すべて表現なので Godot 層に置く。ロジックの Events から駆動する)
    private int _hitstopFrames;
    private int _targetFlashFrames;
    private readonly List<(System.Numerics.Vector2 Pos, int Frames)> _hitMarkers = [];

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = StateeLogging.CreateLoggerFactory(buffer);
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new BattleLogic(new BattleConfig(), CmdlineArgs.ParseInt("--seed=", DefaultSeed));
        _camPos = _logic.PlayerPos;

        LoadAssets();
        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"MessBreak 起動 seed={_logic.Seed}");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        UpdateCamera();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = 0; i < _hitMarkers.Count; i++)
        {
            _hitMarkers[i] = _hitMarkers[i] with { Frames = _hitMarkers[i].Frames - 1 };
        }
        _hitMarkers.RemoveAll(m => m.Frames <= 0);
        if (_targetFlashFrames > 0)
        {
            _targetFlashFrames--;
        }
        if (_time.IsFrozen)
        {
            return;
        }
        // ヒットストップ(命中の重み付け)。論理を数フレーム止めるだけの演出
        if (_hitstopFrames > 0)
        {
            _hitstopFrames--;
            return;
        }
        _logic.Tick(ReadHumanInput());
        _time.OnFrame();
        RefreshView();
    }

    /// <summary>
    /// カメラをプレイヤーとカーソルの間へ置き、エイムした方へ視界が伸びるようにする。
    /// 構え(右クリック)中は寄りを強め、わずかにズームイン(TPS の覗き込みの 2D 翻訳)。
    /// </summary>
    private void UpdateCamera()
    {
        var config = _logic.Config;
        var ads = Input.IsMouseButtonPressed(MouseButton.Right);
        var aimPoint = ToLogic(GetGlobalMousePosition());
        var weight = ads ? LookAheadWeightAds : LookAheadWeight;
        var desired = _logic.PlayerPos + (aimPoint - _logic.PlayerPos) * weight;

        var zoomTarget = ads ? AdsZoom : 1f;
        _camZoom += (zoomTarget - _camZoom) * 0.1f;

        // 画面が部屋の外を映さない範囲にクランプ
        var halfW = ScreenCenter.X / ScaleFactor;
        var halfH = ScreenCenter.Y / ScaleFactor;
        desired = new System.Numerics.Vector2(
            Math.Clamp(desired.X, halfW, config.RoomWidth - halfW),
            Math.Clamp(desired.Y, halfH, config.RoomHeight - halfH)
        );
        _camPos += (desired - _camPos) * 0.12f;
    }

    public override void _Draw()
    {
        var config = _logic.Config;

        // 部屋
        var roomTopLeft = ToScreen(System.Numerics.Vector2.Zero);
        DrawRect(
            new Rect2(
                roomTopLeft,
                new Vector2(config.RoomWidth * ScaleFactor, config.RoomHeight * ScaleFactor)
            ),
            new Color(0.12f, 0.10f, 0.14f)
        );

        // 的(残 HP で色を濃くする。被弾直後は白フラッシュ。リスポーン待ち中は非表示)
        if (_logic.TargetHp > 0)
        {
            var hpRatio = _logic.TargetHp / (float)config.TargetMaxHp;
            var targetColor =
                _targetFlashFrames > 0
                    ? new Color(1f, 1f, 1f)
                    : new Color(0.55f, 0.25f, 0.6f) * hpRatio + new Color(0.3f, 0.15f, 0.3f);
            DrawCircle(ToScreen(_logic.TargetPos), config.TargetRadius * ScaleFactor, targetColor);
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
        var spriteSize = _playerTexture.GetSize() * ScaleFactor;
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
                config.BulletRadius * ScaleFactor,
                new Color(1f, 0.85f, 0.4f)
            );
        }

        // ヒットマーカー(命中位置に広がって消えるリング)
        foreach (var (pos, frames) in _hitMarkers)
        {
            var t = 1f - frames / (float)HitMarkerFrames;
            DrawArc(
                ToScreen(pos),
                (4f + 8f * t) * ScaleFactor,
                0f,
                Mathf.Tau,
                24,
                new Color(1f, 1f, 1f, 1f - t),
                width: 2f
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

    /// <summary>論理座標→描画座標の実効倍率(基本倍率 × カメラズーム)。</summary>
    private float ScaleFactor => Zoom * _camZoom;

    /// <summary>描画座標を論理座標へ写す(マウス位置の変換用)。カメラを考慮する。</summary>
    private System.Numerics.Vector2 ToLogic(Vector2 screen) =>
        new(
            (screen.X - ScreenCenter.X) / ScaleFactor + _camPos.X,
            (screen.Y - ScreenCenter.Y) / ScaleFactor + _camPos.Y
        );

    /// <summary>論理座標(左上原点)を描画座標へ写す。カメラを考慮する。</summary>
    private Vector2 ToScreen(System.Numerics.Vector2 position) =>
        new(
            (position.X - _camPos.X) * ScaleFactor + ScreenCenter.X,
            (position.Y - _camPos.Y) * ScaleFactor + ScreenCenter.Y
        );

    /// <summary>tick 後の状態を State と描画へ反映し、イベントを音・演出へ翻訳する。</summary>
    private void RefreshView()
    {
        foreach (var battleEvent in _logic.Events)
        {
            switch (battleEvent.Kind)
            {
                case BattleEventKind.BulletFired:
                    _shotPlayer.Play();
                    break;
                case BattleEventKind.TargetHit:
                    _hitMarkers.Add((battleEvent.Pos, HitMarkerFrames));
                    _targetFlashFrames = 4;
                    _hitstopFrames = 2;
                    break;
                case BattleEventKind.TargetKilled:
                    _hitstopFrames = 6;
                    break;
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
