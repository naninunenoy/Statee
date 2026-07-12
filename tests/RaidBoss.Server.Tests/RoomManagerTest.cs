using Shouldly;
using Syncee;
using Syncee.Fake;

namespace RaidBoss.Server.Tests;

public class RoomManagerTest
{
    private static void SendCreate(ITransport transport, string room) =>
        transport.Send(
            SyncWire.Serialize(
                new CommandRequest("create", new Dictionary<string, string> { ["room"] = room })
            )
        );

    private static void SendJoin(ITransport transport, string room) =>
        transport.Send(
            SyncWire.Serialize(
                new CommandRequest("join", new Dictionary<string, string> { ["room"] = room })
            )
        );

    [Fact]
    public void createで新しい部屋ができ接続数が1になる()
    {
        var rawTransport = new FakeServerTransport();
        var manager = new RoomManager(rawTransport);
        var client1 = rawTransport.Connect();

        SendCreate(client1, "abc");

        manager.Rooms.ShouldContainKey("abc");
        manager.Rooms["abc"].Authority.ConnectedClientCount.ShouldBe(1);
    }

    [Fact]
    public void 同じ合言葉でjoinすると同じ部屋に参加する()
    {
        var rawTransport = new FakeServerTransport();
        var manager = new RoomManager(rawTransport);
        var client1 = rawTransport.Connect();
        var client2 = rawTransport.Connect();

        SendCreate(client1, "abc");
        SendJoin(client2, "abc");

        manager.Rooms["abc"].Authority.ConnectedClientCount.ShouldBe(2);
    }

    [Fact]
    public void 存在しない合言葉でjoinすると切断される()
    {
        var rawTransport = new FakeServerTransport();
        var manager = new RoomManager(rawTransport);
        var client1 = rawTransport.Connect();
        var disconnected = false;
        client1.Disconnected += () => disconnected = true;

        SendJoin(client1, "no-such-room");

        disconnected.ShouldBeTrue();
    }

    [Fact]
    public void 既に存在する合言葉でcreateすると切断される()
    {
        var rawTransport = new FakeServerTransport();
        var manager = new RoomManager(rawTransport);
        var client1 = rawTransport.Connect();
        var client2 = rawTransport.Connect();
        SendCreate(client1, "abc");
        var disconnected = false;
        client2.Disconnected += () => disconnected = true;

        SendCreate(client2, "abc");

        disconnected.ShouldBeTrue();
        manager.Rooms["abc"].Authority.ConnectedClientCount.ShouldBe(1);
    }

    [Fact]
    public void 別の合言葉は別の部屋になる()
    {
        var rawTransport = new FakeServerTransport();
        var manager = new RoomManager(rawTransport);
        var client1 = rawTransport.Connect();
        var client2 = rawTransport.Connect();

        SendCreate(client1, "abc");
        SendCreate(client2, "xyz");

        manager.Rooms.Count.ShouldBe(2);
        manager.Rooms["abc"].Authority.ShouldNotBe(manager.Rooms["xyz"].Authority);
    }
}
