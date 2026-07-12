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
    public void Start_人数分のプレイヤーHPが全快でPlayingになり中央寄りのレーンに並ぶ()
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
        game.PlayerLanes.ShouldBe([2, 3, 4]);
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
        game.Projectiles.ShouldBeEmpty();
    }

    [Fact]
    public void Step_1人がAttack_即ダメージではなく弾が発射される()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2);

        game.Step([PlayerAction.Attack, PlayerAction.Idle]);

        game.BossHp.ShouldBe(GameLogic.BossMaxHp);
        game.Projectiles.ShouldBe([new Projectile(OwnerIndex: 0, GameLogic.ProjectileTravelTicks)]);
    }

    [Fact]
    public void 弾はProjectileTravelTicks後に着弾しボスにダメージを与える()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2);

        game.Step([PlayerAction.Attack, PlayerAction.Idle]);
        for (var i = 0; i < GameLogic.ProjectileTravelTicks - 1; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
            game.BossHp.ShouldBe(GameLogic.BossMaxHp);
        }
        game.Step([PlayerAction.Idle, PlayerAction.Idle]);

        game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage);
        game.Projectiles.ShouldBeEmpty();
    }

    [Fact]
    public void Step_全員がAttack_着弾時にボスHPが人数分減る()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 3);

        game.Step([PlayerAction.Attack, PlayerAction.Attack, PlayerAction.Attack]);
        for (var i = 0; i < GameLogic.ProjectileTravelTicks; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle, PlayerAction.Idle]);
        }

        game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage * 3);
    }

    [Fact]
    public void 移動でレーンが1つずつ動き端で止まる()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 1); // 初期レーンは中央(3)

        game.Step([PlayerAction.MoveLeft]);
        game.PlayerLanes.ShouldBe([2]);

        for (var i = 0; i < GameLogic.LaneCount; i++)
        {
            game.Step([PlayerAction.MoveLeft]);
        }
        game.PlayerLanes.ShouldBe([0]); // 左端で止まる

        for (var i = 0; i < GameLogic.LaneCount * 2; i++)
        {
            game.Step([PlayerAction.MoveRight]);
        }
        game.PlayerLanes.ShouldBe([GameLogic.LaneCount - 1]); // 右端で止まる
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
        for (var i = 0; i < GameLogic.ProjectileTravelTicks; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }

        game.Phase.ShouldBe(GamePhase.Victory);
        game.BossHp.ShouldBeLessThanOrEqualTo(0);
    }

    [Fact]
    public void ボスは周期でレーンを予告し予告レーン外のプレイヤーは無傷()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2); // レーン [2, 3]

        for (var i = 0; i < GameLogic.BossAttackInterval; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }

        // Tick5(attackNumber=1)でレーン1が予告される
        game.PendingBossAttackLane.ShouldBe(1);
        game.PendingBossAttackTicks.ShouldBe(GameLogic.BossAttackWindupTicks);

        for (var i = 0; i < GameLogic.BossAttackWindupTicks; i++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }

        // 着弾したが予告レーンに誰もいないので無傷
        game.PendingBossAttackLane.ShouldBe(-1);
        game.PlayerHps.ShouldBe([GameLogic.PlayerMaxHp, GameLogic.PlayerMaxHp]);
    }

    [Fact]
    public void 予告レーンに居ると着弾時に被弾する()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 1); // レーン3

        game.Step([PlayerAction.MoveLeft]); // → 2
        game.Step([PlayerAction.MoveLeft]); // → 1
        game.Step([PlayerAction.Idle]);
        game.Step([PlayerAction.Idle]);
        game.Step([PlayerAction.Idle]); // Tick5: レーン1が予告される
        game.PendingBossAttackLane.ShouldBe(1);
        game.Step([PlayerAction.Idle]);
        game.Step([PlayerAction.Idle]); // Tick7: 着弾

        game.PlayerHps.ShouldBe([GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage]);
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
        game.Start(playerCount: 1); // レーン3。予告レーンへ自ら移動して3発受ける

        // attack1: レーン1(Tick5予告 → Tick7着弾)
        game.Step([PlayerAction.MoveLeft]); // → 2
        game.Step([PlayerAction.MoveLeft]); // → 1
        for (var tick = 3; tick <= 7; tick++)
        {
            game.Step([PlayerAction.Idle]);
        }
        game.PlayerHps[0].ShouldBe(GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage);

        // attack2: レーン2(Tick10予告 → Tick12着弾)
        game.Step([PlayerAction.MoveRight]); // → 2
        for (var tick = 9; tick <= 12; tick++)
        {
            game.Step([PlayerAction.Idle]);
        }
        game.PlayerHps[0].ShouldBe(GameLogic.PlayerMaxHp - GameLogic.BossAttackDamage * 2);

        // attack3: レーン3(Tick15予告 → Tick17着弾)で3発目 → HP0以下 → 全滅なので即Defeat
        game.Step([PlayerAction.MoveRight]); // → 3
        for (var tick = 14; tick <= 17; tick++)
        {
            game.Step([PlayerAction.Idle]);
        }

        game.PlayerHps[0].ShouldBeLessThanOrEqualTo(0);
        game.Phase.ShouldBe(GamePhase.Defeat);
    }

    [Fact]
    public void HPが0以下になると一定時間操作不能になり明けると回復する()
    {
        var game = new GameLogic(seed: 1);
        game.Start(playerCount: 2); // レーン [2, 3]。プレイヤー0だけ予告レーンへ移動して3発受ける

        // attack1: レーン1(Tick7着弾)。プレイヤー0だけ移動して受ける
        game.Step([PlayerAction.MoveLeft, PlayerAction.Idle]); // p0 → 1
        for (var tick = 2; tick <= 7; tick++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }
        // attack2: レーン2(Tick12着弾)
        game.Step([PlayerAction.MoveRight, PlayerAction.Idle]); // p0 → 2
        for (var tick = 9; tick <= 12; tick++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }
        // attack3: レーン3(Tick17着弾)。プレイヤー1はレーン3から退避する
        game.Step([PlayerAction.MoveRight, PlayerAction.MoveLeft]); // p0 → 3, p1 → 2
        for (var tick = 14; tick <= 16; tick++)
        {
            game.Step([PlayerAction.Idle, PlayerAction.Idle]);
        }
        game.Step([PlayerAction.Idle, PlayerAction.Idle]); // Tick17: レーン3へ着弾(p1は退避済み)

        game.PlayerHps[0].ShouldBeLessThanOrEqualTo(0);
        game.IncapacitatedTicks[0].ShouldBe(GameLogic.IncapacitationDuration);
        game.Phase.ShouldBe(GamePhase.Playing);

        game.Step([PlayerAction.Attack, PlayerAction.Idle]); // 操作不能中はAttackが無視される(弾も発射しない)
        game.Projectiles.ShouldBeEmpty();
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
            [PlayerAction.Attack, PlayerAction.MoveLeft],
            [PlayerAction.MoveRight, PlayerAction.Attack],
            [PlayerAction.Attack, PlayerAction.Attack],
            [PlayerAction.MoveLeft, PlayerAction.Idle],
            [PlayerAction.Attack, PlayerAction.MoveRight],
            [PlayerAction.Idle, PlayerAction.Idle],
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
        first.PlayerLanes.ShouldBe(second.PlayerLanes);
        first.Projectiles.ShouldBe(second.Projectiles);
        first.PendingBossAttackLane.ShouldBe(second.PendingBossAttackLane);
        first.Phase.ShouldBe(second.Phase);
    }
}
