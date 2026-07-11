using System.Linq;
using Shouldly;

namespace ShootingGame.Logic.Tests;

/// <summary>ボス 🐙(全ウェーブクリア後の入場・フェーズ弾幕・撃破でクリア)の仕様。</summary>
public class ShootingLogicBossTest
{
    /// <summary>ウェーブ1体(即退場)+検証しやすいボス(蛇行なし・素早く入場)。</summary>
    private static ShootingLogic CreateBossLogic(int bossHp)
    {
        var config = new ShootingConfig
        {
            Waves = [new(1, [EnemyKind.Straight])],
            StraightEnemySpeed = 50f,
            PowerUpDropChance = 0.0,
            Boss = new BossConfig
            {
                Hp = bossHp,
                EntrySpeed = 20f,
                FireIntervalTicks = 10,
                SineAmplitude = 0f,
            },
        };
        return new ShootingLogic(seed: 1, config);
    }

    private static bool BossExists(ShootingLogic logic) =>
        logic.Enemies.Any(e => e.Kind == EnemyKind.Boss);

    private static EnemySnapshot Boss(ShootingLogic logic) =>
        logic.Enemies.Single(e => e.Kind == EnemyKind.Boss);

    [Fact]
    public void Tick_最終ウェーブクリア_ボスが設定のHPで湧く()
    {
        var logic = CreateBossLogic(bossHp: 50);

        logic.TickUntil(BossExists);

        Boss(logic).Hp.ShouldBe(50);
    }

    [Fact]
    public void Tick_ボス_アンカーXまで入場して停止する()
    {
        var logic = CreateBossLogic(bossHp: 50);
        logic.TickUntil(l => BossExists(l) && Boss(l).Position.X <= l.Config.Boss!.AnchorX);
        var anchored = Boss(logic).Position;

        logic.TickMany(5);

        Boss(logic).Position.X.ShouldBe(anchored.X);
    }

    [Fact]
    public void Tick_ボスフェーズ1_発射間隔ごとに弾を1発撃つ()
    {
        var logic = CreateBossLogic(bossHp: 50);
        logic.TickUntil(BossExists);

        logic.TickUntil(l => l.EnemyBullets.Count > 0);

        logic.EnemyBullets.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_ボスHPが3分の2以下_フェーズ2になり3ウェイ弾になる()
    {
        var logic = CreateBossLogic(bossHp: 3);
        logic.TickUntil(BossExists);
        // 1発だけ当ててフェーズ2へ(自機とボスは同じ中央 Y にいる)
        logic.Tick(new InputState(Shoot: true));
        logic.TickUntil(l => Boss(l).Hp == 2);
        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(BossPhaseChanged));
        var maxBulletId = logic.EnemyBullets.Count == 0 ? 0 : logic.EnemyBullets.Max(b => b.Id);

        // 次の弾幕1回ぶんを待つ
        logic.TickUntil(l => l.EnemyBullets.Any(b => b.Id > maxBulletId));

        logic.EnemyBullets.Count(b => b.Id > maxBulletId).ShouldBe(3);
    }

    [Fact]
    public void Tick_ボス撃破_GameClearedが記録されIsClearedで盤面が凍結する()
    {
        var logic = CreateBossLogic(bossHp: 1);
        logic.TickUntil(BossExists);
        logic.Tick(new InputState(Shoot: true));

        logic.TickUntil(l => l.IsCleared);
        var tickCount = logic.TickCount;
        logic.Tick(new InputState(Right: true));

        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(GameCleared));
        logic.TickCount.ShouldBe(tickCount);
    }

    [Fact]
    public void Tick_ボス撃破_ボススコアが加算される()
    {
        var logic = CreateBossLogic(bossHp: 1);
        logic.TickUntil(BossExists);
        var scoreBeforeKill = logic.Score;
        logic.Tick(new InputState(Shoot: true));

        logic.TickUntil(l => l.IsCleared);

        logic.Score.ShouldBe(scoreBeforeKill + logic.Config.Boss!.Score);
    }
}
