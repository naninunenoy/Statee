using Shouldly;

namespace RogueGame.Logic.Tests;

public class RogueLogicMoveTest
{
    // # = 壁, . = 床, < = 上り階段, > = 下り階段
    private static readonly string[] CorridorFloor = ["#####", "#<.>#", "#####"];

    private static RogueLogic CreateGame() => new(_ => new Floor(MapText.Parse(CorridorFloor), []));

    [Fact]
    public void コンストラクタ_初期状態_フロア1の上り階段に立つ()
    {
        var game = CreateGame();

        game.CurrentFloor.ShouldBe(1);
        game.PlayerPos.ShouldBe(game.Map.StairsUp);
    }

    [Fact]
    public void Move_床方向_1マス移動する()
    {
        var game = CreateGame();

        game.Move(Direction.East);

        game.PlayerPos.ShouldBe(new GridPos(2, 1));
    }

    [Fact]
    public void Move_壁方向_位置が変わらない()
    {
        var game = CreateGame();

        game.Move(Direction.North);

        game.PlayerPos.ShouldBe(game.Map.StairsUp);
    }

    [Fact]
    public void Move_下り階段に乗る_次フロアの上り階段に立つ()
    {
        var game = CreateGame();

        game.Move(Direction.East);
        game.Move(Direction.East);

        game.CurrentFloor.ShouldBe(2);
        game.PlayerPos.ShouldBe(game.Map.StairsUp);
    }

    [Fact]
    public void Move_上り階段に乗る_前フロアの下り階段に立つ()
    {
        var game = CreateGame();
        game.Move(Direction.East);
        game.Move(Direction.East); // フロア2へ。上り階段の上にいる

        game.Move(Direction.East); // 階段から降りる
        game.Move(Direction.West); // 上り階段に乗り直す

        game.CurrentFloor.ShouldBe(1);
        game.PlayerPos.ShouldBe(game.Map.StairsDown);
    }

    [Fact]
    public void Move_フロア1の上り階段に乗る_フロアは変わらない()
    {
        var game = CreateGame();

        game.Move(Direction.East); // 階段から降りる
        game.Move(Direction.West); // 上り階段に乗り直す

        game.CurrentFloor.ShouldBe(1);
        game.PlayerPos.ShouldBe(game.Map.StairsUp);
    }

    [Fact]
    public void Move_離脱したフロアへ戻る_同一のフロアが保持されている()
    {
        // 呼ばれるたびに別インスタンスを返すファクトリで、再生成でなく保持を観測する
        var game = new RogueLogic(_ => new Floor(MapText.Parse(CorridorFloor), []));
        var floor1Map = game.Map;
        game.Move(Direction.East);
        game.Move(Direction.East); // フロア2へ

        game.Move(Direction.East);
        game.Move(Direction.West); // フロア1へ戻る

        game.CurrentFloor.ShouldBe(1);
        game.Map.ShouldBeSameAs(floor1Map);
    }
}
