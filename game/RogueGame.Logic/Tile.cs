namespace RogueGame.Logic;

/// <summary>ダンジョンの地形1マス。</summary>
public enum Tile
{
    /// <summary>壁。通行不可。</summary>
    Wall,

    /// <summary>床。通行可能。</summary>
    Floor,

    /// <summary>上り階段。1つ上のフロアへ移動できる。</summary>
    StairsUp,

    /// <summary>下り階段。1つ下のフロアへ移動できる。</summary>
    StairsDown,
}
