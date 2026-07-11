using Shouldly;

namespace ShootingGame.Logic.Tests;

/// <summary>ウェーブ進行(シード由来の出現スケジュール)の仕様。</summary>
public class ShootingLogicWaveTest
{
    private static void TickMany(ShootingLogic logic, int count)
    {
        for (var i = 0; i < count; i++)
        {
            logic.Tick(new InputState());
        }
    }

    /// <summary>条件成立まで Tick を進める(上限つき。固定 Tick 数への依存を避ける)。</summary>
    private static void TickUntil(
        ShootingLogic logic,
        Func<ShootingLogic, bool> condition,
        int maxTicks = 2000
    )
    {
        for (var i = 0; i < maxTicks; i++)
        {
            if (condition(logic))
            {
                return;
            }
            logic.Tick(new InputState());
        }
        condition(logic).ShouldBeTrue($"{maxTicks} Tick 以内に条件が成立しなかった");
    }

    [Fact]
    public void Tick_ウェーブなし設定_Waveは0のまま敵も湧かない()
    {
        var logic = new ShootingLogic(seed: 1, new ShootingConfig { Waves = [] });

        TickMany(logic, 200);

        logic.Wave.ShouldBe(0);
        logic.Enemies.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_最初のTick_Wave1が始まりWaveStartedが記録される()
    {
        var logic = new ShootingLogic(seed: 1);

        logic.Tick(new InputState());

        logic.Wave.ShouldBe(1);
        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(WaveStarted));
    }

    [Fact]
    public void Tick_最初のTick_先頭の敵が右端の出現位置に湧く()
    {
        var logic = new ShootingLogic(seed: 1);
        var config = logic.Config;

        logic.Tick(new InputState());

        logic.Enemies.Count.ShouldBe(1);
        logic.Enemies[0].Position.X.ShouldBe(config.FieldWidth + config.EnemyRadius);
        logic
            .Enemies[0]
            .Position.Y.ShouldBeInRange(
                config.SpawnMarginY,
                config.FieldHeight - config.SpawnMarginY
            );
    }

    [Fact]
    public void Tick_ウェーブの敵が全て退場_WaveClearedが記録され次のウェーブへ進む()
    {
        // 速い直進敵1体だけのウェーブ×2。撃たずに退場させてクリア扱いになることを見る
        var config = new ShootingConfig
        {
            Waves = [new(1, [EnemyKind.Straight]), new(1, [EnemyKind.Straight])],
            StraightEnemySpeed = 50f,
        };
        var logic = new ShootingLogic(seed: 1, config);

        TickUntil(logic, l => l.Wave == 2);

        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(WaveCleared));
    }

    [Fact]
    public void Tick_最終ウェーブの敵が全て退場_AllWavesClearedになり以後湧かない()
    {
        var config = new ShootingConfig
        {
            Waves = [new(1, [EnemyKind.Straight])],
            StraightEnemySpeed = 50f,
        };
        var logic = new ShootingLogic(seed: 1, config);

        TickUntil(logic, l => l.AllWavesCleared);
        TickMany(logic, 200);

        logic.Enemies.ShouldBeEmpty();
        logic.Wave.ShouldBe(1);
    }

    [Fact]
    public void Tick_単一種のウェーブ_そのウェーブからはその種だけ湧く()
    {
        var config = new ShootingConfig { Waves = [new(3, [EnemyKind.Sine])] };
        var logic = new ShootingLogic(seed: 1, config);

        TickUntil(logic, l => l.Enemies.Count >= 2);

        logic.Enemies.ShouldAllBe(e => e.Kind == EnemyKind.Sine);
    }

    [Fact]
    public void Tick_同一シード_同一のタイミングと位置で敵が湧く()
    {
        var a = new ShootingLogic(seed: 42);
        var b = new ShootingLogic(seed: 42);

        TickMany(a, 300);
        TickMany(b, 300);

        a.Enemies.ShouldBe(b.Enemies);
        a.EventLog.TotalCount.ShouldBe(b.EventLog.TotalCount);
    }

    [Fact]
    public void Tick_異なるシード_出現スケジュールが変わる()
    {
        var a = new ShootingLogic(seed: 1);
        var b = new ShootingLogic(seed: 2);

        TickMany(a, 300);
        TickMany(b, 300);

        a.Enemies.ShouldNotBe(b.Enemies);
    }
}
