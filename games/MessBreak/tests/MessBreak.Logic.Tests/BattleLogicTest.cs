using System.Numerics;
using Shouldly;

namespace MessBreak.Logic.Tests;

public class BattleLogicTest
{
    private const int Seed = 1;

    private static BattleLogic Create(BattleConfig? config = null) =>
        new(config ?? new BattleConfig(), Seed);

    /// <summary>条件が成立するまで入力なしで進める。固定 tick 待ちを書かないためのヘルパ。</summary>
    private static void TickUntil(BattleLogic logic, Func<bool> condition, int maxTicks = 600)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            if (condition())
            {
                return;
            }
            logic.Tick(TickInput.None);
        }
        throw new InvalidOperationException($"{maxTicks} tick 以内に条件が成立しませんでした");
    }

    // ---- プレイヤー移動 ----

    [Fact]
    public void Tick_右方向の移動入力_毎秒速度を1tick分だけ移動する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 0f)));

        var expected = before.X + logic.Config.PlayerSpeed / logic.Config.TicksPerSecond;
        logic.PlayerPos.X.ShouldBe(expected, 0.001f);
        logic.PlayerPos.Y.ShouldBe(before.Y, 0.001f);
    }

    [Fact]
    public void Tick_斜め方向の移動入力_移動量は直進と同じになる()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 1f)));

        var moved = (logic.PlayerPos - before).Length();
        moved.ShouldBe(logic.Config.PlayerSpeed / logic.Config.TicksPerSecond, 0.001f);
    }

    [Fact]
    public void Tick_左の壁に向かって移動し続ける_半径の位置で止まる()
    {
        var logic = Create();

        for (var i = 0; i < 600; i++)
        {
            logic.Tick(new TickInput(new Vector2(-1f, 0f)));
        }

        logic.PlayerPos.X.ShouldBe(logic.Config.PlayerRadius, 0.001f);
    }

    [Fact]
    public void Tick_移動入力あり_Facingが入力方向を向く()
    {
        var logic = Create();

        logic.Tick(new TickInput(new Vector2(0f, 1f)));

        logic.PlayerFacing.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_移動入力なし_Facingは直前の向きを維持する()
    {
        var logic = Create();
        logic.Tick(new TickInput(new Vector2(0f, 1f)));

        logic.Tick(TickInput.None);

        logic.PlayerFacing.ShouldBe(new Vector2(0f, 1f));
    }

    // ---- ドッジ ----

    [Fact]
    public void Tick_ドッジ入力_Dodge状態でFacing方向に高速移動する()
    {
        var logic = Create();
        logic.Tick(new TickInput(new Vector2(1f, 0f)));
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));

        logic.PlayerAction.ShouldBe(PlayerAction.Dodge);
        logic.PlayerPos.X.ShouldBe(
            before.X + logic.Config.DodgeSpeed / logic.Config.TicksPerSecond,
            0.001f
        );
    }

    [Fact]
    public void Tick_ドッジ後のクールダウン中_再ドッジできない()
    {
        var logic = Create(new BattleConfig { DodgeTicks = 5, DodgeCooldownTicks = 100 });
        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));
        TickUntil(logic, () => logic.PlayerAction == PlayerAction.Free);

        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));

        logic.PlayerAction.ShouldBe(PlayerAction.Free);
    }

    [Fact]
    public void Tick_クールダウン経過後_再ドッジできる()
    {
        var logic = Create(new BattleConfig { DodgeTicks = 5, DodgeCooldownTicks = 8 });
        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));
        TickUntil(logic, () => logic.PlayerAction == PlayerAction.Free && logic.DodgeCooldown == 0);

        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));

        logic.PlayerAction.ShouldBe(PlayerAction.Dodge);
    }

    // ---- 敵 FSM ----

    /// <summary>敵がすぐ攻撃してくる配置(隣接・攻撃範囲は部屋全体)。</summary>
    private static BattleConfig AggressiveAdjacentEnemy() =>
        new()
        {
            PlayerSpawn = new Vector2(100f, 90f),
            EnemySpawn = new Vector2(110f, 90f),
            EnemyAggroRange = 1000f,
            EnemyAttackRange = 1000f,
            EnemyWindupTicks = 3,
            EnemyRecoveryTicks = 5,
        };

    [Fact]
    public void Tick_プレイヤーがアグロ範囲外_Idleのまま動かない()
    {
        var logic = Create(); // 既定: 距離 160 > アグロ 100
        var before = logic.EnemyPos;

        logic.Tick(TickInput.None);

        logic.EnemyAction.ShouldBe(EnemyAction.Idle);
        logic.EnemyPos.ShouldBe(before);
    }

    [Fact]
    public void Tick_プレイヤーがアグロ範囲内_Chaseになり接近する()
    {
        var logic = Create(new BattleConfig { EnemyAggroRange = 1000f });
        var beforeDistance = (logic.EnemyPos - logic.PlayerPos).Length();

        logic.Tick(TickInput.None);
        logic.Tick(TickInput.None);

        logic.EnemyAction.ShouldBe(EnemyAction.Chase);
        (logic.EnemyPos - logic.PlayerPos).Length().ShouldBeLessThan(beforeDistance);
    }

    [Fact]
    public void Tick_攻撃レンジ内_Windupに遷移する()
    {
        var logic = Create(AggressiveAdjacentEnemy());

        TickUntil(logic, () => logic.EnemyAction == EnemyAction.Windup, 10);

        logic.EnemyAction.ShouldBe(EnemyAction.Windup);
    }

    [Fact]
    public void Tick_Windup完了時にプレイヤーが範囲内_ダメージを与えRecoveryへ遷移する()
    {
        var logic = Create(AggressiveAdjacentEnemy());

        TickUntil(logic, () => logic.EnemyAction == EnemyAction.Recovery, 20);

        logic.PlayerHp.ShouldBe(logic.Config.PlayerMaxHp - logic.Config.EnemyAttackDamage);
    }

    [Fact]
    public void Tick_Windup完了時にプレイヤーがドッジ中_ダメージを受けない()
    {
        var logic = Create(AggressiveAdjacentEnemy() with { DodgeTicks = 30 });
        TickUntil(logic, () => logic.EnemyAction == EnemyAction.Windup, 10);

        logic.Tick(new TickInput(Vector2.Zero, Dodge: true)); // Windup 中にドッジ開始(30 tick 無敵)
        TickUntil(logic, () => logic.EnemyAction == EnemyAction.Recovery, 20);

        logic.PlayerHp.ShouldBe(logic.Config.PlayerMaxHp);
    }

    [Fact]
    public void Tick_Recovery完了_Chaseに戻る()
    {
        var logic = Create(AggressiveAdjacentEnemy());
        TickUntil(logic, () => logic.EnemyAction == EnemyAction.Recovery, 20);

        TickUntil(logic, () => logic.EnemyAction != EnemyAction.Recovery, 20);

        logic.EnemyAction.ShouldBe(EnemyAction.Chase);
    }

    [Fact]
    public void Tick_プレイヤーHPが0になる_DefeatになりプレイヤーはDeadになる()
    {
        var logic = Create(AggressiveAdjacentEnemy() with { PlayerMaxHp = 1 });

        TickUntil(logic, () => logic.PlayerHp == 0, 60);

        logic.Phase.ShouldBe(BattlePhase.Defeat);
        logic.PlayerAction.ShouldBe(PlayerAction.Dead);
    }
}
