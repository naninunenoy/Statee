using System.Numerics;
using Shouldly;

namespace ShootingGame.Logic.Tests;

public class ShootingLogicTest
{
    // ウェーブ自動湧きと干渉しないよう、既定はウェーブなしで生成する。
    // ウェーブ仕様のテストは ShootingLogicWaveTest に置く
    private static ShootingLogic CreateLogic(ShootingConfig? config = null) =>
        new(seed: 1, config ?? new ShootingConfig { Waves = [] });

    [Fact]
    public void Tick_1回進める_TickCountが1になる()
    {
        var logic = CreateLogic();

        logic.Tick(new InputState());

        logic.TickCount.ShouldBe(1);
    }

    [Fact]
    public void Tick_右入力_自機がPlayerSpeedぶん右へ動く()
    {
        var logic = CreateLogic();
        var before = logic.PlayerPosition;

        logic.Tick(new InputState(Right: true));

        logic.PlayerPosition.ShouldBe(before with { X = before.X + logic.Config.PlayerSpeed });
    }

    [Fact]
    public void Tick_左入力_自機がPlayerSpeedぶん左へ動く()
    {
        var logic = CreateLogic();
        var before = logic.PlayerPosition;

        logic.Tick(new InputState(Left: true));

        logic.PlayerPosition.ShouldBe(before with { X = before.X - logic.Config.PlayerSpeed });
    }

    [Fact]
    public void Tick_上入力_自機がPlayerSpeedぶん上へ動く()
    {
        var logic = CreateLogic();
        var before = logic.PlayerPosition;

        logic.Tick(new InputState(Up: true));

        logic.PlayerPosition.ShouldBe(before with { Y = before.Y - logic.Config.PlayerSpeed });
    }

    [Fact]
    public void Tick_下入力_自機がPlayerSpeedぶん下へ動く()
    {
        var logic = CreateLogic();
        var before = logic.PlayerPosition;

        logic.Tick(new InputState(Down: true));

        logic.PlayerPosition.ShouldBe(before with { Y = before.Y + logic.Config.PlayerSpeed });
    }

    [Fact]
    public void Tick_左端まで左入力を続ける_自機半径でクランプされる()
    {
        var logic = CreateLogic();

        logic.TickMany(200, new InputState(Left: true));

        logic.PlayerPosition.X.ShouldBe(logic.Config.PlayerRadius);
    }

    [Fact]
    public void Tick_下端まで下入力を続ける_自機半径でクランプされる()
    {
        var logic = CreateLogic();

        logic.TickMany(200, new InputState(Down: true));

        logic.PlayerPosition.Y.ShouldBe(logic.Config.FieldHeight - logic.Config.PlayerRadius);
    }

    [Fact]
    public void Tick_ショット入力_自弾が自機位置に発射される()
    {
        var logic = CreateLogic();

        logic.Tick(new InputState(Shoot: true));

        logic.PlayerBullets.Count.ShouldBe(1);
        logic.PlayerBullets[0].Position.ShouldBe(logic.PlayerPosition);
    }

    [Fact]
    public void Tick_ショット押しっぱなし_FireIntervalTicksごとに1発になる()
    {
        var logic = CreateLogic();

        logic.TickMany(logic.Config.FireIntervalTicks + 1, new InputState(Shoot: true));

        logic.PlayerBullets.Count.ShouldBe(2);
    }

    [Fact]
    public void Tick_自弾が進む_毎TickBulletSpeedぶん右へ動く()
    {
        var logic = CreateLogic();
        logic.Tick(new InputState(Shoot: true));
        var before = logic.PlayerBullets[0].Position;

        logic.Tick(new InputState());

        logic
            .PlayerBullets[0]
            .Position.ShouldBe(before with { X = before.X + logic.Config.PlayerBulletSpeed });
    }

    [Fact]
    public void Tick_自弾が右端を越える_消える()
    {
        var logic = CreateLogic();
        logic.Tick(new InputState(Shoot: true));

        logic.TickUntil(l => l.PlayerBullets.Count == 0);

        logic.PlayerBullets.ShouldBeEmpty();
    }

    [Fact]
    public void SpawnEnemy_直進敵を出す_Enemiesに現れる()
    {
        var logic = CreateLogic();

        var id = logic.SpawnEnemy(EnemyKind.Straight, new Vector2(900, 270));

        logic.Enemies.Count.ShouldBe(1);
        logic
            .Enemies[0]
            .ShouldBe(new EnemySnapshot(id, EnemyKind.Straight, new Vector2(900, 270), 1));
    }

