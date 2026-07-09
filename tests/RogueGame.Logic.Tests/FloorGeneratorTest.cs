using Shouldly;

namespace RogueGame.Logic.Tests;

public class FloorGeneratorTest
{
    [Fact]
    public void Generate_同一シードと同一フロア番号_同一の敵配置を生成する()
    {
        var first = FloorGenerator.Generate(seed: 12345, floorNumber: 1);
        var second = FloorGenerator.Generate(seed: 12345, floorNumber: 1);

        second
            .Enemies.Select(enemy => (enemy.Id, enemy.Pos))
            .ShouldBe(first.Enemies.Select(enemy => (enemy.Id, enemy.Pos)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Generate_任意のフロア番号_敵数は設定どおり(int floorNumber)
    {
        var floor = FloorGenerator.Generate(seed: 12345, floorNumber);

        floor.Enemies.Count.ShouldBe(RogueConfig.EnemyCount(floorNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    public void Generate_任意のシード_敵は階段以外の歩行可能タイルの上にいる(int seed)
    {
        var floor = FloorGenerator.Generate(seed, floorNumber: 1);

        foreach (var enemy in floor.Enemies)
        {
            floor.Map[enemy.Pos].ShouldBe(Tile.Floor, $"敵 {enemy.Id} の位置 {enemy.Pos}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    public void Generate_任意のシード_敵のIDと位置は互いに重複しない(int seed)
    {
        var floor = FloorGenerator.Generate(seed, floorNumber: 1);

        floor.Enemies.Select(enemy => enemy.Id).ShouldBeUnique();
        floor.Enemies.Select(enemy => enemy.Pos).ShouldBeUnique();
    }
}
