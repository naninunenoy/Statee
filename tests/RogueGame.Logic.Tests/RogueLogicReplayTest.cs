using Shouldly;

namespace RogueGame.Logic.Tests;

public class RogueLogicReplayTest
{
    // プレイヤーは < (1,1) から開始する
    private static readonly string[] Corridor = ["#########", "#<......#", "#########"];

    private static RogueLogic CreateGame(Enemy[] enemies, params Item[] items) =>
        new(_ => new Floor(MapText.Parse(Corridor), enemies, items));

    [Fact]
    public void Move_移動が成立_ActionLogに記録される()
    {
        var game = CreateGame([]);

        game.Move(Direction.East);

        game.ActionLog.ShouldBe([RogueAction.Move(Direction.East)]);
    }

    [Fact]
    public void Move_壁方向のノーオップ_それも記録される()
    {
        // リプレイは「入力列の再生」なので、無効入力も含めて記録する
        var game = CreateGame([]);

        game.Move(Direction.North);

        game.ActionLog.ShouldBe([RogueAction.Move(Direction.North)]);
    }

    [Fact]
    public void UseItem_未所持のノーオップ含め_順序どおり記録される()
    {
        var game = CreateGame([], new Item(new ItemId(1), ItemKind.Potion, new GridPos(2, 1)));

        game.Move(Direction.East);
        game.UseItem(ItemKind.Potion);
        game.UseItem(ItemKind.Potion); // 未所持ノーオップ

        game.ActionLog.ShouldBe([
            RogueAction.Move(Direction.East),
            RogueAction.Use(ItemKind.Potion),
            RogueAction.Use(ItemKind.Potion),
        ]);
    }

    [Fact]
    public void Apply_Moveアクション_Move呼び出しと同じ効果()
    {
        var game = CreateGame([]);

        game.Apply(RogueAction.Move(Direction.East));

        game.PlayerPos.ShouldBe(new GridPos(2, 1));
    }

    [Fact]
    public void Apply_Useアクション_UseItem呼び出しと同じ効果()
    {
        var game = CreateGame([], new Item(new ItemId(1), ItemKind.Potion, new GridPos(2, 1)));
        game.Apply(RogueAction.Move(Direction.East));
        game.Inventory.ShouldBe([ItemKind.Potion]); // 前提: 拾えている(空虚な合格の防止)

        game.Apply(RogueAction.Use(ItemKind.Potion));

        game.Inventory.ShouldBeEmpty();
    }

    [Fact]
    public void Replay_実生成ダンジョンでの行動記録_同一シードで同一状態を再現する()
    {
        // 特性テスト(GUIDELINE 2.7): マップの中身は知らずに、
        // 決定論(同一シード+同一アクション列 → 同一状態)だけを検証する
        const int seed = 20260710;
        var original = new RogueLogic(seed);
        foreach (var action in WanderActions())
        {
            original.Apply(action);
        }

        original.ActionLog.ShouldNotBeEmpty(); // 前提: 記録されている(空虚な合格の防止)
        var replayed = RogueLogic.Replay(seed, original.ActionLog);

        replayed.CurrentFloor.ShouldBe(original.CurrentFloor);
        replayed.PlayerPos.ShouldBe(original.PlayerPos);
        replayed.PlayerHp.ShouldBe(original.PlayerHp);
        replayed.PlayerAttack.ShouldBe(original.PlayerAttack);
        replayed.Inventory.ShouldBe(original.Inventory);
        replayed.HasGem.ShouldBe(original.HasGem);
        replayed.IsCleared.ShouldBe(original.IsCleared);
        replayed.IsGameOver.ShouldBe(original.IsGameOver);
        Snapshot(replayed.Enemies).ShouldBe(Snapshot(original.Enemies));
        replayed
            .Items.Select(item => (item.Id, item.Kind, item.Pos))
            .ShouldBe(original.Items.Select(item => (item.Id, item.Kind, item.Pos)));
        replayed.ActionLog.ShouldBe(original.ActionLog);
    }

    private static IEnumerable<(EnemyId, GridPos, int)> Snapshot(IEnumerable<Enemy> enemies) =>
        enemies.Select(enemy => (enemy.Id, enemy.Pos, enemy.Hp));

    /// <summary>
    /// 壁バンプ・戦闘・拾得が自然に混ざるよう、四方をうろつく決定的なアクション列。
    /// </summary>
    private static IEnumerable<RogueAction> WanderActions()
    {
        Direction[] pattern =
        [
            Direction.East,
            Direction.East,
            Direction.South,
            Direction.West,
            Direction.South,
            Direction.East,
            Direction.North,
            Direction.West,
        ];
        for (var i = 0; i < 200; i++)
        {
            yield return RogueAction.Move(pattern[i % pattern.Length]);
            if (i % 17 == 0)
            {
                yield return RogueAction.Use(ItemKind.Potion);
            }
        }
    }
}
