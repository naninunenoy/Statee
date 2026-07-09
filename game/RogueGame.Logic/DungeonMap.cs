namespace RogueGame.Logic;

/// <summary>
/// 生成済みの1フロア分の地形。不変。
/// Entity(プレイヤー・敵・アイテム)は含まず、地形と階段位置のみを持つ。
/// </summary>
public sealed class DungeonMap
{
    private readonly Tile[,] tiles;

    public DungeonMap(Tile[,] tiles, GridPos stairsUp, GridPos stairsDown)
    {
        this.tiles = tiles;
        StairsUp = stairsUp;
        StairsDown = stairsDown;
    }

    /// <summary>マップの幅(マス数)。</summary>
    public int Width => tiles.GetLength(0);

    /// <summary>マップの高さ(マス数)。</summary>
    public int Height => tiles.GetLength(1);

    /// <summary>上り階段の位置。1フロア目では地上への出口を兼ねる。</summary>
    public GridPos StairsUp { get; }

    /// <summary>下り階段の位置。</summary>
    public GridPos StairsDown { get; }

    /// <summary>指定位置の地形を返す。</summary>
    public Tile this[GridPos pos] => tiles[pos.X, pos.Y];

    /// <summary>指定位置が歩行可能(壁でない)かを返す。</summary>
    public bool IsWalkable(GridPos pos) => default;
}
