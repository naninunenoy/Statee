namespace RogueGame.Logic;

/// <summary>
/// 部屋+通路の古典的ダンジョン生成。
/// 同一シード・同一フロア番号から常に同一のマップを生成する(docs/adr/D-044.md)。
/// </summary>
public static class DungeonGenerator
{
    /// <summary>指定シード・フロア番号(1 起点)のマップを生成する。</summary>
    public static DungeonMap Generate(int seed, int floorNumber) => default!;
}
