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

    [Fact]
    public void Generate_同一シードと同一フロア番号_同一のアイテム配置を生成する()
    {
        var first = FloorGenerator.Generate(seed: 12345, floorNumber: 1);
        var second = FloorGenerator.Generate(seed: 12345, floorNumber: 1);

        second
            .Items.Select(item => (item.Id, item.Kind, item.Pos))
            .ShouldBe(first.Items.Select(item => (item.Id, item.Kind, item.Pos)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    public void Generate_任意のシード_ポーションが設定数だけ床の上に配置される(int seed)
    {
        var floor = FloorGenerator.Generate(seed, floorNumber: 1);

        var potions = floor.Items.Where(item => item.Kind == ItemKind.Potion).ToList();
        potions.Count.ShouldBe(RogueConfig.PotionsPerFloor);
        potions.ShouldAllBe(item => floor.Map[item.Pos] == Tile.Floor);
    }

    [Fact]
    public void Generate_剣のフロア_剣が1本だけ配置される()
    {
        var floor = FloorGenerator.Generate(seed: 12345, floorNumber: RogueConfig.SwordFloor);

        floor.Items.Count(item => item.Kind == ItemKind.Sword).ShouldBe(1);
    }

    [Fact]
    public void Generate_剣のフロア以外_剣は配置されない()
    {
        var floor = FloorGenerator.Generate(seed: 12345, floorNumber: 1);

        floor.Items.ShouldAllBe(item => item.Kind != ItemKind.Sword);
    }

    [Fact]
    public void Generate_最下層_宝が1つだけ配置される()
    {
        var floor = FloorGenerator.Generate(seed: 12345, floorNumber: RogueConfig.FloorCount);

        floor.Items.Count(item => item.Kind == ItemKind.Gem).ShouldBe(1);
    }

    [Fact]
    public void Generate_最下層以外_宝は配置されない()
    {
        var floor = FloorGenerator.Generate(seed: 12345, floorNumber: 1);

        floor.Items.ShouldAllBe(item => item.Kind != ItemKind.Gem);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12345)]
    [InlineData(-1)]
    public void Generate_任意のシード_アイテムと敵の位置は互いに重複しない(int seed)
    {
        var floor = FloorGenerator.Generate(seed, floorNumber: RogueConfig.SwordFloor);

        floor
            .Items.Select(item => item.Pos)
            .Concat(floor.Enemies.Select(enemy => enemy.Pos))
            .ShouldBeUnique();
    }
}
