using Shouldly;

namespace RogueGame.Logic.Tests;

public class RogueLogicCombatTest
{
    // プレイヤーは < (1,1) から開始する
    private static readonly string[] Corridor = ["#########", "#<......#", "#########"];

    private static RogueLogic CreateGame(params Enemy[] enemies) =>
        new(_ => new Floor(MapText.Parse(Corridor), enemies));

    private static Enemy CreateEnemy(GridPos pos, int hp, int attack) =>
        new(new EnemyId(1), pos, hp, attack);

    [Fact]
    public void コンストラクタ_初期状態_PlayerHpは設定どおり()
    {
        var game = CreateGame();

        game.PlayerHp.ShouldBe(RogueConfig.PlayerHp);
    }

    [Fact]
    public void Move_敵のマスへ移動_敵にダメージを与え自分は動かない()
    {
        var enemy = CreateEnemy(new GridPos(2, 1), hp: 100, attack: 0);
        var game = CreateGame(enemy);

        game.Move(Direction.East);

        enemy.Hp.ShouldBe(100 - RogueConfig.PlayerAttack);
        game.PlayerPos.ShouldBe(new GridPos(1, 1));
    }

    [Fact]
    public void Move_攻撃で敵のHPが0以下_敵が消える()
    {
        var enemy = CreateEnemy(new GridPos(2, 1), hp: RogueConfig.PlayerAttack, attack: 0);
        var game = CreateGame(enemy);

        game.Move(Direction.East);

        game.Enemies.ShouldBeEmpty();
    }

    [Fact]
    public void Move_プレイヤーに隣接した敵のターン_プレイヤーが攻撃される()
    {
        var enemy = CreateEnemy(new GridPos(3, 1), hp: 100, attack: 1);
        var game = CreateGame(enemy);

        game.Move(Direction.East); // プレイヤー (2,1)。敵と隣接

        game.PlayerHp.ShouldBe(RogueConfig.PlayerHp - 1);
        enemy.Pos.ShouldBe(new GridPos(3, 1));
    }

    [Fact]
    public void Move_敵の攻撃でPlayerHpが0以下_ゲームオーバーになる()
    {
        var enemy = CreateEnemy(new GridPos(3, 1), hp: 100, attack: RogueConfig.PlayerHp);
        var game = CreateGame(enemy);

        game.Move(Direction.East);

        game.IsGameOver.ShouldBeTrue();
    }

    [Fact]
    public void Move_ゲームオーバー後_何も起きない()
    {
        var enemy = CreateEnemy(new GridPos(3, 1), hp: 100, attack: RogueConfig.PlayerHp);
        var game = CreateGame(enemy);
        game.Move(Direction.East); // ここでゲームオーバー

        game.Move(Direction.West);

        game.PlayerPos.ShouldBe(new GridPos(2, 1));
        game.PlayerHp.ShouldBe(0);
    }

    [Fact]
    public void Move_攻撃で敵を倒した_その敵はそのターン攻撃してこない()
    {
        var enemy = CreateEnemy(
            new GridPos(2, 1),
            hp: RogueConfig.PlayerAttack,
            attack: RogueConfig.PlayerHp
        );
        var game = CreateGame(enemy);

        game.Move(Direction.East); // 隣接する敵を倒す

        game.PlayerHp.ShouldBe(RogueConfig.PlayerHp);
    }
}
