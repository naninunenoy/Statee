using Shouldly;
using Syncee.Fake;

namespace Syncee.Tests;

public class ClientRegistryTest
{
    [Fact]
    public void 接続する_ConnectedClientCountが増えclient番号が採番される()
    {
        var serverTransport = new FakeServerTransport();
        var registry = new ClientRegistry(serverTransport);
        var connectedIds = new List<string>();
        registry.ClientConnected += (clientId, _) => connectedIds.Add(clientId);

        serverTransport.Connect();
        serverTransport.Connect();

        registry.ConnectedClientCount.ShouldBe(2);
        connectedIds.ShouldBe(["client-1", "client-2"]);
    }

    [Fact]
    public void Received_クライアントからの受信をclientId付きで通知する()
    {
        var serverTransport = new FakeServerTransport();
        var registry = new ClientRegistry(serverTransport);
        var client1 = serverTransport.Connect();
        (string ClientId, byte[] Bytes)? received = null;
        registry.Received += (clientId, bytes) => received = (clientId, bytes);

        client1.Send([1, 2, 3]);

        received!.Value.ClientId.ShouldBe("client-1");
        received!.Value.Bytes.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Disconnect_ConnectedClientCountが減りClientDisconnectedが発火する()
    {
        var serverTransport = new FakeServerTransport();
        var registry = new ClientRegistry(serverTransport);
        var client1 = serverTransport.Connect();
        serverTransport.Connect();
        string? disconnectedId = null;
        registry.ClientDisconnected += (clientId, _) => disconnectedId = clientId;

        client1.Disconnect();

        registry.ConnectedClientCount.ShouldBe(1);
        disconnectedId.ShouldBe("client-1");
    }

    [Fact]
    public void Broadcast_接続中の全クライアントへ送信する()
    {
        var serverTransport = new FakeServerTransport();
        var registry = new ClientRegistry(serverTransport);
        var client1 = serverTransport.Connect();
        var client2 = serverTransport.Connect();
        byte[]? received1 = null;
        byte[]? received2 = null;
        client1.Received += bytes => received1 = bytes;
        client2.Received += bytes => received2 = bytes;

        registry.Broadcast([9, 9]);

        received1.ShouldBe(new byte[] { 9, 9 });
        received2.ShouldBe(new byte[] { 9, 9 });
    }
}
