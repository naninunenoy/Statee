using Shouldly;

namespace RogueGame.Logic.Tests;

public class RogueLogicEnemyTurnTest
{
    // プレイヤーは < (1,1) から開始する
    private static readonly string[] Corridor = ["#########", "#<......#", "#########"];

    private static readonly string[] LongCorridor =
    [
        "############",
        "#<.........#",
        "############",
    ];

    private static readonly string[] CorridorWithPillar = ["#########", "#<..#...#", "#########"];

    private static RogueLogic CreateGame(string[] rows, params Enemy[] enemies) =>
        new(_ => new Floor(MapText.Parse(rows), enemies));

    private static Enemy CreateEnemy(int id, GridPos pos) =>
        new(new EnemyId(id), pos, RogueConfig.EnemyHp, RogueConfig.EnemyAttack);

    [Fact]
    public void Move_視界内に敵がいる_敵がプレイヤーへ1歩近づく()
    {
        var enemy = CreateEnemy(1, new GridPos(5, 1));
        var game = CreateGame(Corridor, enemy);

        game.Move(Direction.East);

        enemy.Pos.ShouldBe(new GridPos(4, 1));
    }

    [Fact]
    public void Move_SightRangeを超えて敵が離れている_敵は動かない()
    {
        var enemy = CreateEnemy(1, new GridPos(10, 1));
        var game = CreateGame(LongCorridor, enemy);

        game.Move(Direction.East);

        enemy.Pos.ShouldBe(new GridPos(10, 1));
    }

    [Fact]
    public void Move_敵との間に壁がある_敵は動かない()
    {
        var enemy = CreateEnemy(1, new GridPos(6, 1));
        var game = CreateGame(CorridorWithPillar, enemy);

        game.Move(Direction.East);

        enemy.Pos.ShouldBe(new GridPos(6, 1));
    }

    [Fact]
    public void Move_壁方向への移動_ターンは進まず敵も動かない()
    {
        var enemy = CreateEnemy(1, new GridPos(5, 1));
        var game = CreateGame(Corridor, enemy);

        game.Move(Direction.North);

        enemy.Pos.ShouldBe(new GridPos(5, 1));
    }

    [Fact]
    public void Move_敵がプレイヤーに隣接している_敵はプレイヤーに重ならない()
    {
        var enemy = CreateEnemy(1, new GridPos(3, 1));
        var game = CreateGame(Corridor, enemy);

        game.Move(Direction.East); // プレイヤー (2,1)。敵の次の一歩はプレイヤーのマス

        enemy.Pos.ShouldBe(new GridPos(3, 1));
    }

    [Fact]
    public void Move_一列に並んだ敵が追跡する_敵同士は重ならない()
    {
        var front = CreateEnemy(1, new GridPos(4, 1));
        var back = CreateEnemy(2, new GridPos(5, 1));
        var game = CreateGame(Corridor, front, back);

        game.Move(Direction.East);

        game.Enemies.Select(enemy => enemy.Pos).ShouldBeUnique();
    }

    [Fact]
    public void Move_敵のいるマスへ移動する_移動できない()
    {
        var enemy = CreateEnemy(1, new GridPos(2, 1));
        var game = CreateGame(Corridor, enemy);

        game.Move(Direction.East);

        game.PlayerPos.ShouldBe(new GridPos(1, 1));
    }
}
