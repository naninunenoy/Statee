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
    public static bool CanSee(DungeonMap map, GridPos from, GridPos to) => default;
}