    [Fact]
    public void SpawnEnemy_直進敵を出す_EnemySpawnedが記録される()
    {
        var logic = CreateLogic();

        logic.SpawnEnemy(EnemyKind.Straight, new Vector2(900, 270));

        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(EnemySpawned));
    }

    [Fact]
    public void Tick_直進敵_毎TickStraightEnemySpeedぶん左へ動く()
    {
        var logic = CreateLogic();
        logic.SpawnEnemy(EnemyKind.Straight, new Vector2(900, 400));

        logic.Tick(new InputState());

        logic.Enemies[0].Position.ShouldBe(new Vector2(900 - logic.Config.StraightEnemySpeed, 400));
    }

    [Fact]
    public void Tick_敵が左端を越える_消える()
    {
        var logic = CreateLogic();
        logic.SpawnEnemy(EnemyKind.Straight, new Vector2(10, 400));

        logic.TickUntil(l => l.Enemies.Count == 0);

        logic.Enemies.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_自弾が敵に当たる_敵と弾が消えスコアが増える()
    {
        var logic = CreateLogic();
        // 自機と同じ Y の遠方に敵を置き、弾が敵より先に届く配置にする
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition with { X = 800 });
        logic.Tick(new InputState(Shoot: true));

        logic.TickUntil(l => l.Enemies.Count == 0);

        logic.PlayerBullets.ShouldBeEmpty();
        logic.Score.ShouldBe(logic.Config.EnemyScore);
        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(EnemyDestroyed));
    }

    [Fact]
    public void Tick_敵が自機に触れる_残機が減り無敵になる()
    {
        var logic = CreateLogic();
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);

        logic.Tick(new InputState());

        logic.Lives.ShouldBe(logic.Config.InitialLives - 1);
        logic.IsInvincible.ShouldBeTrue();
        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(PlayerHit));
    }

    [Fact]
    public void Tick_無敵中に敵が触れ続ける_残機は1回ぶんしか減らない()
    {
        var logic = CreateLogic();
        logic.SpawnEnemy(
            EnemyKind.Straight,
            logic.PlayerPosition with
            {
                X = logic.PlayerPosition.X + 100,
            }
        );
        logic.Tick(new InputState());
        var livesAfterFirstHit = logic.Config.InitialLives - 1;

        // 無敵時間内(敵はまだ自機に重なり続けている)
        logic.TickMany(logic.Config.InvincibleTicks - 1);

        logic.Lives.ShouldBe(livesAfterFirstHit);
    }

    [Fact]
    public void Tick_無敵が切れた後に敵が触れる_再び被弾する()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            InvincibleTicks = 5,
            StraightEnemySpeed = 0f,
        };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);
        logic.Tick(new InputState());

        logic.TickMany(config.InvincibleTicks + 1);

        logic.Lives.ShouldBe(config.InitialLives - 2);
    }

    [Fact]
    public void Tick_残機1で被弾_ゲームオーバーになる()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            InitialLives = 1,
            StraightEnemySpeed = 0f,
        };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);

        logic.Tick(new InputState());

        logic.IsGameOver.ShouldBeTrue();
        logic.EventLog.Entries.ShouldContain(e => e.Name == nameof(GameEnded));
    }

    [Fact]
    public void Tick_ゲームオーバー後_状態が変わらない()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            InitialLives = 1,
            StraightEnemySpeed = 0f,
        };
        var logic = CreateLogic(config);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);
        logic.Tick(new InputState());
        var tickCount = logic.TickCount;
        var position = logic.PlayerPosition;

        logic.Tick(new InputState(Right: true, Shoot: true));

        logic.TickCount.ShouldBe(tickCount);
        logic.PlayerPosition.ShouldBe(position);
        logic.PlayerBullets.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_同一シードと同一入力列_同一状態になる()
    {
        var inputs = new InputState[]
        {
            new(Right: true, Shoot: true),
            new(Up: true, Shoot: true),
            new(Shoot: true),
            new(Left: true),
            new(Down: true, Shoot: true),
        };
        var a = CreateLogic();
        var b = CreateLogic();
        a.SpawnEnemy(EnemyKind.Straight, new Vector2(400, 270));
        b.SpawnEnemy(EnemyKind.Straight, new Vector2(400, 270));

        foreach (var input in inputs)
        {
            a.Tick(input);
            b.Tick(input);
        }

        a.PlayerPosition.ShouldBe(b.PlayerPosition);
        a.PlayerBullets.ShouldBe(b.PlayerBullets);
        a.Enemies.ShouldBe(b.Enemies);
        a.Score.ShouldBe(b.Score);
    }

    [Fact]
    public void EventLog_容量を超えて記録する_古い順に捨てられ通し番号は続く()
    {
        var config = new ShootingConfig { Waves = [], EventLogCapacity = 3 };
        var logic = CreateLogic(config);

        for (var i = 0; i < 5; i++)
        {
            logic.SpawnEnemy(EnemyKind.Straight, new Vector2(900, 100 + i * 50));
        }

        logic.EventLog.TotalCount.ShouldBe(5);
        logic.EventLog.Entries.Count.ShouldBe(3);
        logic.EventLog.Entries[^1].Sequence.ShouldBe(5);
    }
}
