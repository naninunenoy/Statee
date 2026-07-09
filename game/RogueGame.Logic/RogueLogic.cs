namespace RogueGame.Logic;

/// <summary>
/// ローグライクの中核状態機械。アクション列 → 状態遷移の決定論的関数(docs/adr/D-044.md)。
/// フロアは離脱後も状態を保持する(帰路の増援・覚醒のため使い捨てない)。
/// </summary>
public sealed class RogueLogic
{
    private readonly Func<int, Floor> floorFactory;
    private readonly Dictionary<int, Floor> visitedFloors = [];

    /// <summary>本番用。シードからフロアを生成する。</summary>
    public RogueLogic(int seed)
        : this(floorNumber => FloorGenerator.Generate(seed, floorNumber)) { }

    /// <summary>フロアの供給元を注入する(テストで手組みフロアを使うためのシーム)。</summary>
    public RogueLogic(Func<int, Floor> floorFactory)
    {
        this.floorFactory = floorFactory;
        CurrentFloor = 1;
        PlayerPos = Map.StairsUp;
    }

    /// <summary>現在のフロア番号(1 起点。地上に最も近いのが 1)。</summary>
    public int CurrentFloor { get; private set; }

    /// <summary>現在フロアの地形。</summary>
    public DungeonMap Map => Floor.Map;

    /// <summary>現在フロアの生存している敵。</summary>
    public IReadOnlyList<Enemy> Enemies => Floor.Enemies;

    /// <summary>プレイヤーの現在位置。</summary>
    public GridPos PlayerPos { get; private set; }

    private Floor Floor =>
        visitedFloors.TryGetValue(CurrentFloor, out var floor)
            ? floor
            : visitedFloors[CurrentFloor] = floorFactory(CurrentFloor);

    /// <summary>
    /// 指定方向へ1マス移動する。壁方向なら何も起きない(ターンも消費しない)。
    /// 下り階段に乗ると次フロアへ、上り階段に乗ると前フロアへ自動遷移する
    /// (フロア1の上り階段は現時点では何も起きない)。
    /// 移動が成立したら敵のターンが進む。
    /// </summary>
    public void Move(Direction direction)
    {
        var next = Neighbor(PlayerPos, direction);
        if (!Map.IsWalkable(next) || Enemies.Any(enemy => enemy.Pos == next))
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
        ProcessEnemyTurn();
    }

    /// <summary>
    /// 敵のターン。プレイヤーが見えている敵は1歩近づく(直進追跡)。
    /// 見えていない敵は動かない。敵同士・プレイヤーとは重ならない。
    /// </summary>
    private void ProcessEnemyTurn()
    {
        foreach (var enemy in Enemies)
        {
            if (!LineOfSight.CanSee(Map, enemy.Pos, PlayerPos))
            {
                continue;
            }
            var step = ChaseStep(enemy);
            if (step is { } next)
            {
                enemy.Pos = next;
            }
        }
    }

    /// <summary>
    /// 直進追跡の1歩。差の大きい軸を優先し、塞がっていればもう一方の軸を試す。
    /// プレイヤー・他の敵・壁のマスへは進まない(進めなければ null)。
    /// </summary>
    private GridPos? ChaseStep(Enemy enemy)
    {
        var dx = PlayerPos.X - enemy.Pos.X;
        var dy = PlayerPos.Y - enemy.Pos.Y;
        var horizontal = enemy.Pos with { X = enemy.Pos.X + Math.Sign(dx) };
        var vertical = enemy.Pos with { Y = enemy.Pos.Y + Math.Sign(dy) };
        var candidates =
            Math.Abs(dx) >= Math.Abs(dy)
                ? (First: horizontal, Second: vertical)
                : (First: vertical, Second: horizontal);
        foreach (var next in (GridPos[])[candidates.First, candidates.Second])
        {
            if (
                next != enemy.Pos
                && next != PlayerPos
                && Map.IsWalkable(next)
                && !Enemies.Any(other => other.Pos == next)
            )
            {
                return next;
            }
        }
        return null;
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
