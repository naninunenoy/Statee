using System.Numerics;
using Shouldly;

namespace ShootingGame.Logic.Tests;

/// <summary>敵種(Sine 🛸 / Shooter 🦑)と敵弾の仕様。ウェーブ自動湧きなしで検証する。</summary>
public class ShootingLogicEnemyKindTest
{
    private static ShootingLogic CreateLogic(ShootingConfig? config = null) =>
        new(seed: 1, config ?? new ShootingConfig { Waves = [] });

    [Fact]
    public void Tick_サイン波敵_4分の1周期でXは等速左進Yは振幅ぶん下がる()
    {
        var logic = CreateLogic();
        logic.SpawnEnemy(EnemyKind.Sine, new Vector2(600, 300));
        var quarter = logic.Config.SinePeriodTicks / 4;

        logic.TickMany(quarter);

        logic.Enemies[0].Position.X.ShouldBe(600 - logic.Config.SineEnemySpeed * quarter, 0.001f);
        logic.Enemies[0].Position.Y.ShouldBe(300 + logic.Config.SineAmplitude, 0.001f);
    }

    [Fact]
    public void Tick_サイン波敵_1周期で基準のYへ戻る()
    {
        var logic = CreateLogic();
        logic.SpawnEnemy(EnemyKind.Sine, new Vector2(600, 300));

        logic.TickMany(logic.Config.SinePeriodTicks);

        logic.Enemies[0].Position.Y.ShouldBe(300, 0.01f);
    }

    [Fact]
    public void Tick_シューター敵_発射間隔の経過で敵弾を1発撃つ()
    {
        var config = new ShootingConfig { Waves = [], ShooterFireIntervalTicks = 10 };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Shooter, new Vector2(800, 400));

        logic.TickMany(config.ShooterFireIntervalTicks);

        logic.EnemyBullets.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_敵弾_発射時の自機方向へ等速で進みホーミングしない()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            ShooterFireIntervalTicks = 10,
            ShooterEnemySpeed = 0f,
        };
        var logic = CreateLogic(config);
        // 自機と同じ Y に置く → 発射方向は真左
        logic.SpawnEnemy(EnemyKind.Shooter, logic.PlayerPosition with { X = 620 });
        logic.TickUntil(l => l.EnemyBullets.Count == 1);
        var before = logic.EnemyBullets[0].Position;

        // 自機を動かしても既発射の弾の進路は変わらない
        logic.Tick(new InputState(Down: true));

        logic
            .EnemyBullets[0]
            .Position.ShouldBe(before with { X = before.X - config.EnemyBulletSpeed });
    }

    [Fact]
    public void Tick_敵弾が自機に当たる_残機が減り当たった弾は消える()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            ShooterFireIntervalTicks = 10,
            ShooterEnemySpeed = 0f,
        };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Shooter, logic.PlayerPosition with { X = 400 });

        logic.TickUntil(l => l.Lives < config.InitialLives);

        var hitRange = config.PlayerRadius + config.EnemyBulletRadius;
        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(PlayerHit));
        logic.EnemyBullets.ShouldAllBe(b =>
            Vector2.Distance(b.Position, logic.PlayerPosition) > hitRange
        );
    }

    [Fact]
    public void Tick_無敵中の敵弾_すり抜けて残機も弾も変わらない()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            ShooterFireIntervalTicks = 10,
            ShooterEnemySpeed = 0f,
            StraightEnemySpeed = 0f,
            InvincibleTicks = 100_000,
        };
        var logic = CreateLogic(config);
        // 接触被弾で無敵化してから、敵弾を自機へ通す
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);
        logic.SpawnEnemy(EnemyKind.Shooter, logic.PlayerPosition with { X = 300 });
        logic.TickUntil(l => l.EnemyBullets.Count >= 1);
        var firstBulletId = logic.EnemyBullets[0].Id;

        // 先頭の弾が自機を通過して画面外で消えるまで進める
        logic.TickUntil(l => l.EnemyBullets.All(b => b.Id != firstBulletId));

        logic.Lives.ShouldBe(config.InitialLives - 1);
    }

    [Fact]
    public void Tick_敵弾が画面外_消える()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            ShooterFireIntervalTicks = 10,
            ShooterEnemySpeed = 0f,
            StraightEnemySpeed = 0f,
            InvincibleTicks = 100_000,
        };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition); // 無敵化(すり抜け用)
        logic.SpawnEnemy(EnemyKind.Shooter, logic.PlayerPosition with { X = 300 });
        logic.TickUntil(l => l.EnemyBullets.Count >= 1);
        var firstBulletId = logic.EnemyBullets[0].Id;

        logic.TickUntil(l => l.EnemyBullets.All(b => b.Id != firstBulletId));

        logic.EnemyBullets.ShouldAllBe(b => b.Position.X >= -config.EnemyBulletRadius);
    }
}
