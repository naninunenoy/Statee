namespace RogueGame.Logic;

/// <summary>
/// プレイヤーの1アクション。リプレイ検証(D-044 の検証の柱3)のための記録・再生の単位。
/// </summary>
public readonly record struct RogueAction
{
    /// <summary>アクションの種別。</summary>
    public RogueActionKind Kind { get; private init; }

    /// <summary>Kind が Move のときの移動方向。</summary>
    public Direction Direction { get; private init; }

    /// <summary>Kind が Use のときの使用アイテム。</summary>
    public ItemKind Item { get; private init; }

    /// <summary>移動アクションを作る。</summary>
    public static RogueAction Move(Direction direction) =>
        new() { Kind = RogueActionKind.Move, Direction = direction };

    /// <summary>アイテム使用アクションを作る。</summary>
    public static RogueAction Use(ItemKind item) =>
        new() { Kind = RogueActionKind.Use, Item = item };
}
