using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MessBreak.Logic;
using Microsoft.Extensions.Logging;
using Statee.Core;
using Statee.Godot;
using ZLogger;

namespace MessBreak;

/// <summary>
/// MessBreak の Godot 層エントリポイント(3D TPS)。描画・入力→TickInput 変換・Statee 配線
/// だけを担い、ゲームルールはすべて MessBreak.Logic に置く(docs/USING.md「境界の掟」)。
/// 論理は _PhysicsProcess(60Hz)で 1 Tick。論理座標は 2D(X,Y)のまま、
/// 表示は Logic(X,Y) → World(X, 0, Y) に写す。モデルは voxcee 生成の GLB。
/// </summary>
public partial class Main : Node3D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;
    private const int MaxTickFrames = 3600;

    /// <summary>UI バー(画面下部)の高さ。ゲーム画面はウィンドウからこの帯を除いた領域。</summary>
    private const float UiBarHeight = 96f;

    private const float HpBarWidth = 200f;
    private const int HitMarkerFrames = 12;
    private const int EnemyFlashFrames = 4;
    private const int BurstMarkerFrames = 18;

    /// <summary>マウス感度(ラジアン / ピクセル)。</summary>
    private const float MouseSensitivity = 0.0035f;

    private const float PitchMin = -0.55f;
    private const float PitchMax = 0.35f;

    private const float CameraDistance = 28f;
    private const float CameraDistanceAds = 16f;
    private const float CameraHeight = 14f;
    private const float CameraShoulder = 6f;
    private const float CameraFov = 70f;
    private const float CameraFovAds = 50f;
    private const float CameraLerp = 0.18f;

    /// <summary>ボクセルモデルの基準スケール(1 voxel → ワールド単位)。</summary>
    private const float VoxelScale = 2.5f;

    /// <summary>床・壁タイルの XY スケール(8 voxel → TileSize 40)。</summary>
    private const float TileVoxelScale = 5f;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();
    private readonly HudState _hudState = new();

    private BattleLogic _logic = null!;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    private Node3D _world = null!;
    private Node3D _stageRoot = null!;
    private Node3D _actorsRoot = null!;
    private Node3D _fxRoot = null!;
    private Camera3D _camera = null!;
    private Node3D _playerNode = null!;
    private Node3D? _attackerModel;
    private Node3D? _debufferModel;
    private Node3D? _turretNode;
    private Node3D? _bossSpawnMarker;
    private Node3D? _turretSlotMarker;

    private readonly Dictionary<int, Node3D> _enemyNodes = new();
    private readonly Dictionary<int, MeshInstance3D> _bulletNodes = new();
    private Node3D _floorProto = null!;
    private Node3D _wallProto = null!;
    private Node3D _mobProto = null!;
    private Node3D _bossProto = null!;
    private Node3D _turretProto = null!;
    private Node3D _attackerProto = null!;
    private Node3D _debufferProto = null!;

    private AudioStreamPlayer _shotPlayer = null!;
    private AudioStreamPlayer _skillPlayer = null!;

    // TPS カメラ(マウスルック)。yaw=0 で Logic Facing=(1,0)=ワールド +X
    private float _yaw;
    private float _pitch;
    private float _camDistance = CameraDistance;
    private float _camFov = CameraFov;
    private Vector3 _camPos;

    private int _hitstopFrames;
    private readonly Dictionary<int, int> _enemyFlashFrames = new();
    private readonly List<(System.Numerics.Vector2 Pos, int Frames)> _hitMarkers = [];
    private readonly List<(System.Numerics.Vector2 Pos, int Frames, float Radius)> _burstMarkers =
    [];
    private readonly List<MeshInstance3D> _hitMarkerMeshes = [];
    private readonly List<MeshInstance3D> _burstMeshes = [];

    private System.Numerics.Vector2 _lastPlayerPos;

    private Label _missionLabel = null!;
    private Panel _uiBar = null!;
    private Label _hpLabel = null!;
    private ColorRect _hpBack = null!;
    private ColorRect _hpFill = null!;
    private Label _char1Label = null!;
    private Label _char2Label = null!;
    private Label _switchLabel = null!;
    private Control _crosshair = null!;
    private CanvasLayer _pauseLayer = null!;
    private Button _resumeButton = null!;
    private bool _paused;
    private bool _ads;

    private Rect2 GameRect
    {
        get
        {
            var window = GetViewport().GetVisibleRect().Size;
            return new Rect2(0f, 0f, window.X, window.Y - UiBarHeight);
        }
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

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
        _yaw = MathF.Atan2(_logic.PlayerFacing.Y, _logic.PlayerFacing.X);
        _lastPlayerPos = _logic.PlayerPos;
        _camPos = ToWorld(_logic.PlayerPos) + Vector3.Up * CameraHeight;

        if (CmdlineArgs.HasFlag("--frozen"))
        {
            _time.Freeze();
        }

        BuildWorld();
        LoadAssets();
        BuildStage();
        BuildPlayer();
        BuildMarkers();
        BuildHud();
        BuildPauseMenu();
        ApplyMouseMode();
        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"MessBreak 3D TPS 起動 seed={_logic.Seed}");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        if (!_paused)
        {
            UpdateCamera();
        }
        SyncWorld();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape })
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_paused)
        {
            return;
        }

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw += motion.Relative.X * MouseSensitivity;
            _pitch = Math.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, PitchMin, PitchMax);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_paused || _time.IsFrozen)
        {
            return;
        }
        if (_hitstopFrames > 0)
        {
            _hitstopFrames--;
            return;
        }
        AdvanceEffectTimers();
        _logic.Tick(ReadHumanInput());
        _time.OnFrame();
        RefreshView();
    }

    public override void _ExitTree()
    {
        StopStateeServer();
        _loggerFactory?.Dispose();
    }

    private void AdvanceEffectTimers()
    {
        for (var i = 0; i < _hitMarkers.Count; i++)
        {
            _hitMarkers[i] = _hitMarkers[i] with { Frames = _hitMarkers[i].Frames - 1 };
        }
        _hitMarkers.RemoveAll(m => m.Frames <= 0);
        for (var i = 0; i < _burstMarkers.Count; i++)
        {
            _burstMarkers[i] = _burstMarkers[i] with { Frames = _burstMarkers[i].Frames - 1 };
        }
        _burstMarkers.RemoveAll(m => m.Frames <= 0);
        foreach (var id in _enemyFlashFrames.Keys.ToArray())
        {
            if (--_enemyFlashFrames[id] <= 0)
            {
                _enemyFlashFrames.Remove(id);
            }
        }
        _lastPlayerPos = _logic.PlayerPos;
    }

    /// <summary>
    /// 肩越し TPS カメラ。向きはマウスルックの yaw/pitch。構え中は寄りと FOV を絞る。
    /// </summary>
    private void UpdateCamera()
    {
        _ads = Input.IsMouseButtonPressed(MouseButton.Right);
        var distTarget = _ads ? CameraDistanceAds : CameraDistance;
        var fovTarget = _ads ? CameraFovAds : CameraFov;
        _camDistance += (distTarget - _camDistance) * 0.15f;
        _camFov += (fovTarget - _camFov) * 0.15f;
        _camera.Fov = _camFov;

        var forward = Facing3();
        var right = new Vector3(-forward.Z, 0f, forward.X);
        var player = ToWorld(_logic.PlayerPos) + Vector3.Up * 10f;
        var desired =
            player
            - forward * _camDistance * MathF.Cos(_pitch)
            + Vector3.Up * (_camDistance * MathF.Sin(_pitch) + CameraHeight * 0.2f)
            + right * CameraShoulder;
        _camPos += (desired - _camPos) * CameraLerp;
        _camera.GlobalPosition = _camPos;
        _camera.LookAt(player + forward * 8f, Vector3.Up);
    }

    /// <summary>論理座標(X,Y)をワールド(X,0,Y)へ。</summary>
    private static Vector3 ToWorld(System.Numerics.Vector2 pos, float y = 0f) =>
        new(pos.X, y, pos.Y);

    /// <summary>水平面の向き(マウス yaw)。Logic Facing = (cos yaw, sin yaw)。</summary>
    private System.Numerics.Vector2 Facing2() => new(MathF.Cos(_yaw), MathF.Sin(_yaw));

    private Vector3 Facing3()
    {
        var f = Facing2();
        return new Vector3(f.X, 0f, f.Y);
    }

    /// <summary>カメラ中心レイと床(Y=0)の交点を論理座標で返す。</summary>
    private System.Numerics.Vector2 AimPointOnFloor()
    {
        var origin = _camera.GlobalPosition;
        var dir = -_camera.GlobalTransform.Basis.Z;
        if (MathF.Abs(dir.Y) < 1e-4f)
        {
            var f = Facing2();
            return _logic.PlayerPos + f * 80f;
        }
        var t = -origin.Y / dir.Y;
        if (t < 0f)
        {
            var f = Facing2();
            return _logic.PlayerPos + f * 80f;
        }
        var hit = origin + dir * t;
        return new System.Numerics.Vector2(hit.X, hit.Z);
    }

    private TickInput ReadHumanInput()
    {
        // WASD はカメラ向き基準(TPS)。エージェントの tick トークンはワールド絶対のまま
        var local = System.Numerics.Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.A))
        {
            local.X -= 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.D))
        {
            local.X += 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.W))
        {
            local.Y += 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.S))
        {
            local.Y -= 1f;
        }
        // 矢印も WASD と同じ相対移動
        if (Input.IsPhysicalKeyPressed(Key.Left))
        {
            local.X -= 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Right))
        {
            local.X += 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Up))
        {
            local.Y += 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Down))
        {
            local.Y -= 1f;
        }

        var forward = Facing2();
        var right = new System.Numerics.Vector2(-forward.Y, forward.X);
        var move = forward * local.Y + right * local.X;

        var fire =
            Input.IsMouseButtonPressed(MouseButton.Left)
            || Input.IsPhysicalKeyPressed(Key.Z)
            || Input.IsPhysicalKeyPressed(Key.J);

        return new TickInput(
            move,
            forward,
            Fire: fire,
            Dodge: Input.IsPhysicalKeyPressed(Key.Space),
            Sprint: Input.IsPhysicalKeyPressed(Key.Shift),
            Skill: Input.IsPhysicalKeyPressed(Key.E),
            AimPoint: AimPointOnFloor(),
            SwitchTo: Input.IsPhysicalKeyPressed(Key.Key1) ? CharacterId.Attacker
                : Input.IsPhysicalKeyPressed(Key.Key2) ? CharacterId.Debuffer
                : null,
            Interact: Input.IsPhysicalKeyPressed(Key.F)
        );
    }

    private void BuildWorld()
    {
        _world = new Node3D { Name = "World" };
        AddChild(_world);

        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.45f, 0.62f, 0.85f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.75f, 0.78f, 0.85f),
                AmbientLightEnergy = 0.85f,
            },
        };
        _world.AddChild(env);

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50f, 35f, 0f),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        };
        _world.AddChild(sun);

        _stageRoot = new Node3D { Name = "Stage" };
        _world.AddChild(_stageRoot);
        _actorsRoot = new Node3D { Name = "Actors" };
        _world.AddChild(_actorsRoot);
        _fxRoot = new Node3D { Name = "Fx" };
        _world.AddChild(_fxRoot);

        _camera = new Camera3D
        {
            Current = true,
            Fov = CameraFov,
            Near = 0.1f,
            Far = 2000f,
        };
        AddChild(_camera);
    }

    private void LoadAssets()
    {
        _floorProto = LoadGlbModel("floor", new Vector3(TileVoxelScale, 2f, TileVoxelScale));
        _wallProto = LoadGlbModel(
            "wall",
            new Vector3(TileVoxelScale, TileVoxelScale, TileVoxelScale)
        );
        _mobProto = LoadGlbModel("mob", new Vector3(VoxelScale, VoxelScale, VoxelScale));
        _bossProto = LoadGlbModel("boss", new Vector3(VoxelScale, VoxelScale, VoxelScale));
        _turretProto = LoadGlbModel("turret", new Vector3(VoxelScale, VoxelScale, VoxelScale));
        _attackerProto = LoadGlbModel("attacker", new Vector3(VoxelScale, VoxelScale, VoxelScale));
        _debufferProto = LoadGlbModel("debuffer", new Vector3(VoxelScale, VoxelScale, VoxelScale));

        _shotPlayer = new AudioStreamPlayer
        {
            Stream = AudioStreamWav.LoadFromFile(
                ProjectSettings.GlobalizePath("res://../audio/shot.wav")
            ),
        };
        AddChild(_shotPlayer);
        _skillPlayer = new AudioStreamPlayer
        {
            Stream = AudioStreamWav.LoadFromFile(
                ProjectSettings.GlobalizePath("res://../audio/skill.wav")
            ),
        };
        AddChild(_skillPlayer);
    }

    /// <summary>
    /// voxcee 生成 GLB を実行時ロードし、原点を足元中央へずらす。
    /// Godot の import 経路は使わない(dotee PNG と同じ方針)。
    /// </summary>
    private static Node3D LoadGlbModel(string name, Vector3 scale)
    {
        var path = ProjectSettings.GlobalizePath($"res://../art/{name}.glb");
        var doc = new GltfDocument();
        var state = new GltfState();
        var err = doc.AppendFromFile(path, state);
        if (err != Error.Ok)
        {
            throw new InvalidOperationException($"GLB を読めません: {path} ({err})");
        }
        var imported = doc.GenerateScene(state) as Node3D
            ?? throw new InvalidOperationException($"GLB ルートが Node3D ではありません: {path}");
        var root = new Node3D { Name = name };
        root.AddChild(imported);

        // ボクセル原点は角。既知サイズで足元中央へずらす(AABB 走査より単純で安定)
        var size = EstimateVoxelSize(name);
        imported.Position = new Vector3(-size.X * 0.5f, 0f, -size.Z * 0.5f);
        root.Scale = scale;
        return root;
    }

    /// <summary>art/*.voxel.txt の寸法と一致するローカルサイズ(スケール前)。</summary>
    private static Vector3 EstimateVoxelSize(string name) =>
        name switch
        {
            "floor" => new Vector3(8f, 1f, 8f),
            "wall" => new Vector3(8f, 12f, 8f),
            "mob" => new Vector3(8f, 5f, 6f),
            "boss" => new Vector3(10f, 8f, 10f),
            "turret" => new Vector3(8f, 8f, 6f),
            "attacker" or "debuffer" => new Vector3(8f, 12f, 8f),
            _ => new Vector3(8f, 8f, 8f),
        };

    private static Node3D InstantiateModel(Node3D proto) => (Node3D)proto.Duplicate();

    private void BuildStage()
    {
        foreach (var child in _stageRoot.GetChildren())
        {
            child.QueueFree();
        }
        var stage = _logic.Stage;
        var tile = stage.TileSize;
        for (var row = 0; row < stage.Rows.Count; row++)
        {
            for (var col = 0; col < stage.Rows[row].Length; col++)
            {
                var solid = stage.IsSolidCell(col, row);
                var cell = InstantiateModel(solid ? _wallProto : _floorProto);
                cell.Position = new Vector3((col + 0.5f) * tile, 0f, (row + 0.5f) * tile);
                _stageRoot.AddChild(cell);
            }
        }
    }

    private void BuildPlayer()
    {
        _playerNode = new Node3D { Name = "Player" };
        _actorsRoot.AddChild(_playerNode);
        _attackerModel = InstantiateModel(_attackerProto);
        _debufferModel = InstantiateModel(_debufferProto);
        _playerNode.AddChild(_attackerModel);
        _playerNode.AddChild(_debufferModel);
        _debufferModel.Visible = false;
    }

    private void BuildMarkers()
    {
        _bossSpawnMarker = MakeDiamondMarker(new Color(1f, 0.35f, 0.35f, 0.85f));
        _actorsRoot.AddChild(_bossSpawnMarker);
        _bossSpawnMarker.Position = ToWorld(_logic.Stage.BossSpawn, 2f);

        _turretSlotMarker = MakeDiamondMarker(new Color(0.45f, 0.8f, 0.5f, 0.85f));
        _actorsRoot.AddChild(_turretSlotMarker);
        _turretSlotMarker.Position = ToWorld(_logic.Stage.TurretSlot, 2f);
        _turretSlotMarker.Visible = false;
    }

    private static Node3D MakeDiamondMarker(Color color)
    {
        var root = new Node3D();
        var mesh = new MeshInstance3D
        {
            Mesh = new PrismMesh
            {
                Size = new Vector3(6f, 8f, 6f),
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        root.AddChild(mesh);
        return root;
    }

    private void SyncWorld()
    {
        // プレイヤー
        _playerNode.Position = ToWorld(_logic.PlayerPos);
        var facing = _logic.PlayerFacing;
        if (facing != System.Numerics.Vector2.Zero)
        {
            // モデルは -Z 正面想定。LookAt で -Z を向きへ揃える
            var look = ToWorld(_logic.PlayerPos + facing, 1f);
            _playerNode.LookAt(look, Vector3.Up);
        }
        var attacker = _logic.ActiveCharacter == CharacterId.Attacker;
        if (_attackerModel is not null)
        {
            _attackerModel.Visible = attacker;
        }
        if (_debufferModel is not null)
        {
            _debufferModel.Visible = !attacker;
        }
        _playerNode.ModulateAlpha(_logic.PlayerAction == PlayerAction.Dodge ? 0.5f : 1f);

        // 敵
        var alive = new HashSet<int>();
        foreach (var enemy in _logic.Enemies)
        {
            alive.Add(enemy.Id);
            if (!_enemyNodes.TryGetValue(enemy.Id, out var node))
            {
                node = InstantiateModel(enemy.Kind == EnemyKind.Mob ? _mobProto : _bossProto);
                _actorsRoot.AddChild(node);
                _enemyNodes[enemy.Id] = node;
            }
            node.Position = ToWorld(enemy.Pos);
            if (enemy.Kind == EnemyKind.Boss)
            {
                var toPlayer = _logic.PlayerPos - enemy.Pos;
                if (toPlayer != System.Numerics.Vector2.Zero)
                {
                    node.LookAt(
                        ToWorld(enemy.Pos + System.Numerics.Vector2.Normalize(toPlayer), 1f),
                        Vector3.Up
                    );
                }
            }
            node.SetMeta("flashing", _enemyFlashFrames.ContainsKey(enemy.Id));
            EnsureDebuffRing(node, enemy.DebuffTicks > 0);
        }
        foreach (var id in _enemyNodes.Keys.ToArray())
        {
            if (!alive.Contains(id))
            {
                _enemyNodes[id].QueueFree();
                _enemyNodes.Remove(id);
            }
        }

        // 弾
        var bulletIds = new HashSet<int>();
        foreach (var bullet in _logic.Bullets)
        {
            bulletIds.Add(bullet.Id);
            if (!_bulletNodes.TryGetValue(bullet.Id, out var mesh))
            {
                mesh = new MeshInstance3D
                {
                    Mesh = new SphereMesh
                    {
                        Radius = _logic.Config.BulletRadius,
                        Height = _logic.Config.BulletRadius * 2f,
                    },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(1f, 0.85f, 0.4f),
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    },
                };
                _fxRoot.AddChild(mesh);
                _bulletNodes[bullet.Id] = mesh;
            }
            mesh.Position = ToWorld(bullet.Pos, 8f);
        }
        foreach (var id in _bulletNodes.Keys.ToArray())
        {
            if (!bulletIds.Contains(id))
            {
                _bulletNodes[id].QueueFree();
                _bulletNodes.Remove(id);
            }
        }

        // タレット
        if (_logic.TurretPlaced)
        {
            if (_turretNode is null)
            {
                _turretNode = InstantiateModel(_turretProto);
                _actorsRoot.AddChild(_turretNode);
                _turretNode.Position = ToWorld(_logic.Stage.TurretSlot);
            }
            _turretNode.Visible = true;
        }
        else if (_turretNode is not null)
        {
            _turretNode.Visible = false;
        }

        if (_bossSpawnMarker is not null)
        {
            _bossSpawnMarker.Visible = !_logic.BossAppeared;
        }
        if (_turretSlotMarker is not null)
        {
            _turretSlotMarker.Visible = _logic.ZoneCaptured && !_logic.TurretPlaced;
        }

        SyncFxMeshes();
    }

    private void SyncFxMeshes()
    {
        while (_hitMarkerMeshes.Count < _hitMarkers.Count)
        {
            var m = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 2f, OuterRadius = 4f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = Colors.White,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            };
            _fxRoot.AddChild(m);
            _hitMarkerMeshes.Add(m);
        }
        for (var i = 0; i < _hitMarkerMeshes.Count; i++)
        {
            if (i >= _hitMarkers.Count)
            {
                _hitMarkerMeshes[i].Visible = false;
                continue;
            }
            var (pos, frames) = _hitMarkers[i];
            var t = 1f - frames / (float)HitMarkerFrames;
            _hitMarkerMeshes[i].Visible = true;
            _hitMarkerMeshes[i].Position = ToWorld(pos, 8f);
            _hitMarkerMeshes[i].Scale = Vector3.One * (1f + t * 2f);
            if (_hitMarkerMeshes[i].MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = new Color(1f, 1f, 1f, 1f - t);
            }
        }

        while (_burstMeshes.Count < _burstMarkers.Count)
        {
            var m = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.5f, OuterRadius = 1f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.6f, 0.25f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            };
            _fxRoot.AddChild(m);
            _burstMeshes.Add(m);
        }
        for (var i = 0; i < _burstMeshes.Count; i++)
        {
            if (i >= _burstMarkers.Count)
            {
                _burstMeshes[i].Visible = false;
                continue;
            }
            var (pos, frames, radius) = _burstMarkers[i];
            var t = 1f - frames / (float)BurstMarkerFrames;
            _burstMeshes[i].Visible = true;
            _burstMeshes[i].Position = ToWorld(pos, 2f);
            _burstMeshes[i].Scale = Vector3.One * MathF.Max(0.1f, radius * t);
            if (_burstMeshes[i].MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = new Color(1f, 0.6f, 0.25f, 1f - t);
            }
        }
    }

    private static void EnsureDebuffRing(Node3D enemyNode, bool active)
    {
        var ring = enemyNode.GetNodeOrNull<MeshInstance3D>("DebuffRing");
        if (active)
        {
            if (ring is null)
            {
                ring = new MeshInstance3D
                {
                    Name = "DebuffRing",
                    Mesh = new TorusMesh { InnerRadius = 8f, OuterRadius = 10f },
                    Position = new Vector3(0f, 2f, 0f),
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.8f, 0.4f, 1f, 0.9f),
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    },
                };
                enemyNode.AddChild(ring);
            }
            ring.Visible = true;
        }
        else if (ring is not null)
        {
            ring.Visible = false;
        }
    }

    private void RefreshView()
    {
        foreach (var battleEvent in _logic.Events)
        {
            switch (battleEvent.Kind)
            {
                case BattleEventKind.BulletFired:
                case BattleEventKind.TurretFired:
                    _shotPlayer.Play();
                    break;
                case BattleEventKind.EnemyHit:
                    _hitMarkers.Add((battleEvent.Pos, HitMarkerFrames));
                    _enemyFlashFrames[battleEvent.EnemyId] = EnemyFlashFrames;
                    _hitstopFrames = 2;
                    break;
                case BattleEventKind.EnemyKilled:
                    _hitstopFrames = 6;
                    break;
                case BattleEventKind.BossAppeared:
                    _hitstopFrames = 6;
                    _skillPlayer.Play();
                    break;
                case BattleEventKind.MissionCleared:
                    _hitstopFrames = 12;
                    break;
                case BattleEventKind.SkillBurst:
                    _burstMarkers.Add(
                        (
                            battleEvent.Pos,
                            BurstMarkerFrames,
                            _logic.Config.CharacterOf(_logic.ActiveCharacter).SkillRadius
                        )
                    );
                    _skillPlayer.Play();
                    break;
            }
        }
        // tick コマンド経路では人間のマウスルックと独立して向きが変わるので追従する
        if (_logic.PlayerFacing != System.Numerics.Vector2.Zero)
        {
            _yaw = MathF.Atan2(_logic.PlayerFacing.Y, _logic.PlayerFacing.X);
        }
        _state.Update(_logic, _paused);
        UpdateHud();
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var bar = new Panel();
        _uiBar = bar;
        bar.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        bar.OffsetTop = -UiBarHeight;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.09f),
            BorderColor = new Color(0.35f, 0.3f, 0.45f),
            BorderWidthTop = 2,
        };
        bar.AddThemeStyleboxOverride("panel", style);
        layer.AddChild(bar);

        _hpLabel = MakeLabel(bar, new Vector2(24f, 14f), 18, new Color(0.6f, 1f, 0.65f));
        _hpBack = new ColorRect
        {
            Position = new Vector2(24f, 52f),
            Size = new Vector2(HpBarWidth, 14f),
            Color = new Color(0.2f, 0.22f, 0.2f),
        };
        bar.AddChild(_hpBack);
        _hpFill = new ColorRect
        {
            Size = new Vector2(HpBarWidth, 14f),
            Color = new Color(0.35f, 0.85f, 0.45f),
        };
        _hpBack.AddChild(_hpFill);

        var overlay = new CanvasLayer();
        AddChild(overlay);
        _missionLabel = new Label { Position = new Vector2(16f, 12f) };
        _missionLabel.AddThemeFontSizeOverride("font_size", 18);
        _missionLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.8f));
        _missionLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _missionLabel.AddThemeConstantOverride("outline_size", 6);
        overlay.AddChild(_missionLabel);

        _char1Label = MakeLabel(bar, new Vector2(0f, 14f), 18, Colors.White);
        _char2Label = MakeLabel(bar, new Vector2(0f, 50f), 18, Colors.White);
        _switchLabel = MakeLabel(bar, new Vector2(0f, 32f), 14, new Color(1f, 1f, 1f, 0.6f));
        AnchorToBarCenter(_char1Label, -120f);
        AnchorToBarCenter(_char2Label, -120f);
        AnchorToBarCenter(_switchLabel, 110f);

        // 画面中央クロスヘア(ゲーム領域の中心。UI バー分を上へずらす)
        _crosshair = new Control();
        _crosshair.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(_crosshair);
        var cross = new ColorRect
        {
            Size = new Vector2(4f, 4f),
            Color = new Color(1f, 1f, 1f, 0.9f),
        };
        cross.SetAnchorsPreset(Control.LayoutPreset.Center);
        cross.OffsetLeft = -2f;
        cross.OffsetTop = -2f - UiBarHeight * 0.5f;
        cross.OffsetRight = 2f;
        cross.OffsetBottom = 2f - UiBarHeight * 0.5f;
        _crosshair.AddChild(cross);
        foreach (var (dx, dy, w, h) in new (float, float, float, float)[]
                 {
                     (-10f, -1f, 6f, 2f),
                     (4f, -1f, 6f, 2f),
                     (-1f, -10f, 2f, 6f),
                     (-1f, 4f, 2f, 6f),
                 })
        {
            var arm = new ColorRect
            {
                Size = new Vector2(w, h),
                Color = new Color(1f, 1f, 1f, 0.75f),
            };
            arm.SetAnchorsPreset(Control.LayoutPreset.Center);
            arm.OffsetLeft = dx;
            arm.OffsetTop = dy - UiBarHeight * 0.5f;
            arm.OffsetRight = dx + w;
            arm.OffsetBottom = dy + h - UiBarHeight * 0.5f;
            _crosshair.AddChild(arm);
        }
    }

    private static void AnchorToBarCenter(Label label, float offsetX)
    {
        var top = label.Position.Y;
        label.AnchorLeft = 0.5f;
        label.AnchorRight = 0.5f;
        label.OffsetLeft = offsetX;
        label.OffsetTop = top;
    }

    private static Label MakeLabel(Control parent, Vector2 pos, int fontSize, Color color)
    {
        var label = new Label { Position = pos };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
        return label;
    }

    private string CooldownText(int ticks) =>
        ticks == 0 ? "READY" : $"{ticks / (float)_logic.Config.TicksPerSecond:0.0}s";

    private void UpdateHud()
    {
        _missionLabel.Text =
            _logic.MissionCleared ? "ミッション達成!"
            : !_logic.ZoneCaptured ? "雑魚を倒してエリアを制圧しよう"
            : !_logic.TurretPlaced ? "スロット(緑)の近くで F: タレット設置"
            : !_logic.BossAppeared ? "出現ポイント(赤)の近くで F: 強敵を呼ぶ"
            : "強敵を倒せ!(デバフ→大技のコンボが有効)";

        var active = _logic.ActiveCharacter;
        _char1Label.Text =
            $"{(active == CharacterId.Attacker ? "▶" : "  ")} 1 アタッカー  E {CooldownText(_logic.SkillCooldownOf(CharacterId.Attacker))}";
        _char2Label.Text =
            $"{(active == CharacterId.Debuffer ? "▶" : "  ")} 2 デバッファー  E {CooldownText(_logic.SkillCooldownOf(CharacterId.Debuffer))}";
        _char1Label.AddThemeColorOverride(
            "font_color",
            active == CharacterId.Attacker ? Colors.White : new Color(1f, 1f, 1f, 0.45f)
        );
        _char2Label.AddThemeColorOverride(
            "font_color",
            active == CharacterId.Debuffer
                ? new Color(0.55f, 0.9f, 1f)
                : new Color(1f, 1f, 1f, 0.45f)
        );
        _switchLabel.Text =
            _logic.SwitchCooldown > 0 ? $"切替 {CooldownText(_logic.SwitchCooldown)}" : "";

        var maxHp = _logic.Config.PlayerMaxHp;
        _hpLabel.Text = $"HP {_logic.PlayerHp}/{maxHp}";
        _hpFill.Size = _hpFill.Size with
        {
            X = HpBarWidth * Math.Clamp(_logic.PlayerHp / (float)maxHp, 0f, 1f),
        };

        _crosshair.Visible = !_paused;
        _ads = !_paused && Input.IsMouseButtonPressed(MouseButton.Right);

        _hudState.Update(
            new HudState.Snapshot(
                MissionText: _missionLabel.Text,
                MissionRect: RectText(_missionLabel.GetGlobalRect()),
                HpText: _hpLabel.Text,
                HpBarRatio: _hpFill.Size.X / HpBarWidth,
                HpBarRect: RectText(_hpBack.GetGlobalRect()),
                Char1Text: _char1Label.Text,
                Char1Rect: RectText(_char1Label.GetGlobalRect()),
                Char2Text: _char2Label.Text,
                Char2Rect: RectText(_char2Label.GetGlobalRect()),
                SwitchText: _switchLabel.Text,
                UiBarRect: RectText(_uiBar.GetGlobalRect()),
                GameRect: RectText(GameRect),
                PauseMenuVisible: _pauseLayer?.Visible ?? false,
                PlayerModelCharacter: _logic.ActiveCharacter.ToString(),
                AdsActive: _ads,
                CrosshairVisible: _crosshair.Visible
            )
        );
    }

    private static string RectText(Rect2 rect) =>
        $"{rect.Position.X:0},{rect.Position.Y:0},{rect.Size.X:0},{rect.Size.Y:0}";

    private void BuildPauseMenu()
    {
        _pauseLayer = new CanvasLayer { Layer = 10, Visible = false };
        AddChild(_pauseLayer);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(center);

        var menu = new VBoxContainer();
        menu.AddThemeConstantOverride("separation", 12);
        center.AddChild(menu);

        var title = new Label { Text = "ポーズ", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        menu.AddChild(title);

        _resumeButton = MakeMenuButton(menu, "再開", () => TogglePause());
        MakeMenuButton(menu, "はじめから", RestartMission);
        var abortButton = MakeMenuButton(menu, "中断", () => { });
        abortButton.Disabled = true;
        abortButton.TooltipText = "タイトル画面の実装後に有効化";
        MakeMenuButton(menu, "ゲーム終了", () => GetTree().Quit());
    }

    private static Button MakeMenuButton(VBoxContainer menu, string text, Action onPressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(220f, 40f) };
        button.Pressed += onPressed;
        menu.AddChild(button);
        return button;
    }

    private void ApplyMouseMode()
    {
        Input.MouseMode = _paused
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseLayer.Visible = _paused;
        ApplyMouseMode();
        if (_paused)
        {
            _resumeButton.GrabFocus();
        }
        _state.Update(_logic, _paused);
        UpdateHud();
    }

    private void RestartMission()
    {
        _logic = new BattleLogic(new BattleConfig(), Stages.Room1(), _logic.Seed);
        _yaw = MathF.Atan2(_logic.PlayerFacing.Y, _logic.PlayerFacing.X);
        _pitch = 0f;
        _camDistance = CameraDistance;
        _camFov = CameraFov;
        _camPos = ToWorld(_logic.PlayerPos) + Vector3.Up * CameraHeight;
        _hitMarkers.Clear();
        _burstMarkers.Clear();
        _hitstopFrames = 0;
        _enemyFlashFrames.Clear();
        _lastPlayerPos = _logic.PlayerPos;
        foreach (var node in _enemyNodes.Values)
        {
            node.QueueFree();
        }
        _enemyNodes.Clear();
        foreach (var node in _bulletNodes.Values)
        {
            node.QueueFree();
        }
        _bulletNodes.Clear();
        if (_turretNode is not null)
        {
            _turretNode.QueueFree();
            _turretNode = null;
        }
        TogglePause();
        RefreshView();
        SyncWorld();
        _logger.ZLogInformation($"ミッションをはじめから(seed={_logic.Seed})");
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        host.RegisterStateProvider(_hudState);
        host.RegisterTimeControl(_time);
        StandardCommands.Register(host, this, _logger);
        host.RegisterTickCommand(
            _time,
            parseInput: args =>
            {
                var aim = new System.Numerics.Vector2(
                    ParseFloat(args.GetString("aimx")),
                    ParseFloat(args.GetString("aimy"))
                );
                var skillX = args.GetString("skillx");
                var skillY = args.GetString("skilly");
                System.Numerics.Vector2? aimPoint =
                    skillX is null && skillY is null
                        ? null
                        : new System.Numerics.Vector2(ParseFloat(skillX), ParseFloat(skillY));
                return ParseInput(args.GetString("input") ?? "", aim, aimPoint);
            },
            step: input =>
            {
                AdvanceEffectTimers();
                _logic.Tick(input);
            },
            result: () =>
            {
                RefreshView();
                SyncWorld();
                _logger.ZLogInformation(
                    $"tick → tick={_logic.TickCount} hits={_logic.HitCount}/{_logic.ShotCount}"
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

    partial void StartStateeServer(StateeHost host);

    partial void StopStateeServer();

    private static float ParseFloat(string? value) =>
        value is null ? 0f : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// エージェント用。left/right/up/down はワールド絶対(Logic 座標)。
    /// 人間の WASD(カメラ相対)とは別経路。
    /// </summary>
    private static TickInput ParseInput(
        string tokens,
        System.Numerics.Vector2 aim,
        System.Numerics.Vector2? aimPoint
    )
    {
        var dir = System.Numerics.Vector2.Zero;
        var fire = false;
        var dodge = false;
        var sprint = false;
        var skill = false;
        var interact = false;
        CharacterId? switchTo = null;
        foreach (var token in tokens.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "-":
                    break;
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
                case "skill":
                    skill = true;
                    break;
                case "char1":
                    switchTo = CharacterId.Attacker;
                    break;
                case "char2":
                    switchTo = CharacterId.Debuffer;
                    break;
                case "interact":
                    interact = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"未知の入力トークン '{token}'(left/right/up/down/fire/dodge/sprint/skill/char1/char2/interact)"
                    );
            }
        }
        return new TickInput(dir, aim, fire, dodge, sprint, skill, aimPoint, switchTo, interact);
    }
}

/// <summary>Node3D の子メッシュに一括でアルファを載せるための拡張。</summary>
file static class Node3DAlphaExtensions
{
    public static void ModulateAlpha(this Node3D node, float alpha)
    {
        void Walk(Node n)
        {
            if (n is GeometryInstance3D gi)
            {
                gi.Transparency = 1f - alpha;
            }
            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }
        Walk(node);
    }
}
