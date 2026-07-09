using UnitGenerator;

namespace RogueGame.Logic;

/// <summary>
/// フロア上のアイテムの安定 ID(docs/adr/D-006.md)。拾われるまで変わらない。
/// </summary>
[UnitOf(typeof(int))]
public readonly partial struct ItemId;
