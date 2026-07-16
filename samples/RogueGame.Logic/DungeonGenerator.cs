namespace RogueGame.Logic;

/// <summary>
/// 部屋+通路の古典的ダンジョン生成。
/// 同一シード・同一フロア番号から常に同一のマップを生成する(docs/adr/D-044.md)。
/// </summary>
public static class DungeonGenerator
{
    private const int MaxRooms = 8;
    private const int PlacementAttempts = 200;
    private const int MinRoomWidth = 4;
    private const int MaxRoomWidth = 9;
    private const int MinRoomHeight = 3;
    private const int MaxRoomHeight = 6;

    /// <summary>指定シード・フロア番号(1 起点)のマップを生成する。</summary>
    public static DungeonMap Generate(int seed, int floorNumber)
    {
        // HashCode.Combine はプロセスごとにランダム化されるため、再現性のために自前で合成する
        var rng = new Random(unchecked(seed * 31 + floorNumber));
        var tiles = new Tile[RogueConfig.MapWidth, RogueConfig.MapHeight];
        var rooms = PlaceRooms(rng, tiles);
        CarveCorridors(rng, tiles, rooms);

        var stairsUp = Center(rooms[0]);
        var stairsDown = Center(rooms[^1]);
        tiles[stairsUp.X, stairsUp.Y] = Tile.StairsUp;
        tiles[stairsDown.X, stairsDown.Y] = Tile.StairsDown;
        return new DungeonMap(tiles, stairsUp, stairsDown);
    }

    private readonly record struct Room(int X, int Y, int Width, int Height);

    private static List<Room> PlaceRooms(Random rng, Tile[,] tiles)
    {
        var rooms = new List<Room>();
        for (var attempt = 0; attempt < PlacementAttempts && rooms.Count < MaxRooms; attempt++)
        {
            var width = rng.Next(MinRoomWidth, MaxRoomWidth + 1);
            var height = rng.Next(MinRoomHeight, MaxRoomHeight + 1);
            var room = new Room(
                rng.Next(1, RogueConfig.MapWidth - width - 1),
                rng.Next(1, RogueConfig.MapHeight - height - 1),
                width,
                height
            );
            if (rooms.Any(existing => Overlaps(existing, room)))
            {
                continue;
            }
            rooms.Add(room);
            for (var x = room.X; x < room.X + room.Width; x++)
            {
                for (var y = room.Y; y < room.Y + room.Height; y++)
                {
                    tiles[x, y] = Tile.Floor;
                }
            }
        }
        return rooms;
    }

    private static bool Overlaps(Room a, Room b) =>
        // 1マスの間隔を空けて部屋同士が癒着しないようにする
        a.X - 1
            < b.X + b.Width
        && b.X - 1 < a.X + a.Width
        && a.Y - 1 < b.Y + b.Height
        && b.Y - 1 < a.Y + a.Height;

    private static void CarveCorridors(Random rng, Tile[,] tiles, List<Room> rooms)
    {
        for (var i = 1; i < rooms.Count; i++)
        {
            var from = Center(rooms[i - 1]);
            var to = Center(rooms[i]);
            // L字通路。曲げる順序(横→縦 / 縦→横)は乱数で選ぶ
            if (rng.Next(2) == 0)
            {
                CarveHorizontal(tiles, from.X, to.X, from.Y);
                CarveVertical(tiles, from.Y, to.Y, to.X);
            }
            else
            {
                CarveVertical(tiles, from.Y, to.Y, from.X);
                CarveHorizontal(tiles, from.X, to.X, to.Y);
            }
        }
    }

    private static void CarveHorizontal(Tile[,] tiles, int fromX, int toX, int y)
    {
        for (var x = Math.Min(fromX, toX); x <= Math.Max(fromX, toX); x++)
        {
            tiles[x, y] = Tile.Floor;
        }
    }

    private static void CarveVertical(Tile[,] tiles, int fromY, int toY, int x)
    {
        for (var y = Math.Min(fromY, toY); y <= Math.Max(fromY, toY); y++)
        {
            tiles[x, y] = Tile.Floor;
        }
    }

    private static GridPos Center(Room room) =>
        new(room.X + room.Width / 2, room.Y + room.Height / 2);
}
