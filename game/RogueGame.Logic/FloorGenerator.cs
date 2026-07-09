namespace RogueGame.Logic;

/// <summary>
/// 地形生成(<see cref="DungeonGenerator"/>)に敵の初期配置を加えて1フロアを組み立てる。
/// 同一シード・同一フロア番号から常に同一のフロアを生成する(docs/adr/D-044.md)。
/// </summary>
public static class FloorGenerator
{
    /// <summary>指定シード・フロア番号(1 起点)のフロアを生成する。</summary>
    public static Floor Generate(int seed, int floorNumber) => default!;
}
