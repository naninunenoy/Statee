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
    public void 片方だけ入力する_確定せず両クライアントへ配布されない()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        serverTransport.Connect();

        SendInput(client1, 0, "attack");

        authority.ConfirmedTickCount.ShouldBe(0);
        authority.Game.BossHp.ShouldBe(GameLogic.BossMaxHp);
    }

    [Fact]
    public void 両者揃って入力する_確定してボスHPが減り両クライアントへ配布される()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        TickBundle? received1 = null;
        TickBundle? received2 = null;
        client1.Received += bytes => received1 = SyncWire.DeserializeTickBundle(bytes);
        client2.Received += bytes => received2 = SyncWire.DeserializeTickBundle(bytes);

        SendInput(client1, 0, "attack");
        SendInput(client2, 0, "attack");

        authority.ConfirmedTickCount.ShouldBe(1);
        authority.Game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage * 2);
        received1!.Tick.ShouldBe(0);
        received2!.Tick.ShouldBe(0);
    }

    [Fact]
    public void 複数Tickを順に確定する_Tick順にGameLogicへ適用される()
    {
        var serverTransport = new FakeServerTransport();
        var authority = new RaidBossAuthority(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();

        SendInput(client1, 0, "attack");
        SendInput(client2, 0, "idle");
        SendInput(client1, 1, "idle");
        SendInput(client2, 1, "attack");

        authority.Game.TickCount.ShouldBe(2);
        authority.Game.BossHp.ShouldBe(GameLogic.BossMaxHp - GameLogic.PlayerAttackDamage * 2);
    }
}
