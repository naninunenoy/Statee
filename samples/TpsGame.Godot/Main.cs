using System;
using System.Collections.Generic;
using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;
using Statee.Godot;
using TpsGame.Logic;
using ZLogger;

namespace TpsGame;

/// <summary>
/// TpsGame の Godot 層エントリポイント。描画・入力→InputState 変換・Statee 配線
/// だけを担い、ゲームルールはすべて TpsGame.Logic に置く(docs/USING.md「境界の掟」)。
/// 論理は _PhysicsProcess(60Hz)で 1 Tick ずつ進む固定タイムステップ。
/// </summary>
public partial class Main : Node3D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;
    private const int MaxTickFrames = 3600;

    /// <summary>voxcee の 1 ボクセル = 1 単位を人型の見た目高さ(~2m)へ縮める倍率。</summary>
    private const float ModelScale = 0.1f;

    /// <summary>モデル原点(角)を足元中心へ寄せるオフセット(ボクセル座標 × ModelScale)。</summary>
    private static readonly Vector3 ModelOriginOffset = new(-0.45f, 0f, -0.35f);

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();

    private TpsLogic _logic = null!;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    private Node3D _playerNode = null!;
    private Camera3D _camera = null!;
    private Label _hud = null!;
    private PackedScene _playerScene = null!;
    private PackedScene _targetScene = null!;
    private readonly Dictionary<int, Node3D> _targetNodes = [];
    private readonly Dictionary<int, MeshInstance3D> _bulletNodes = [];
    private StandardMaterial3D _bulletMaterial = null!;

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = StateeLogging.CreateLoggerFactory(buffer);
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new TpsLogic(CmdlineArgs.ParseInt("--seed=", DefaultSeed));

        if (CmdlineArgs.HasFlag("--frozen"))
        {
            _time.Freeze();
        }

        BuildWorld();
        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"TpsGame 起動 seed={_logic.Seed}");
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

        if (_logic.IsQuitRequested)
        {
            GetTree().Quit(0);
        }
    }

    public override void _ExitTree()
    {
        StopStateeServer();
        _loggerFactory?.Dispose();
    }

    private void BuildWorld()
    {
        // 地面
        var floor = new MeshInstance3D
        {
            Mesh = new PlaneMesh
            {
                Size = new Vector2(
                    _logic.Config.ArenaHalfExtent * 2f,
                    _logic.Config.ArenaHalfExtent * 2f
                ),
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.55f, 0.62f, 0.45f),
            },
        };
        AddChild(floor);

        // 簡易ライト
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50f, 30f, 0f),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        };
        AddChild(sun);
        var ambient = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.55f, 0.75f, 0.95f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.7f, 0.75f, 0.85f),
                AmbientLightEnergy = 0.45f,
            },
        };
        AddChild(ambient);

        _playerScene = GD.Load<PackedScene>("res://assets/models/player.glb");
        _targetScene = GD.Load<PackedScene>("res://assets/models/target.glb");

        _playerNode = new Node3D { Name = "Player" };
        var playerModel = InstantiateModel(_playerScene);
        _playerNode.AddChild(playerModel);
        AddChild(_playerNode);

        // 三人称カメラ(プレイヤー後方斜め上)。Camera3D はローカル -Z を見るので
        // プレイヤー後方(-Z)に置き Y を 180° 回して正面(+Z)側を映す
        var pivot = new Node3D { Name = "CameraPivot", Position = new Vector3(0f, 1.2f, 0f) };
        _playerNode.AddChild(pivot);
        _camera = new Camera3D
        {
            Position = new Vector3(0f, 1.6f, -4.8f),
            RotationDegrees = new Vector3(-18f, 180f, 0f),
            Current = true,
            Fov = 60f,
        };
        pivot.AddChild(_camera);

        foreach (var target in _logic.Targets)
        {
            var node = new Node3D { Name = $"Target_{target.Id}" };
            node.AddChild(InstantiateModel(_targetScene));
            AddChild(node);
            _targetNodes[target.Id] = node;
        }

        _bulletMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.7f, 0.1f),
            EmissionEnergyMultiplier = 1.5f,
        };

        // HUD
        var canvas = new CanvasLayer();
        AddChild(canvas);
        _hud = new Label { Position = new Vector2(16, 12), Text = "TpsGame" };
        _hud.AddThemeFontSizeOverride("font_size", 22);
        canvas.AddChild(_hud);
    }

    private static Node3D InstantiateModel(PackedScene scene)
    {
        var root = new Node3D();
        var model = scene.Instantiate<Node3D>();
        model.Scale = Vector3.One * ModelScale;
        model.Position = ModelOriginOffset;
        // voxcee の +Z 奥行きを Godot の正面(+Z)に合わせつつ、足元を地面に置く
        root.AddChild(model);
        EnsureVertexColorMaterials(model);
        return root;
    }

    /// <summary>頂点色付き GLB が無質感にならないよう StandardMaterial を当てる。</summary>
    private static void EnsureVertexColorMaterials(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            var mat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.85f };
            mesh.MaterialOverride = mat;
        }
        foreach (var child in node.GetChildren())
        {
            EnsureVertexColorMaterials(child);
        }
    }

    private static InputState ReadHumanInput() =>
        new(
            Forward: Input.IsPhysicalKeyPressed(Key.W),
            Back: Input.IsPhysicalKeyPressed(Key.S),
            Left: Input.IsPhysicalKeyPressed(Key.A),
            Right: Input.IsPhysicalKeyPressed(Key.D),
            Shoot: Input.IsMouseButtonPressed(MouseButton.Left),
            Quit: Input.IsPhysicalKeyPressed(Key.Escape)
        );

    private void RefreshView()
    {
        _state.Update(_logic);

        var pos = _logic.PlayerPosition;
        _playerNode.Position = new Vector3(pos.X, pos.Y, pos.Z);
        // Godot の Y 回転は時計回りが正なので、論理 yaw(+Z 基準・数学角)を反転
        _playerNode.Rotation = new Vector3(0f, -_logic.PlayerYaw, 0f);

        foreach (var target in _logic.Targets)
        {
            if (!_targetNodes.TryGetValue(target.Id, out var node))
            {
                continue;
            }
            node.Visible = target.Alive;
            node.Position = new Vector3(target.Position.X, target.Position.Y, target.Position.Z);
        }

        SyncBullets();

        _hud.Text =
            $"TpsGame  score={_logic.Score}  targets={_logic.AliveTargetCount}  tick={_logic.TickCount}"
            + (_logic.IsQuitRequested ? "  QUIT" : "")
            + "\nWASD move / LMB shoot / Esc quit";
    }

    private void SyncBullets()
    {
        var alive = new HashSet<int>();
        foreach (var bullet in _logic.Bullets)
        {
            alive.Add(bullet.Id);
            if (!_bulletNodes.TryGetValue(bullet.Id, out var node))
            {
                node = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f },
                    MaterialOverride = _bulletMaterial,
                };
                AddChild(node);
                _bulletNodes[bullet.Id] = node;
            }
            node.Position = new Vector3(bullet.Position.X, bullet.Position.Y, bullet.Position.Z);
        }

        var toRemove = new List<int>();
        foreach (var (id, node) in _bulletNodes)
        {
            if (alive.Contains(id))
            {
                continue;
            }
            node.QueueFree();
            toRemove.Add(id);
        }
        foreach (var id in toRemove)
        {
            _bulletNodes.Remove(id);
        }
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        host.RegisterTickCommand(
            _time,
            parseInput: args => ParseInput(args.GetString("input") ?? ""),
            step: input => _logic.Tick(input),
            result: () =>
            {
                RefreshView();
                if (_logic.IsQuitRequested)
                {
                    GetTree().Quit(0);
                }
                _logger.ZLogInformation($"tick → tick={_logic.TickCount} score={_logic.Score}");
                return new
                {
                    _logic.TickCount,
                    _logic.Score,
                    _logic.AliveTargetCount,
                    _logic.IsQuitRequested,
                };
            },
            maxFramesPerCall: MaxTickFrames
        );
        StartStateeServer(host);
    }

    partial void StartStateeServer(StateeHost host);

    partial void StopStateeServer();

    /// <summary>"forward+shoot" のような + 区切りトークンを InputState へ写す。</summary>
    private static InputState ParseInput(string tokens)
    {
        var input = new InputState();
        foreach (var token in tokens.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            input = token.Trim().ToLowerInvariant() switch
            {
                "-" => input,
                "forward" or "w" => input with { Forward = true },
                "back" or "s" => input with { Back = true },
                "left" or "a" => input with { Left = true },
                "right" or "d" => input with { Right = true },
                "shoot" or "fire" => input with { Shoot = true },
                "quit" or "escape" or "esc" => input with { Quit = true },
                _ => input,
            };
        }
        return input;
    }
}
