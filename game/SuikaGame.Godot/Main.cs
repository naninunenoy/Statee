using System;
using System.Collections.Generic;
using Godot;
using R3;
using SuikaGame.Logic;

namespace SuikaGame;

/// <summary>
/// スイカゲームの Godot 層エントリポイント。物理・描画・入力だけを担い、
/// 規則(合体・スコア・ゲームオーバー)は SuikaGame.Logic に委ねる(D-011, D-024)。
/// 境界: 接触 → ReportContact / 溢れ → SetOverflowing / 時間 → Tick、
/// 逆向きは Merges 購読で物理ボディを差し替える。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultSeed = 12345;
    private const float WallLeft = 100f;
    private const float WallRight = 500f;
    private const float FloorY = 760f;
    private const float OverflowLineY = 260f;
    private const float DropY = 120f;

    private readonly Dictionary<FruitId, Fruit> _fruits = [];
    private SuikaLogic _logic = null!;
    private IDisposable _subscriptions = null!;
    private float _dropX = (WallLeft + WallRight) / 2f;

    public override void _Ready()
    {
        _logic = new SuikaLogic(DefaultSeed);
        var subscriptions = Disposable.CreateBuilder();
        _logic.Merges.Subscribe(Logic_MergeOccurred).AddTo(ref subscriptions);
        _logic.Score.Subscribe(score => GD.Print($"Score: {score}")).AddTo(ref subscriptions);
        _logic
            .IsGameOver.Where(gameOver => gameOver)
            .Subscribe(_ => GD.Print("GameOver"))
            .AddTo(ref subscriptions);
        _subscriptions = subscriptions.Build();

        BuildContainer();
        GD.Print($"SuikaGame 起動 next={_logic.PeekNext()}");

        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0)
        {
            RunSmoke();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // 溢れ判定は Area2D でなく毎フレームの位置走査で行う(理由は docs/NOTES.md)。
        // 落下直後の誤検知を避けるため、一度でも接触したフルーツだけを対象にする
        foreach (var (id, fruit) in _fruits)
        {
            var overflowing =
                fruit.HasContacted && fruit.Position.Y - Fruit.RadiusOf(fruit.Kind) < OverflowLineY;
            _logic.SetOverflowing(id, overflowing);
        }

        _logic.Tick(delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                _dropX = motion.Position.X;
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                Drop();
                break;
        }
    }

    public override void _Draw()
    {
        var wallColor = new Color(0.35f, 0.3f, 0.25f);
        DrawLine(
            new Vector2(WallLeft, OverflowLineY),
            new Vector2(WallLeft, FloorY),
            wallColor,
            4f
        );
        DrawLine(
            new Vector2(WallRight, OverflowLineY),
            new Vector2(WallRight, FloorY),
            wallColor,
            4f
        );
        DrawLine(new Vector2(WallLeft, FloorY), new Vector2(WallRight, FloorY), wallColor, 4f);
        DrawDashedLine(
            new Vector2(WallLeft, OverflowLineY),
            new Vector2(WallRight, OverflowLineY),
            new Color(0.9f, 0.2f, 0.2f, 0.6f),
            2f
        );
    }

    public override void _ExitTree()
    {
        _subscriptions.Dispose();
        _logic.Dispose();
    }

    private void Drop()
    {
        if (_logic.IsGameOver.CurrentValue)
        {
            return;
        }

        var kind = _logic.PeekNext();
        var radius = Fruit.RadiusOf(kind);
        var x = Mathf.Clamp(_dropX, WallLeft + radius, WallRight - radius);
        SpawnBody(_logic.SpawnNext(), kind, new Vector2(x, DropY));
    }

    private void SpawnBody(FruitId id, FruitKind kind, Vector2 position)
    {
        var fruit = Fruit.Create(id, kind);
        fruit.Position = position;
        fruit.Contacted += Fruit_Contacted;
        _fruits[id] = fruit;
        AddChild(fruit);
    }

    private void Fruit_Contacted(Fruit self, Fruit other)
    {
        _logic.ReportContact(self.FruitId, other.FruitId);
    }

    private void Logic_MergeOccurred(MergeEvent merge)
    {
        var midpoint = (PositionOf(merge.RemovedA) + PositionOf(merge.RemovedB)) / 2f;
        RemoveBody(merge.RemovedA);
        RemoveBody(merge.RemovedB);
        if (merge is { Created: { } created, CreatedKind: { } createdKind })
        {
            // 接触シグナル(物理フラッシュ中)から呼ばれるため、ボディの追加は次フレームへ遅延する
            Callable.From(() => SpawnBody(created, createdKind, midpoint)).CallDeferred();
        }
    }

    private Vector2 PositionOf(FruitId id) => _fruits[id].Position;

    private void RemoveBody(FruitId id)
    {
        _fruits[id].QueueFree();
        _fruits.Remove(id);
    }

    private void BuildContainer()
    {
        var container = new StaticBody2D { Name = "Container" };
        container.AddChild(Wall(new Vector2(WallLeft, 0f), new Vector2(WallLeft, FloorY)));
        container.AddChild(Wall(new Vector2(WallRight, 0f), new Vector2(WallRight, FloorY)));
        container.AddChild(Wall(new Vector2(WallLeft, FloorY), new Vector2(WallRight, FloorY)));
        AddChild(container);

        static CollisionShape2D Wall(Vector2 a, Vector2 b) =>
            new()
            {
                Shape = new SegmentShape2D { A = a, B = b },
            };
    }

    /// <summary>
    /// headless での配線確認(`--smoke`)。同種2個を隣接落下させ、
    /// 物理接触 → ReportContact → Merges の経路が通ったら終了する。固定時間待機はしない。
    /// </summary>
    private void RunSmoke()
    {
        _logic
            .Merges.Take(1)
            .Subscribe(merge =>
            {
                GD.Print($"SMOKE: merge 検出 created={merge.CreatedKind}");
                Callable.From(() => GetTree().Quit()).CallDeferred();
            });
        // 左右に離すと着地後に届かないことがあるため、縦に積んで確実に衝突させる
        var center = (WallLeft + WallRight) / 2f;
        SpawnBody(_logic.Spawn(FruitKind.Cherry), FruitKind.Cherry, new Vector2(center, 400f));
        SpawnBody(_logic.Spawn(FruitKind.Cherry), FruitKind.Cherry, new Vector2(center, 300f));
        GD.Print("SMOKE: チェリー2個を投下");
    }
}
