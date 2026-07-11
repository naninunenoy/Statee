using Shouldly;

namespace RaidBoss.Logic.Tests;

public class GameLogicTest
{
    [Fact]
    public void 開始直後_双方全快でボスHPも全快でPlayingになる()
    {
        var game = new GameLogic(seed: 1);

        game.Phase.ShouldBe(GamePhase.Playing);
        game.BossHp.ShouldBe(GameLogic.BossMaxHp);
        game.Player1Hp.ShouldBe(GameLogic.PlayerMaxHp);
        game.Player2Hp.ShouldBe(GameLogic.PlayerMaxHp);
        game.TickCount.ShouldBe(0);
    }

    [Fact]
    public void Step_双方Idle_TickCountだけ進みHPは変化しない()
    {
        var game = new GameLogic(seed: 1);

        game.Step(PlayerAction.Idle, PlayerAction.Idle);

        game.TickCount.ShouldBe(1);
        game.BossHp.ShouldBe(GameLogic.BossMaxHp);
        game.Player1Hp.ShouldBe(GameLogic.PlayerMaxHp);
        game.Player2Hp.ShouldBe(GameLogic.PlayerMaxHp);
    }

    [Fact]
    public void Step_Player1がAttack_ボスHPが攻撃力分減る()
    {
        var game = new GameLogic(seed: 1);

        game.Step(PlayerAction.Attack, PlayerAction.Idle);

        game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage);
    }

    [Fact]
    public void Step_双方がAttack_ボスHPが攻撃力の2倍減る()
    {
        var game = new GameLogic(seed: 1);

        game.Step(PlayerAction.Attack, PlayerAction.Attack);

        game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage * 2);
    }

    [Fact]
    public void ボスHPが0以下になるとVictoryへ遷移する()
    {
        var game = new GameLogic(seed: 1);
        var attacksNeeded =
            (GameLogic.BossMaxHp + GameLogic.PlayerAttackDamage * 2 - 1)
            / (GameLogic.PlayerAttackDamage * 2);

        for (var i = 0; i < attacksNeeded; i++)
        {
            game.Step(PlayerAction.Attack, PlayerAction.Attack);
        }

        game.Phase.ShouldBe(GamePhase.Victory);
        game.BossHp.ShouldBeLessThanOrEqualTo(0);
    }

    [Fact]
    public void ボスは一定周期でプレイヤーを交互に攻撃する()
    {
        var game = new GameLogic(seed: 1);

        for (var i = 0; i < GameLogic.BossAttackInterval; i++)
        {
            game.Step(PlayerAction.Idle, PlayerAction.Idle);
        }

        game.Player1Hp.ShouldBe(GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage);
        game.Player2Hp.ShouldBe(GameLogic.PlayerMaxHp);

        for (var i = 0; i < GameLogic.BossAttackInterval; i++)
        {
            game.Step(PlayerAction.Idle, PlayerAction.Idle);
        }

        game.Player1Hp.ShouldBe(GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage);
        game.Player2Hp.ShouldBe(GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage);
    }

    [Fact]
    public void 両プレイヤーのHPが0以下になるとDefeatへ遷移する()
    {
        var game = new GameLogic(seed: 1);
        var ticksNeeded =
            GameLogic.BossAttackInterval
            * (
                (GameLogic.PlayerMaxHp + GameLogic.BossAttackDamage - 1)
                / GameLogic.BossAttackDamage
            )
            * 2;

        for (var i = 0; i < ticksNeeded && game.Phase == GamePhase.Playing; i++)
        {
            game.Step(PlayerAction.Idle, PlayerAction.Idle);
        }

        game.Phase.ShouldBe(GamePhase.Defeat);
    }

    [Fact]
    public void 同一シード同一入力列なら2回実行しても結果が完全一致する()
    {
        PlayerAction[] p1Actions =
        [
            PlayerAction.Attack,
            PlayerAction.Idle,
            PlayerAction.Attack,
            PlayerAction.Attack,
        ];
        PlayerAction[] p2Actions =
        [
            PlayerAction.Idle,
            PlayerAction.Attack,
            PlayerAction.Attack,
            PlayerAction.Idle,
        ];

        var first = new GameLogic(seed: 42);
        var second = new GameLogic(seed: 42);
        for (var i = 0; i < p1Actions.Length; i++)
        {
            first.Step(p1Actions[i], p2Actions[i]);
            second.Step(p1Actions[i], p2Actions[i]);
        }

        first.BossHp.ShouldBe(second.BossHp);
        first.Player1Hp.ShouldBe(second.Player1Hp);
        first.Player2Hp.ShouldBe(second.Player2Hp);
        first.Phase.ShouldBe(second.Phase);
    }
}
