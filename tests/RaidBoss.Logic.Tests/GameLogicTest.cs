using Shouldly;

namespace RaidBoss.Logic.Tests;

public class GameLogicTest
{
    [Fact]
    public void 生成直後はWaitingでボスHPも全快()
    {
        var game = new GameLogic(seed: 1);

        game.Phase.ShouldBe(GamePhase.Waiting);
        game.BossHp.ShouldBe(GameLogic.BossMaxHp);
        game.TickCount.ShouldBe(0);
    }

    [Fact]
    public void Start_人数分のプレイヤーHPが全快でPlayingになる()
    {
        var game = new GameLogic(seed: 1);

        game.Start(playerCount: 3);

        game.Phase.ShouldBe(GamePhase.Playing);
        game.PlayerCount.ShouldBe(3);
        game.PlayerHps.ShouldBe([
            GameLogic.PlayerMaxHp,
            GameLogic.PlayerMaxHp,
            GameLogic.PlayerMaxHp,
        ]);
    }

    [Fact]
    public void Step_全員Idle_TickCountだけ進みHPは変化しない()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2);

        game.Step([PlayerAction.Idle, PlayerAction.Idle]);

        game.TickCount.ShouldBe(1);
        game.BossHp.ShouldBe(GameLogic.BossMaxHp);
        game.PlayerHps.ShouldBe([GameLogic.PlayerMaxHp, GameLogic.PlayerMaxHp]);
    }

    [Fact]
    public void Step_1人がAttack_ボスHPが攻撃力分減る()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2);

        game.Step([PlayerAction.Attack, PlayerAction.Idle]);

        game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage);
    }

    [Fact]
    public void Step_全員がAttack_ボスHPが人数分減る()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 3);

        game.Step([PlayerAction.Attack, PlayerAction.Attack, PlayerAction.Attack]);

        game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage * 3);
    }

    [Fact]
    public void ボスHPが0以下になるとVictoryへ遷移する()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2);
        var attacksNeeded =
            (GameLogic.BossMaxHp + GameLogic.PlayerAttackDamage * 2 - 1)
            / (GameLogic.PlayerAttackDamage * 2);

        for (var i = 0; i < attacksNeeded; i++)
        {
            game.Step([PlayerAction.Attack, PlayerAction.Attack]);
        }

        game.Phase.ShouldBe(GamePhase.Victory);
        game.BossHp.ShouldBeLessThanOrEqualTo(0);
    }

    [Fact]
    public void ボスは一定周期でプレイヤーを順番に巡回攻撃する()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 3);

        for (var i = 0; i < GameLogic.BossAttackInterval; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle, PlayerAction.Idle]);
        }

        game.PlayerHps.ShouldBe([
            GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage,
            GameLogic.PlayerMaxHp,
            GameLogic.PlayerMaxHp,
        ]);

        for (var i = 0; i < GameLogic.BossAttackInterval; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle, PlayerAction.Idle]);
        }

        game.PlayerHps.ShouldBe([
            GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage,
            GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage,
            GameLogic.PlayerMaxHp,
        ]);
    }

    [Fact]
    public void Start_1人でも開始できる()
    {
        var game = new GameLogic(seed: 1);

        game.Start(playerCount: GameLogic.MinPlayerCount);

        game.Phase.ShouldBe(GamePhase.Playing);
        game.PlayerCount.ShouldBe(1);
    }

    [Fact]
    public void ソロプレイでHPが0以下になると即Defeatへ遷移する()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 1);
        var hitsNeeded =
            (GameLogic.PlayerMaxHp + GameLogic.BossAttackDamage - 1) / GameLogic.BossAttackDamage;
        var ticksNeeded = hitsNeeded * GameLogic.BossAttackInterval;

        for (var i = 0; i < ticksNeeded && game.Phase == GamePhase.Playing; i++)
        {
            game.Step([PlayerAction.Idle]);
        }

        game.Phase.ShouldBe(GamePhase.Defeat);
    }

    [Fact]
    public void HPが0以下になると一定時間操作不能になり明けると回復する()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2);
        var hitsNeeded =
            (GameLogic.PlayerMaxHp + GameLogic.BossAttackDamage - 1) / GameLogic.BossAttackDamage;
        // 2人交互ターゲットなので、プレイヤー0(奇数attackNumber)が hitsNeeded 回目に倒れるTickを求める
        var attackNumberToDownPlayer0 = hitsNeeded * 2 - 1;
        var ticksToDown = attackNumberToDownPlayer0 * GameLogic.BossAttackInterval;

        for (var i = 0; i < ticksToDown; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }

        game.PlayerHps[0].ShouldBeLessThanOrEqualTo(0);
        game.IncapacitatedTicks[0].ShouldBe(GameLogic.IncapacitationDuration);
        game.Phase.ShouldBe(GamePhase.Playing);

        var bossHpBeforeIgnoredAttack = game.BossHp;
        game.Step([PlayerAction.Attack, PlayerAction.Idle]); // 操作不能中はAttackが無視される
        game.BossHp.ShouldBe(bossHpBeforeIgnoredAttack);
        game.IncapacitatedTicks[0].ShouldBe(GameLogic.IncapacitationDuration - 1);

        for (var i = 0; i < GameLogic.IncapacitationDuration - 1; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }

        game.IncapacitatedTicks[0].ShouldBe(0);
        game.PlayerHps[0].ShouldBe(GameLogic.ReviveHp);
    }

    [Fact]
    public void 同一シード同一入力列なら2回実行しても結果が完全一致する()
    {
        PlayerAction[][] actionsPerTick =
        [
            [PlayerAction.Attack, PlayerAction.Idle],
            [PlayerAction.Idle, PlayerAction.Attack],
            [PlayerAction.Attack, PlayerAction.Attack],
            [PlayerAction.Attack, PlayerAction.Idle],
        ];

        var first = new GameLogic(seed: 42);
        var second = new GameLogic(seed: 42);
        first.Start(playerCount: 2);
        second.Start(playerCount: 2);
        foreach (var actions in actionsPerTick)
        {
            first.Step(actions);
            second.Step(actions);
        }

        first.BossHp.ShouldBe(second.BossHp);
        first.PlayerHps.ShouldBe(second.PlayerHps);
        first.Phase.ShouldBe(second.Phase);
    }
}
