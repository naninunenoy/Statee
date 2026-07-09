namespace RogueGame.Logic;

/// <summary>グリッド上の移動方向。</summary>
public enum Direction
{
    /// <summary>上(Y 減少)。</summary>
    North,

    /// <summary>下(Y 増加)。</summary>
    South,

    /// <summary>左(X 減少)。</summary>
    West,

    /// <summary>右(X 増加)。</summary>
    East,
}
