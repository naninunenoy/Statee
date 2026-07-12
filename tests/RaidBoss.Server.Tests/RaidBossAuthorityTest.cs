using RaidBoss.Logic;
using Shouldly;
using Syncee;
using Syncee.Fake;

namespace RaidBoss.Server.Tests;

public class RaidBossAuthorityTest
{
    private static void SendInput(ITransport transport, int tick, string action) =>
        transport.Send(
            SyncWire.Serialize(
                new CommandRequest(
                    "input",
                    new Dictionary<string, string>
                    {
                        ["tick"] = tick.ToString(),
                        ["action"] = action,
                    }
                )
            )
        );

    private static void SendStart(ITransport transport) =>
        transport.Send(SyncWire.Serialize(new CommandRequest("start", null)));

    [Fact]
    public void 接続する_ConnectedClientCountが増える()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);

        serverTransport.Connect();
        serverTransport.Connect();

        authority.ConnectedClientCount.ShouldBe(2);
    }

    [Fact]
    public void start前は入力してもPlayingへ遷移しない()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        serverTransport.Connect();

        SendInput(client1, 0, "attack");

        authority.Game.Phase.ShouldBe(GamePhase.Waiting);
        authority.ConfirmedTickCount.ShouldBe(0);
    }

    [Fact]
    public void start人数分の接続後startするとPlayingになり参加人数が確定する()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        serverTransport.Connect();
        serverTransport.Connect();

        SendStart(client1);

        authority.Game.Phase.ShouldBe(GamePhase.Playing);
        authority.Game.PlayerCount.ShouldBe(3);
    }

    [Fact]
    public void 接続が1人でもstartすればPlayingになる()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();

        SendStart(client1);

        authority.Game.Phase.ShouldBe(GamePhase.Playing);
        authority.Game.PlayerCount.ShouldBe(1);
    }

    [Fact]
    public void startすると開始通知が全クライアントへ配布される()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        TickBundle? received1 = null;
        TickBundle? received2 = null;
        client1.Received += bytes => received1 = SyncWire.DeserializeTickBundle(bytes);
        client2.Received += bytes => received2 = SyncWire.DeserializeTickBundle(bytes);

        SendStart(client1);

        received1!.Tick.ShouldBe(RaidBossAuthority.StartNotificationTick);
        received1.InputsByClient.Count.ShouldBe(2);
        received2!.Tick.ShouldBe(RaidBossAuthority.StartNotificationTick);
    }

    [Fact]
    public void 片方だけ入力する_確定せず両クライアントへ配布されない()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        SendStart(client1);

        SendInput(client1, 0, "attack");

        authority.ConfirmedTickCount.ShouldBe(0);
        authority.Game.BossHp.ShouldBe(GameLogic.BossMaxHp);
    }

    [Fact]
    public void 両者揃って入力する_確定して弾が発射され両クライアントへ配布される()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        SendStart(client1);
        TickBundle? received1 = null;
        TickBundle? received2 = null;
        client1.Received += bytes => received1 = SyncWire.DeserializeTickBundle(bytes);
        client2.Received += bytes => received2 = SyncWire.DeserializeTickBundle(bytes);

        SendInput(client1, 0, "attack");
        SendInput(client2, 0, "attack");

        authority.ConfirmedTickCount.ShouldBe(1);
        authority.Game.Projectiles.Count.ShouldBe(2);
        authority.Game.BossHp.ShouldBe(GameLogic.BossMaxHp);
        received1!.Tick.ShouldBe(0);
        received2!.Tick.ShouldBe(0);
    }

    [Fact]
    public void 弾が着弾するとボスHPが減る()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        SendStart(client1);

        SendInput(client1, 0, "attack");
        SendInput(client2, 0, "attack");
        for (var tick = 1; tick <= GameLogic.ProjectileTravelTicks; tick++)
        {
            SendInput(client1, tick, "idle");
            SendInput(client2, tick, "idle");
        }

        authority.Game.TickCount.ShouldBe(GameLogic.ProjectileTravelTicks + 1);
        authority.Game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage * 2);
    }
}
