namespace Reversi.Logic;

/// <summary>
/// 画面座標 ⇔ 盤マスの変換。盤を Node2D 直描画にするため、
/// クリックのヒット判定はこの純C#層でテストする。
/// </summary>
public readonly record struct BoardGeometry(float OriginX, float OriginY, float CellSize)
{
    /// <summary>盤全体の一辺の長さ(px)。</summary>
    public float BoardLength => CellSize * Board.Size;

    /// <summary>画面座標が指すマス。盤の外なら null。</summary>
    public (int X, int Y)? CellAt(float px, float py)
    {
        var x = (int)MathF.Floor((px - OriginX) / CellSize);
        var y = (int)MathF.Floor((py - OriginY) / CellSize);
        if (px < OriginX || py < OriginY || x >= Board.Size || y >= Board.Size)
        {
            return null;
        }
        return (x, y);
    }

    /// <summary>マスの中心の画面座標。</summary>
    public (float X, float Y) CenterOf(int x, int y) =>
        (OriginX + x * CellSize + CellSize / 2f, OriginY + y * CellSize + CellSize / 2f);
}
