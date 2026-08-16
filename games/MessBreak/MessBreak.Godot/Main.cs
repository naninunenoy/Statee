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
/// 上下関係のある責務は独立レイヤーへ分離する(Main の目安はおおよそ 400 行):
/// GameCamera / BattleSprites / BattleView / HudView / TickInputReader。
/// 同層のコンパイル条件分岐だけ Main.StateeServer.cs(partial)に残す(D-065)。
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
    private readonly HudState _hudState = new();
    private readonly GameCamera _camera = new();
    private readonly HudView _hud = new();

    private BattleView _battleView = null!;
    private BattleLogic _logic = null!;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    /// <summary>ポーズメニュー(Esc)で論理 tick を止めているか。Statee の freeze とは独立。</summary>
    private bool _paused;

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        // OS カーソルは隠し、BattleView が自前のレティクルを描く
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        // UI バーが画面を食い潰さない程度の下限(headless では Window が無いので触らない)
        if (GetWindow() is { } window)
        {
            window.MinSize = new Vector2I(640, 360);
        }

        var buffer = new LogBuffer(1024);
        _loggerFactory = StateeLogging.CreateLoggerFactory(buffer);
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new BattleLogic(
            new BattleConfig(),
            Stages.Room1(),
            CmdlineArgs.ParseInt("--seed=", DefaultSeed)
        );

        _camera.SetViewportSize(GetViewportRect().Size);
        _camera.Position = _logic.PlayerPos;
        _battleView = new BattleView(_camera);
        AddChild(_battleView);
        _battleView.Bind(_logic, _paused);
        _battleView.LoadAssets();
        _battleView.ResetPresentation(_logic.PlayerPos);

        // 起動直後から実時間で tick が進むと接続タイミングで盤面が変わるため、
        // 再現シナリオでは --frozen で tick 0 から凍結した状態で始められる(D-073)
        if (CmdlineArgs.HasFlag("--frozen"))
        {
            _time.Freeze();
        }

        _hud.Build(this);
        _hud.BuildPauseMenu(this, TogglePause, RestartMission, () => GetTree().Quit());
        RefreshView();
        StartStatee(buffer);
        _logger.LogInformation("MessBreak 起動 seed={Seed}", _logic.Seed);
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        _camera.SetViewportSize(GetViewportRect().Size);
        _battleView.Bind(_logic, _paused);
        _battleView.UpdateCamera(GetGlobalMousePosition());
        _battleView.QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape })
        {
            TogglePause();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // ポーズ中は論理も演出タイマーも止める(Statee のコマンド処理だけ _Process で動き続ける)
        if (_paused)
        {
            return;
        }
        if (_time.IsFrozen)
        {
            return;
        }
        // ヒットストップ(命中の重み付け)。論理を数フレーム止めるだけの演出なので
        // 論理 tick ではなく physics フレームで数える(論理を止める側だから同期できない)
        if (_battleView.IsInHitstop)
        {
            _battleView.TickHitstop();
            return;
        }
        _battleView.Bind(_logic, _paused);
        _battleView.AdvanceEffectTimers();
        _logic.Tick(TickInputReader.ReadHuman(_camera, GetGlobalMousePosition(), _logic.PlayerPos));
        _time.OnFrame();
        RefreshView();
    }

    public override void _ExitTree()
    {
        StopStateeServer();
        _loggerFactory?.Dispose();
    }

    /// <summary>tick 後の状態を State と描画へ反映し、イベントを音・演出へ翻訳する。</summary>
    private void RefreshView()
    {
        _battleView.Bind(_logic, _paused);
        _battleView.ApplyEvents();
        _state.Update(_logic, _paused);
        _hud.Update(_logic, _camera, _battleView, _paused, _hudState);
        _battleView.QueueRedraw();
    }

    /// <summary>ポーズの開閉。開いている間は OS カーソルを出してメニューを操作させる。</summary>
    private void TogglePause()
    {
        _paused = !_paused;
        _hud.SetPaused(_paused);
        _battleView.Bind(_logic, _paused);
        _state.Update(_logic, _paused);
        _hud.Update(_logic, _camera, _battleView, _paused, _hudState);
        _battleView.QueueRedraw();
    }

    /// <summary>「はじめから」。同じ seed でロジックを作り直し、演出の残骸も消して再開する。</summary>
    private void RestartMission()
    {
        _logic = new BattleLogic(new BattleConfig(), Stages.Room1(), _logic.Seed);
        _battleView.Bind(_logic, _paused);
        _battleView.ResetPresentation(_logic.PlayerPos);
        TogglePause();
        RefreshView();
        _logger.ZLogInformation($"ミッションをはじめから(seed={_logic.Seed})");
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        host.RegisterStateProvider(_hudState);
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        // 継続入力つきで論理を進めるコマンド(エージェントのプレイ経路)。
        // freeze と組み合わせて「入力を指定して N Tick 進める」を実現する。
        // 例: send --command tick --arg frames=30,input=right+fire,aimx=1,aimy=-0.5
        // (CLI の --arg は複数指定をカンマで区切るため、入力トークンは + で連結する)
        host.RegisterTickCommand(
            _time,
            parseInput: args =>
            {
                var aim = new System.Numerics.Vector2(
                    TickInputReader.ParseFloat(args.GetString("aimx")),
                    TickInputReader.ParseFloat(args.GetString("aimy"))
                );
                // skillx/skilly はスキル爆心の絶対座標(省略時は向いている方向の射程いっぱい)
                var skillX = args.GetString("skillx");
                var skillY = args.GetString("skilly");
                System.Numerics.Vector2? aimPoint =
                    skillX is null && skillY is null
                        ? null
                        : new System.Numerics.Vector2(
                            TickInputReader.ParseFloat(skillX),
                            TickInputReader.ParseFloat(skillY)
                        );
                return TickInputReader.Parse(args.GetString("input") ?? "", aim, aimPoint);
            },
            step: input =>
            {
                _battleView.Bind(_logic, _paused);
                _battleView.AdvanceEffectTimers();
                _logic.Tick(input);
            },
            result: () =>
            {
                RefreshView();
                _logger.LogInformation(
                    "tick → tick={Tick} hits={Hits}/{Shots}",
                    _logic.TickCount,
                    _logic.HitCount,
                    _logic.ShotCount
                );
                return new
                {
                    _logic.TickCount,
                    _logic.ShotCount,
                    _logic.HitCount,
                    _logic.KillCount,
                    _logic.ZoneCaptured,
                    _logic.TurretPlaced,
                    _logic.BossAppeared,
                    _logic.MissionCleared,
                };
            },
            maxFramesPerCall: MaxTickFrames
        );
        StartStateeServer(host);
    }

    // TCP 待ち受け(外部 CLI/MCP の入口)は Main.StateeServer.cs に隔離している。
    // ExportRelease ではファイルごとビルドから除外され、この呼び出しは丸ごと消える(D-065)
    partial void StartStateeServer(StateeHost host);

    partial void StopStateeServer();
}
