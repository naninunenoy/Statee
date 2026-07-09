namespace RogueGame.Logic.Tests;

/// <summary>テスト用の文字アートマップ。# = 壁, . = 床, &lt; = 上り階段, &gt; = 下り階段。</summary>
public static class MapText
{
    public static DungeonMap Parse(params string[] rows)
    {
        var tiles = new Tile[rows[0].Length, rows.Length];
        var stairsUp = default(GridPos);
        var stairsDown = default(GridPos);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                tiles[x, y] = rows[y][x] switch
                {
                    '#' => Tile.Wall,
                    '.' => Tile.Floor,
                    '<' => Tile.StairsUp,
                    '>' => Tile.StairsDown,
                    _ => throw new ArgumentException($"未知のタイル文字: {rows[y][x]}"),
                };
                if (tiles[x, y] == Tile.StairsUp)
                {
                    stairsUp = new GridPos(x, y);
                }
                if (tiles[x, y] == Tile.StairsDown)
                {
                    stairsDown = new GridPos(x, y);
                }
            }
        }
        return new DungeonMap(tiles, stairsUp, stairsDown);
    }
}
