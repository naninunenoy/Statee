namespace RogueGame.Logic;

/// <summary>フロア上に落ちているアイテム1つ。</summary>
public sealed class Item
{
    public Item(ItemId id, ItemKind kind, GridPos pos)
    {
        Id = id;
        Kind = kind;
        Pos = pos;
    }

    /// <summary>安定 ID。拾われるまで変わらない。</summary>
    public ItemId Id { get; }

    /// <summary>種類。</summary>
    public ItemKind Kind { get; }

    /// <summary>位置。</summary>
    public GridPos Pos { get; }
}
