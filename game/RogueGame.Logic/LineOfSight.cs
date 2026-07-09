namespace RogueGame.Logic;

/// <summary>
/// 視界判定。敵 AI の「見えたら追跡」と描画層の Fog of War で共用する(docs/adr/D-044.md)。
/// </summary>
public static class LineOfSight
{
    /// <summary>視界の届く最大距離(チェビシェフ距離)。</summary>
    public const int SightRange = 6;

    /// <summary>
    /// from から to が見えるかを返す。
    /// 距離が <see cref="SightRange"/> 以内で、間の直線上に壁がないとき true。
    /// </summary>
    public static bool CanSee(DungeonMap map, GridPos from, GridPos to)
    {
        var distance = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
        if (distance > SightRange)
        {
            return false;
        }
        return LinePositions(from, to).All(pos => map[pos] != Tile.Wall);
    }

    /// <summary>from から to への直線(Bresenham)。両端を含む。</summary>
    private static IEnumerable<GridPos> LinePositions(GridPos from, GridPos to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = -Math.Abs(to.Y - from.Y);
        var stepX = from.X < to.X ? 1 : -1;
        var stepY = from.Y < to.Y ? 1 : -1;
        var error = dx + dy;
        var current = from;
        while (true)
        {
            yield return current;
            if (current == to)
            {
                yield break;
            }
            var doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                current = current with { X = current.X + stepX };
            }
            if (doubledError <= dx)
            {
                error += dx;
                current = current with { Y = current.Y + stepY };
            }
        }
    }
}
