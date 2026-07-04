using System;
using Godot;
using SuikaGame.Logic;

namespace SuikaGame;

/// <summary>
/// フルーツの物理ボディ。位置・落下・衝突は Godot 物理が所有し(D-011, D-024)、
/// 同種判定などの規則は SuikaLogic に委ねる。接触はイベントで上位へ報告するだけ。
/// </summary>
public partial class Fruit : RigidBody2D
{
    private static readonly Color[] KindColors =
    [
        new(0.86f, 0.08f, 0.24f), // Cherry
        new(0.96f, 0.26f, 0.21f), // Strawberry
        new(0.61f, 0.35f, 0.71f), // Grape
        new(0.98f, 0.69f, 0.23f), // Dekopon
        new(0.95f, 0.49f, 0.13f), // Persimmon
        new(0.80f, 0.86f, 0.22f), // Apple
        new(0.55f, 0.76f, 0.29f), // Pear
        new(0.99f, 0.85f, 0.21f), // Peach
        new(0.94f, 0.90f, 0.55f), // Pineapple
        new(0.56f, 0.93f, 0.56f), // Melon
        new(0.13f, 0.55f, 0.13f), // Watermelon
    ];

    public FruitId FruitId { get; private set; }
    public FruitKind Kind { get; private set; }

    /// <summary>何か(壁・床・他フルーツ)に一度でも接触したか。落下中の溢れ誤検知の除外に使う。</summary>
    public bool HasContacted { get; private set; }

    /// <summary>他のフルーツと接触した(自分, 相手)。両側から重複して上がるが、規則側が冪等に扱う。</summary>
    public event Action<Fruit, Fruit>? Contacted;

    public static float RadiusOf(FruitKind kind) => 16f + 9f * (int)kind;

    public static Fruit Create(FruitId id, FruitKind kind)
    {
        var fruit = new Fruit
        {
            FruitId = id,
            Kind = kind,
            // フレームを跨いだ追跡のための安定 ID(GUIDELINE 3.4)
            Name = $"Fruit_{id.AsPrimitive()}",
            ContactMonitor = true,
            MaxContactsReported = 8,
            // 親(Main)は pause 中も動く Always のため、継承すると物理が止まらない。明示的に Pausable にする
            ProcessMode = ProcessModeEnum.Pausable,
        };
        fruit.AddChild(
            new CollisionShape2D { Shape = new CircleShape2D { Radius = RadiusOf(kind) } }
        );
        return fruit;
    }

    public override void _Ready()
    {
        BodyEntered += Body_Entered;
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, RadiusOf(Kind), KindColors[(int)Kind]);
    }

    private void Body_Entered(Node body)
    {
        HasContacted = true;
        if (body is Fruit other)
        {
            Contacted?.Invoke(this, other);
        }
    }
}
