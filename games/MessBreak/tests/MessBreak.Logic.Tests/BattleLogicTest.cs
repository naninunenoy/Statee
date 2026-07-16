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
    public void Tick_スプリント入力つきの移動_スプリント速度で移動する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 0f), Sprint: true));

        var expected = before.X + logic.Config.SprintSpeed / logic.Config.TicksPerSecond;
        logic.PlayerPos.X.ShouldBe(expected, 0.001f);
    }

    // ---- エイム(移動と独立) ----

    [Fact]
    public void Tick_エイム入力あり_Facingがエイム方向を向く()
    {
        var logic = Create();

        logic.Tick(new TickInput(AimDir: new Vector2(0f, 1f)));

        logic.PlayerFacing.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_移動入力のみ_Facingは変わらない()
    {
        var logic = Create(); // 初期 Facing は (1,0)

        logic.Tick(new TickInput(new Vector2(0f, 1f)));

        logic.PlayerFacing.ShouldBe(new Vector2(1f, 0f));
    }

    [Fact]
    public void Tick_エイム入力なし_Facingは直前の向きを維持する()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(0f, 1f)));

        logic.Tick(TickInput.None);

        logic.PlayerFacing.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_移動しながらエイム_移動とエイムが両立する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 0f), AimDir: new Vector2(0f, -1f)));

        logic.PlayerPos.X.ShouldBeGreaterThan(before.X);
        logic.PlayerFacing.ShouldBe(new Vector2(0f, -1f));
    }

    // ---- 射撃 ----

    [Fact]
    public void Tick_発射入力_エイム方向の弾が生成される()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(0f, 1f)));

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.Count.ShouldBe(1);
        logic.Bullets[0].Dir.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_弾_毎tickエイム方向へ速度分だけ進む()
    {
        var logic = Create();
        logic.Tick(new TickInput(Fire: true)); // 初期 Facing (1,0) へ発射
        var before = logic.Bullets[0].Pos;

        logic.Tick(TickInput.None);

        var expected = before.X + logic.Config.BulletSpeed / logic.Config.TicksPerSecond;
        logic.Bullets[0].Pos.X.ShouldBe(expected, 0.001f);
    }

    [Fact]
    public void Tick_発射を押し続ける_クールダウンごとに1発だけ出る()
    {
        var logic = Create(new BattleConfig { FireCooldownTicks = 10 });

        for (var i = 0; i < 10; i++)
        {
            logic.Tick(new TickInput(Fire: true));
        }

        logic.Bullets.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_クールダウン経過後の発射入力_次の弾が出る()
    {
        var logic = Create(new BattleConfig { FireCooldownTicks = 10 });
        logic.Tick(new TickInput(Fire: true));
        TickUntil(logic, () => logic.FireCooldown == 0);

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.Count.ShouldBe(2);
    }

    [Fact]
    public void Tick_複数の弾_IDが一意で安定している()
    {
        var logic = Create(new BattleConfig { FireCooldownTicks = 1 });
        logic.Tick(new TickInput(Fire: true));
        var firstId = logic.Bullets[0].Id;
        TickUntil(logic, () => logic.FireCooldown == 0);

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.Count.ShouldBe(2);
        logic.Bullets[0].Id.ShouldBe(firstId);
        logic.Bullets[1].Id.ShouldNotBe(firstId);
    }

    [Fact]
    public void Tick_移動しながら発射_移動と発射が両立する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(0f, 1f), Fire: true));

        logic.PlayerPos.Y.ShouldBeGreaterThan(before.Y);
        logic.Bullets.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_弾が敵に当たる_ダメージを与えて弾は消える()
    {
        var logic = Create(new BattleConfig { EnemyAggroRange = 0f }); // 敵は動かず 160 先
        logic.Tick(new TickInput(Fire: true)); // 初期 Facing (1,0) = 敵の方向

        TickUntil(logic, () => logic.EnemyHp < logic.Config.EnemyMaxHp, 120);

        logic.EnemyHp.ShouldBe(logic.Config.EnemyMaxHp - logic.Config.BulletDamage);
        logic.Bullets.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_弾が部屋の外に出る_消える()
    {
        var logic = Create(new BattleConfig { EnemyAggroRange = 0f });
        logic.Tick(new TickInput(AimDir: new Vector2(-1f, 0f))); // 敵のいない左の壁へ
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.Bullets.Count == 0, 120);

        logic.EnemyHp.ShouldBe(logic.Config.EnemyMaxHp);
    }

    [Fact]
    public void Tick_敵HPが0になる_Victoryになり敵はDeadになる()
    {
        var logic = Create(new BattleConfig { EnemyAggroRange = 0f, EnemyMaxHp = 1 });

        logic.Tick(new TickInput(Fire: true));
        TickUntil(logic, () => logic.EnemyHp == 0, 120);

        logic.Phase.ShouldBe(BattlePhase.Victory);
        logic.EnemyAction.ShouldBe(EnemyAction.Dead);
    }

    [Fact]
    public void Tick_Victory後_状態が進まない()
    {
        var logic = Create(new BattleConfig { EnemyAggroRange = 0f, EnemyMaxHp = 1 });
        logic.Tick(new TickInput(Fire: true));
        TickUntil(logic, () => logic.Phase == BattlePhase.Victory, 120);
        var tickCount = logic.TickCount;

        logic.Tick(new TickInput(new Vector2(1f, 0f)));

        logic.TickCount.ShouldBe(tickCount);
    }

    // ---- ドッジ ----

    [Fact]
    public void Tick_移動入力なしのドッジ_Dodge状態でFacing方向に高速移動する()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(1f, 0f)));
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));

        logic.PlayerAction.ShouldBe(PlayerAction.Dodge);
        logic.PlayerPos.X.ShouldBe(
            before.X + logic.Config.DodgeSpeed / logic.Config.TicksPerSecond,
            0.001f
        );
    }

    [Fact]
    public void Tick_移動入力つきドッジ_移動方向に移動しエイムは維持される()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(1f, 0f)));
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));

        logic.PlayerPos.Y.ShouldBe(
            before.Y + logic.Config.DodgeSpeed / logic.Config.TicksPerSecond,
            0.001f
        );
        logic.PlayerFacing.ShouldBe(new Vector2(1f, 0f));
    }

    [Fact]
    public void Tick_ドッジ中の発射入力_弾が出ない()
    {
        var logic = Create(new BattleConfig { DodgeTicks = 10 });
        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.ShouldBeEmpty();
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
