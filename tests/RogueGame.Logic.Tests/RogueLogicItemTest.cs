using Shouldly;

namespace RogueGame.Logic.Tests;

public class RogueLogicItemTest
{
    // プレイヤーは < (1,1) から開始する
    private static readonly string[] Corridor = ["#########", "#<......#", "#########"];

    private static RogueLogic CreateGame(Enemy[] enemies, params Item[] items) =>
        new(_ => new Floor(MapText.Parse(Corridor), enemies, items));

    private static Item CreateItem(ItemKind kind, GridPos pos) => new(new ItemId(1), kind, pos);

    [Fact]
    public void コンストラクタ_初期状態_攻撃力は設定どおりでインベントリは空()
    {
        var game = CreateGame([]);

        game.PlayerAttack.ShouldBe(RogueConfig.PlayerAttack);
        game.Inventory.ShouldBeEmpty();
    }

    [Fact]
    public void Move_ポーションのマスへ移動_拾ってインベントリに入る()
    {
        var game = CreateGame([], CreateItem(ItemKind.Potion, new GridPos(2, 1)));

        game.Move(Direction.East);

        game.Inventory.ShouldBe([ItemKind.Potion]);
        game.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Move_剣のマスへ移動_攻撃力が上がりインベントリに入らない()
    {
        var game = CreateGame([], CreateItem(ItemKind.Sword, new GridPos(2, 1)));

        game.Move(Direction.East);

        game.PlayerAttack.ShouldBe(RogueConfig.PlayerAttack + RogueConfig.SwordAttackBonus);
        game.Inventory.ShouldBeEmpty();
        game.Items.ShouldBeEmpty();
    }

    [Fact]
    public void UseItem_ポーション所持でHPが減っている_回復して消費される()
    {
        // 敵は最初の隣接攻撃(6ダメージ)の後、反撃で倒される
        var enemy = new Enemy(
            new EnemyId(1),
            new GridPos(3, 1),
            hp: RogueConfig.PlayerAttack,
            attack: 6
        );
        var game = CreateGame([enemy], CreateItem(ItemKind.Potion, new GridPos(2, 1)));
        game.Move(Direction.East); // ポーションを拾い、隣接する敵に攻撃される(HP 4)
        game.Move(Direction.East); // 敵を倒す

        game.UseItem(ItemKind.Potion);

        game.PlayerHp.ShouldBe(RogueConfig.PlayerHp - 6 + RogueConfig.PotionHeal);
        game.Inventory.ShouldBeEmpty();
    }

    [Fact]
    public void UseItem_HPが満タン_初期HPを超えて回復しない()
    {
        var game = CreateGame([], CreateItem(ItemKind.Potion, new GridPos(2, 1)));
        game.Move(Direction.East);

        game.UseItem(ItemKind.Potion);

        game.PlayerHp.ShouldBe(RogueConfig.PlayerHp);
    }

    [Fact]
    public void UseItem_使用が成立_ターンを消費して敵が動く()
    {
        var enemy = new Enemy(new EnemyId(1), new GridPos(6, 1), hp: 100, attack: 1);
        var game = CreateGame([enemy], CreateItem(ItemKind.Potion, new GridPos(2, 1)));
        game.Move(Direction.East); // 拾う。敵は (5,1) へ

        game.UseItem(ItemKind.Potion);

        enemy.Pos.ShouldBe(new GridPos(4, 1));
    }

    [Fact]
    public void UseItem_未所持_ターンを消費せず敵も動かない()
    {
        var enemy = new Enemy(new EnemyId(1), new GridPos(6, 1), hp: 100, attack: 1);
        var game = CreateGame([enemy]);

        game.UseItem(ItemKind.Potion);

        enemy.Pos.ShouldBe(new GridPos(6, 1));
    }
}
