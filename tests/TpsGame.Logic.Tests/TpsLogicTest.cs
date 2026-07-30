using System.Numerics;
using Shouldly;
using TpsGame.Logic;

namespace TpsGame.Logic.Tests;

public class TpsLogicTest
{
    [Fact]
    public void Tick_Forward入力_プレイヤーがプラスZへ進む()
    {
        var game = new TpsLogic(seed: 1);

        game.Tick(new InputState(Forward: true));

        game.PlayerPosition.Z.ShouldBeGreaterThan(0f);
        game.TickCount.ShouldBe(1);
    }

    [Fact]
    public void Tick_Shoot入力_弾が1発生成される()
    {
        var game = new TpsLogic(seed: 1);

        game.Tick(new InputState(Shoot: true));

        game.Bullets.Count.ShouldBe(1);
        game.Events.ShouldContain(e => e.Name == "ShotFired");
    }

    [Fact]
    public void Tick_弾が的に当たる_的が破壊されスコアが増える()
    {
        var config = new TpsConfig
        {
            PlayerStart = new Vector3(0f, 0f, 0f),
            PlayerStartYaw = 0f,
            BulletSpeed = 1f,
            FireIntervalTicks = 1,
            TargetSpawns = [new Vector3(0f, 0f, 2f)],
            TargetRadius = 0.6f,
            BulletRadius = 0.2f,
            BulletHeight = 1f,
            TargetHeight = 2f,
            TargetRespawnTicks = 10,
        };
        var game = new TpsLogic(seed: 1, config);

        game.Tick(new InputState(Shoot: true));
        // 弾が的まで届くまで進める
        for (var i = 0; i < 5 && game.AliveTargetCount > 0; i++)
        {
            game.Tick(default);
        }

        game.AliveTargetCount.ShouldBe(0);
        game.Score.ShouldBe(1);
        game.Events.ShouldContain(e => e.Name == "TargetDestroyed");
        game.Targets[0].RespawnTicksLeft.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Tick_破壊後に一定Tick経過_的が復活する()
    {
        var config = new TpsConfig
        {
            PlayerStart = new Vector3(0f, 0f, 0f),
            BulletSpeed = 1f,
            FireIntervalTicks = 1,
            TargetSpawns = [new Vector3(0f, 0f, 2f)],
            TargetRadius = 0.6f,
            BulletRadius = 0.2f,
            TargetRespawnTicks = 3,
        };
        var game = new TpsLogic(seed: 1, config);

        game.Tick(new InputState(Shoot: true));
        for (var i = 0; i < 5 && game.AliveTargetCount > 0; i++)
        {
            game.Tick(default);
        }
        game.AliveTargetCount.ShouldBe(0);

        game.Tick(default); // respawn 2
        game.Tick(default); // respawn 1
        game.Tick(default); // respawn 0 → Alive

        game.AliveTargetCount.ShouldBe(1);
        game.Events.ShouldContain(e => e.Name == "TargetRespawned");
    }

    [Fact]
    public void Tick_Quit入力_終了要求になり以降Tickしても状態が変わらない()
    {
        var game = new TpsLogic(seed: 1);
        var before = game.PlayerPosition;

        game.Tick(new InputState(Quit: true));
        game.Tick(new InputState(Forward: true, Shoot: true));

        game.IsQuitRequested.ShouldBeTrue();
        game.PlayerPosition.ShouldBe(before);
        game.Bullets.Count.ShouldBe(0);
        game.Events.ShouldContain(e => e.Name == "QuitRequested");
    }

    [Fact]
    public void 初期状態_設定どおりの的が生存している()
    {
        var game = new TpsLogic(seed: 1);

        game.AliveTargetCount.ShouldBe(game.Config.TargetSpawns.Count);
        game.Targets.ShouldAllBe(t => t.Alive);
    }
}
