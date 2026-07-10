using Shouldly;

namespace RogueGame.Logic.Tests;

public class RogueLogicGemTest
{
    // フロア1: < (1,1) 開始、> (3,1)。フロア2: < (1,1)、💎 (2,1)
    private static readonly string[] Floor1 = ["######", "#<.>.#", "######"];
    private static readonly string[] Floor2 = ["######", "#<...#", "######"];

    private static readonly string[] LongCorridor =
    [
        "############",
        "#<.........#",
        "############",
    ];

    private static RogueLogic CreateTwoFloorGame() =>
        new(floorNumber =>
            floorNumber == 1
                ? new Floor(MapText.Parse(Floor1), [])
                : new Floor(
                    MapText.Parse(Floor2),
                    [],
                    [new Item(new ItemId(1), ItemKind.Gem, new GridPos(2, 1))]
                )
        );

    private static void MoveToGem(RogueLogic game)
    {
        game.Move(Direction.East);
        game.Move(Direction.East); // フロア2へ
        game.Move(Direction.East); // 💎 を拾う
    }

    private static void ReturnToSurface(RogueLogic game)
    {
        game.Move(Direction.West); // フロア2の上り階段 → フロア1の下り階段 (3,1)
        game.Move(Direction.West); // 増援 (2,1) を攻撃
        game.Move(Direction.West); // 増援 (2,1) を倒す
        game.Move(Direction.West); // (2,1) へ移動
        game.Move(Direction.West); // 上り階段 → 脱出
    }

    [Fact]
    public void Move_宝のマスへ移動_HasGemがtrueになりフロアから消える()
    {
        var game = CreateTwoFloorGame();

        MoveToGem(game);

        game.HasGem.ShouldBeTrue();
        game.Items.ShouldBeEmpty();
        game.Inventory.ShouldBeEmpty();
    }

    [Fact]
    public void 宝取得_現在フロアに増援が湧く()
    {
        var game = CreateTwoFloorGame();

        MoveToGem(game);

        game.Enemies.Count.ShouldBe(RogueConfig.ReinforcementsPerFloor);
    }

    [Fact]
    public void 宝取得_訪問済みフロアにも増援が湧いている()
    {
        var game = CreateTwoFloorGame();
        MoveToGem(game);

        game.Move(Direction.West); // フロア2の上り階段へ → フロア1に戻る

        game.CurrentFloor.ShouldBe(1);
        game.Enemies.Count.ShouldBe(RogueConfig.ReinforcementsPerFloor);
    }

    [Fact]
    public void 宝取得_増援は空いている床の上に湧き重ならない()
    {
        var game = CreateTwoFloorGame();

        MoveToGem(game);

        game.Enemies.ShouldAllBe(enemy => game.Map[enemy.Pos] == Tile.Floor);
        game.Enemies.Select(enemy => enemy.Pos).ShouldBeUnique();
        game.Enemies.ShouldAllBe(enemy => enemy.Pos != game.PlayerPos);
    }

    [Fact]
    public void 宝取得後_視界外の敵も追跡してくる()
    {
        var enemy = new Enemy(new EnemyId(1), new GridPos(10, 1), hp: 100, attack: 1);
        var game = new RogueLogic(_ => new Floor(
            MapText.Parse(LongCorridor),
            [enemy],
            [new Item(new ItemId(1), ItemKind.Gem, new GridPos(2, 1))]
        ));

        game.Move(Direction.East); // 💎 を拾う。敵との距離は SightRange 超

        enemy.Pos.ShouldBe(new GridPos(9, 1));
    }

    [Fact]
    public void 宝を持ってフロア1の上り階段に乗る_クリアになる()
    {
        var game = CreateTwoFloorGame();
        MoveToGem(game);

        ReturnToSurface(game);

        game.IsCleared.ShouldBeTrue();
    }

    [Fact]
    public void 宝を持たずにフロア1の上り階段に乗る_クリアにならない()
    {
        var game = CreateTwoFloorGame();

        game.Move(Direction.East);
        game.Move(Direction.West); // 上り階段に乗り直す

        game.IsCleared.ShouldBeFalse();
    }

    [Fact]
    public void クリア後_アクションは何も起きない()
    {
        var game = CreateTwoFloorGame();
        MoveToGem(game);
        ReturnToSurface(game);

        var posAtClear = game.PlayerPos;
        var hpAtClear = game.PlayerHp;
        game.Move(Direction.East);

        game.PlayerPos.ShouldBe(posAtClear);
        game.PlayerHp.ShouldBe(hpAtClear);
    }
}
