using Shouldly;

namespace RogueGame.Logic.Tests;

public class DungeonGeneratorTest
{
    [Fact]
    public void Generate_同一シードと同一フロア番号_同一のマップを生成する()
    {
        var first = DungeonGenerator.Generate(seed: 12345, floorNumber: 1);
        var second = DungeonGenerator.Generate(seed: 12345, floorNumber: 1);

        TilesOf(second).ShouldBe(TilesOf(first));
        second.StairsUp.ShouldBe(first.StairsUp);
        second.StairsDown.ShouldBe(first.StairsDown);
    }

    [Fact]
    public void Generate_同一シードで異なるフロア番号_異なるマップを生成する()
    {
        var floor1 = DungeonGenerator.Generate(seed: 12345, floorNumber: 1);
        var floor2 = DungeonGenerator.Generate(seed: 12345, floorNumber: 2);

        TilesOf(floor2).ShouldNotBe(TilesOf(floor1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Generate_任意のシード_マップサイズは設定どおり(int seed)
    {
        var map = DungeonGenerator.Generate(seed, floorNumber: 1);

        map.Width.ShouldBe(RogueConfig.MapWidth);
        map.Height.ShouldBe(RogueConfig.MapHeight);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Generate_任意のシード_外周はすべて壁(int seed)
    {
        var map = DungeonGenerator.Generate(seed, floorNumber: 1);

        foreach (var pos in EdgePositions(map))
        {
            map[pos].ShouldBe(Tile.Wall, $"外周 {pos} が壁でない");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Generate_任意のシード_上り階段と下り階段が1つずつ地形に置かれる(int seed)
    {
        var map = DungeonGenerator.Generate(seed, floorNumber: 1);

        map[map.StairsUp].ShouldBe(Tile.StairsUp);
        map[map.StairsDown].ShouldBe(Tile.StairsDown);
        CountTiles(map, Tile.StairsUp).ShouldBe(1);
        CountTiles(map, Tile.StairsDown).ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Generate_任意のシード_上り階段から下り階段へ歩いて到達できる(int seed)
    {
        var map = DungeonGenerator.Generate(seed, floorNumber: 1);

        Reachable(map, map.StairsUp, map.StairsDown).ShouldBeTrue();
    }

    [Fact]
    public void IsWalkable_床と階段_true()
    {
        var map = DungeonGenerator.Generate(seed: 12345, floorNumber: 1);

        map.IsWalkable(map.StairsUp).ShouldBeTrue();
        map.IsWalkable(map.StairsDown).ShouldBeTrue();
        map.IsWalkable(FirstTile(map, Tile.Floor)).ShouldBeTrue();
    }

    [Fact]
    public void IsWalkable_壁_false()
    {
        var map = DungeonGenerator.Generate(seed: 12345, floorNumber: 1);

        map.IsWalkable(FirstTile(map, Tile.Wall)).ShouldBeFalse();
    }

    private static List<Tile> TilesOf(DungeonMap map) =>
        [.. AllPositions(map).Select(pos => map[pos])];

    private static IEnumerable<GridPos> AllPositions(DungeonMap map) =>
        from y in Enumerable.Range(0, map.Height)
        from x in Enumerable.Range(0, map.Width)
        select new GridPos(x, y);

    private static IEnumerable<GridPos> EdgePositions(DungeonMap map) =>
        AllPositions(map)
            .Where(pos =>
                pos.X == 0 || pos.Y == 0 || pos.X == map.Width - 1 || pos.Y == map.Height - 1
            );

    private static int CountTiles(DungeonMap map, Tile tile) =>
        AllPositions(map).Count(pos => map[pos] == tile);

    private static GridPos FirstTile(DungeonMap map, Tile tile) =>
        AllPositions(map).First(pos => map[pos] == tile);

    private static bool Reachable(DungeonMap map, GridPos from, GridPos to)
    {
        var visited = new HashSet<GridPos> { from };
        var queue = new Queue<GridPos>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            if (pos == to)
            {
                return true;
            }
            GridPos[] neighbors =
            [
                new(pos.X + 1, pos.Y),
                new(pos.X - 1, pos.Y),
                new(pos.X, pos.Y + 1),
                new(pos.X, pos.Y - 1),
            ];
            foreach (var next in neighbors)
            {
                if (
                    next.X >= 0
                    && next.X < map.Width
                    && next.Y >= 0
                    && next.Y < map.Height
                    && map.IsWalkable(next)
                    && visited.Add(next)
                )
                {
                    queue.Enqueue(next);
                }
            }
        }
        return false;
    }
}
