using Shouldly;

namespace ShootingGame.Logic.Tests;

/// <summary>入力ログとフレーム精度リプレイ(D-048 の検証の柱3)の仕様。</summary>
public class ShootingLogicReplayTest
{
    /// <summary>ウェーブ・ドロップ・ボスまで含む既定ルールで、乱数消費経路ごと再現されることを見る。</summary>
    private static readonly InputState[] Session =
    [
        .. Enumerable.Repeat(new InputState(Shoot: true), 120),
        .. Enumerable.Repeat(new InputState(Up: true, Shoot: true), 60),
        .. Enumerable.Repeat(new InputState(Down: true), 80),
        .. Enumerable.Repeat(new InputState(Right: true, Shoot: true), 140),
    ];

    [Fact]
    public void Tick_入力を与える_InputLogにTickごと記録される()
    {
        var logic = new ShootingLogic(seed: 1, new ShootingConfig { Waves = [] });
        var inputs = new InputState[] { new(Right: true), new(Shoot: true), new() };

        foreach (var input in inputs)
        {
            logic.Tick(input);
        }

        logic.InputLog.ShouldBe(inputs);
    }

    [Fact]
    public void Tick_ゲームオーバー後の入力_記録されない()
    {
        var config = new ShootingConfig
        {
            Waves = [],
            InitialLives = 1,
            StraightEnemySpeed = 0f,
        };
        var logic = new ShootingLogic(seed: 1, config);
        logic.SpawnEnemy(EnemyKind.Straight, logic.PlayerPosition);
        logic.Tick(new InputState());
        var logged = logic.InputLog.Count;

        logic.Tick(new InputState(Right: true));

        logic.InputLog.Count.ShouldBe(logged);
    }

    [Fact]
    public void InputRuns_同じ入力の連続_ランレングスに畳まれる()
    {
        var logic = new ShootingLogic(seed: 1, new ShootingConfig { Waves = [] });
        var right = new InputState(Right: true);
        var shoot = new InputState(Shoot: true);

        logic.TickMany(3, right);
        logic.TickMany(2, shoot);

        logic.InputRuns.ShouldBe([new InputRun(3, right), new InputRun(2, shoot)]);
    }

    [Fact]
    public void Replay_同一シードに入力ログを再生_同一状態になる()
    {
        var recorded = new ShootingLogic(seed: 777);
        foreach (var input in Session)
        {
            recorded.Tick(input);
        }

        var replayed = ShootingLogic.Replay(777, recorded.InputLog);

        replayed.TickCount.ShouldBe(recorded.TickCount);
        replayed.PlayerPosition.ShouldBe(recorded.PlayerPosition);
        replayed.Score.ShouldBe(recorded.Score);
        replayed.Lives.ShouldBe(recorded.Lives);
        replayed.PowerLevel.ShouldBe(recorded.PowerLevel);
        replayed.Wave.ShouldBe(recorded.Wave);
        replayed.PlayerBullets.ShouldBe(recorded.PlayerBullets);
        replayed.EnemyBullets.ShouldBe(recorded.EnemyBullets);
        replayed.Enemies.ShouldBe(recorded.Enemies);
        replayed.Items.ShouldBe(recorded.Items);
        replayed.EventLog.TotalCount.ShouldBe(recorded.EventLog.TotalCount);
    }

    [Fact]
    public void Replay_InputRunsから展開して再生_同一状態になる()
    {
        var recorded = new ShootingLogic(seed: 777);
        foreach (var input in Session)
        {
            recorded.Tick(input);
        }

        var expanded = recorded.InputRuns.SelectMany(run =>
            Enumerable.Repeat(run.Input, run.Ticks)
        );
        var replayed = ShootingLogic.Replay(777, expanded);

        replayed.PlayerPosition.ShouldBe(recorded.PlayerPosition);
        replayed.EventLog.TotalCount.ShouldBe(recorded.EventLog.TotalCount);
    }
}
