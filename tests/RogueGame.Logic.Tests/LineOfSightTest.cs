using Shouldly;

namespace RogueGame.Logic.Tests;

public class LineOfSightTest
{
    // 中央 (4,2) に遮蔽壁が1つある部屋
    private static readonly string[] RoomWithPillar =
    [
        "############",
        "#<.........#",
        "#...#......#",
        "#.........>#",
        "############",
    ];

    [Theory]
    [InlineData(1, 1, 3, 3)] // 斜め近距離
    [InlineData(1, 2, 1, 2)] // 自分自身
    [InlineData(1, 1, 7, 1)] // ちょうど SightRange(6)
    public void CanSee_視界距離内で遮蔽なし_true(int fromX, int fromY, int toX, int toY)
    {
        var map = MapText.Parse(RoomWithPillar);

        LineOfSight.CanSee(map, new GridPos(fromX, fromY), new GridPos(toX, toY)).ShouldBeTrue();
    }

    [Fact]
    public void CanSee_SightRangeを超える距離_false()
    {
        var map = MapText.Parse(RoomWithPillar);

        LineOfSight.CanSee(map, new GridPos(1, 1), new GridPos(8, 1)).ShouldBeFalse();
    }

    [Fact]
    public void CanSee_間に壁がある_false()
    {
        var map = MapText.Parse(RoomWithPillar);

        // (1,2) → (7,2) の直線は遮蔽壁 (4,2) を通る
        LineOfSight.CanSee(map, new GridPos(1, 2), new GridPos(7, 2)).ShouldBeFalse();
    }
}
