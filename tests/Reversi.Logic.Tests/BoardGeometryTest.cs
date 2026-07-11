using Shouldly;

namespace Reversi.Logic.Tests;

public class BoardGeometryTest
{
    // 原点 (40,60)・マス 52px の標準レイアウト
    private static readonly BoardGeometry Geometry = new(OriginX: 40f, OriginY: 60f, CellSize: 52f);

    [Fact]
    public void CellAt_左上マスの内部_0_0()
    {
        Geometry.CellAt(41f, 61f).ShouldBe((0, 0));
        Geometry.CellAt(91.9f, 111.9f).ShouldBe((0, 0));
    }

    [Fact]
    public void CellAt_右下マスの内部_7_7()
    {
        Geometry.CellAt(40f + 52f * 7 + 1f, 60f + 52f * 7 + 1f).ShouldBe((7, 7));
    }

    [Fact]
    public void CellAt_盤の外_null()
    {
        Geometry.CellAt(39.9f, 61f).ShouldBeNull(); // 左外
        Geometry.CellAt(41f, 59.9f).ShouldBeNull(); // 上外
        Geometry.CellAt(40f + 52f * 8 + 0.1f, 61f).ShouldBeNull(); // 右外
        Geometry.CellAt(41f, 60f + 52f * 8 + 0.1f).ShouldBeNull(); // 下外
    }

    [Fact]
    public void CenterOf_マス中心の座標を返す()
    {
        Geometry.CenterOf(0, 0).ShouldBe((40f + 26f, 60f + 26f));
        Geometry.CenterOf(7, 7).ShouldBe((40f + 52f * 7 + 26f, 60f + 52f * 7 + 26f));
    }

    [Fact]
    public void CellAt_CenterOf_往復が一致する()
    {
        for (var y = 0; y < Board.Size; y++)
        {
            for (var x = 0; x < Board.Size; x++)
            {
                var (px, py) = Geometry.CenterOf(x, y);
                Geometry.CellAt(px, py).ShouldBe((x, y));
            }
        }
    }
}
