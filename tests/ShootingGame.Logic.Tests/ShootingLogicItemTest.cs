using System.Numerics;
using Shouldly;

namespace ShootingGame.Logic.Tests;

/// <summary>アイテム ⭐(ショット強化)の仕様。ウェーブ自動湧きなしで検証する。</summary>
public class ShootingLogicItemTest
{
    private static ShootingLogic CreateLogic(ShootingConfig? config = null) =>
        new(seed: 1, config ?? new ShootingConfig { Waves = [] });

    /// <summary>アイテムを自機位置に出して1 Tick で取得させる。</summary>
    private static void Collect(ShootingLogic logic)
    {
        logic.SpawnItem(logic.PlayerPosition);
        logic.Tick(new InputState());
    }

    [Fact]
    public void 生成直後_PowerLevelは1()
    {
        var logic = CreateLogic();

        logic.PowerLevel.ShouldBe(1);
    }

    [Fact]
    public void Tick_ドロップ率1で敵撃破_アイテムが敵の位置あたりに出る()
    {
        var config = new ShootingConfig { Waves = [], PowerUpDropChance = 1.0 };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition with { X = 800 });
        logic.Tick(new InputState(Shoot: true));

        logic.TickUntil(l => l.Enemies.Count == 0);

        logic.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_ドロップ率0で敵撃破_アイテムは出ない()
    {
        var config = new ShootingConfig { Waves = [], PowerUpDropChance = 0.0 };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition with { X = 800 });
        logic.Tick(new InputState(Shoot: true));

        logic.TickUntil(l => l.Enemies.Count == 0);

        logic.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_アイテム_毎TickItemDriftSpeedぶん左へ漂う()
    {
        var logic = CreateLogic();
        logic.SpawnItem(new Vector2(500, 300));

        logic.Tick(new InputState());

        logic.Items[0].Position.ShouldBe(new Vector2(500 - logic.Config.ItemDriftSpeed, 300));
    }

    [Fact]
    public void Tick_アイテムに触れる_PowerLevelが上がり取得イベントが記録される()
    {
        var logic = CreateLogic();

        Collect(logic);

        logic.PowerLevel.ShouldBe(2);
        logic.Items.ShouldBeEmpty();
        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(PowerUpCollected));
    }

    [Fact]
    public void Tick_最大段階でアイテムに触れる_PowerLevelは上限のまま()
    {
        var logic = CreateLogic();

        for (var i = 0; i < logic.Config.MaxPowerLevel + 1; i++)
        {
            Collect(logic);
        }

        logic.PowerLevel.ShouldBe(logic.Config.MaxPowerLevel);
    }

    [Fact]
    public void Tick_PowerLevel2でショット_1回の発射で2発出る()
    {
        var logic = CreateLogic();
        Collect(logic);

        logic.Tick(new InputState(Shoot: true));

        logic.PlayerBullets.Count.ShouldBe(2);
    }

    [Fact]
    public void Tick_PowerLevel2で被弾_1段階下がる()
    {
        var logic = CreateLogic();
        Collect(logic);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);

        logic.Tick(new InputState());

        logic.PowerLevel.ShouldBe(1);
    }

    [Fact]
    public void Tick_PowerLevel1で被弾_1のまま()
    {
        var logic = CreateLogic();
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);

        logic.Tick(new InputState());

        logic.PowerLevel.ShouldBe(1);
    }

    [Fact]
    public void Tick_アイテムが左端を越える_消える()
    {
        var logic = CreateLogic();
        // 自機に触れない Y に出す
        logic.SpawnItem(new Vector2(30, 500));

        logic.TickUntil(l => l.Items.Count == 0);

        logic.Items.ShouldBeEmpty();
    }
}
