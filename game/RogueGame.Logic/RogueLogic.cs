namespace RogueGame.Logic;

/// <summary>
/// ローグライクの中核状態機械。アクション列 → 状態遷移の決定論的関数(docs/adr/D-044.md)。
/// フロアは離脱後も状態を保持する(帰路の増援・覚醒のため使い捨てない)。
/// </summary>
public sealed class RogueLogic
{
    private readonly Func<int, DungeonMap> mapFactory;
    private readonly Dictionary<int, DungeonMap> visitedFloors = [];

    /// <summary>本番用。シードからフロアを生成する。</summary>
    public RogueLogic(int seed)
        : this(floorNumber => DungeonGenerator.Generate(seed, floorNumber)) { }

    /// <summary>フロア地形の供給元を注入する(テストで手組みマップを使うためのシーム)。</summary>
    public RogueLogic(Func<int, DungeonMap> mapFactory)
    {
        this.mapFactory = mapFactory;
        CurrentFloor = 1;
        PlayerPos = Map.StairsUp;
    }

    /// <summary>現在のフロア番号(1 起点。地上に最も近いのが 1)。</summary>
    public int CurrentFloor { get; private set; }

    /// <summary>現在フロアの地形。</summary>
    public DungeonMap Map =>
        visitedFloors.TryGetValue(CurrentFloor, out var map)
            ? map
            : visitedFloors[CurrentFloor] = mapFactory(CurrentFloor);

    /// <summary>プレイヤーの現在位置。</summary>
    public GridPos PlayerPos { get; private set; }

    /// <summary>
    /// 指定方向へ1マス移動する。壁方向なら何も起きない。
    /// 下り階段に乗ると次フロアへ、上り階段に乗ると前フロアへ自動遷移する
    /// (フロア1の上り階段は現時点では何も起きない)。
    /// </summary>
    public void Move(Direction direction)
    {
        var next = Neighbor(PlayerPos, direction);
        if (!Map.IsWalkable(next))
        {
            return;
        }
        PlayerPos = next;
        switch (Map[next])
        {
            case Tile.StairsDown:
                CurrentFloor++;
                PlayerPos = Map.StairsUp;
                break;
            case Tile.StairsUp when CurrentFloor > 1:
                CurrentFloor--;
                PlayerPos = Map.StairsDown;
                break;
        }
    }

    private static GridPos Neighbor(GridPos pos, Direction direction) =>
        direction switch
        {
            Direction.North => pos with { Y = pos.Y - 1 },
            Direction.South => pos with { Y = pos.Y + 1 },
            Direction.West => pos with { X = pos.X - 1 },
            Direction.East => pos with { X = pos.X + 1 },
            _ => pos,
        };
}
