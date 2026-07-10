namespace RogueGame.Logic;

/// <summary>
/// 地形生成(<see cref="DungeonGenerator"/>)に敵の初期配置を加えて1フロアを組み立てる。
/// 同一シード・同一フロア番号から常に同一のフロアを生成する(docs/adr/D-044.md)。
/// </summary>
public static class FloorGenerator
{
    /// <summary>指定シード・フロア番号(1 起点)のフロアを生成する。</summary>
    public static Floor Generate(int seed, int floorNumber)
    {
        var map = DungeonGenerator.Generate(seed, floorNumber);
        // 地形生成と敵配置で乱数列を分離し、地形のシード互換を保つ
        var rng = new Random(unchecked(seed * 31 + floorNumber) ^ 0x5EED);
        var candidates = FloorTiles(map).ToList();
        var enemies = new List<Enemy>();
        for (var i = 0; i < RogueConfig.EnemyCount(floorNumber) && candidates.Count > 0; i++)
        {
            var pos = candidates[rng.Next(candidates.Count)];
            candidates.Remove(pos);
            enemies.Add(
                new Enemy(new EnemyId(i + 1), pos, RogueConfig.EnemyHp, RogueConfig.EnemyAttack)
            );
        }
        var items = new List<Item>();
        var itemId = 1;
        for (var i = 0; i < RogueConfig.PotionsPerFloor && candidates.Count > 0; i++)
        {
            items.Add(new Item(new ItemId(itemId++), ItemKind.Potion, TakeAt(rng, candidates)));
        }
        if (floorNumber == RogueConfig.SwordFloor && candidates.Count > 0)
        {
            items.Add(new Item(new ItemId(itemId++), ItemKind.Sword, TakeAt(rng, candidates)));
        }
        if (floorNumber == RogueConfig.FloorCount && candidates.Count > 0)
        {
            items.Add(new Item(new ItemId(itemId), ItemKind.Gem, TakeAt(rng, candidates)));
        }
        return new Floor(map, enemies, items);
    }

    private static GridPos TakeAt(Random rng, List<GridPos> candidates)
    {
        var pos = candidates[rng.Next(candidates.Count)];
        candidates.Remove(pos);
        return pos;
    }

    private static IEnumerable<GridPos> FloorTiles(DungeonMap map) =>
        from x in Enumerable.Range(0, map.Width)
        from y in Enumerable.Range(0, map.Height)
        let pos = new GridPos(x, y)
        where map[pos] == Tile.Floor
        select pos;
}
