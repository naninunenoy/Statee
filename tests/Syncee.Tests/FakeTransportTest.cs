using Shouldly;

namespace Syncee.Fake.Tests;

public class FakeTransportTest
{
    [Fact]
    public void Connect_クライアント接続が来る_サーバ側にClientConnectedが通知される()
    {
        var server = new FakeServerTransport();
        ITransport? accepted = null;
        server.ClientConnected += t => accepted = t;

        var client = server.Connect();

        accepted.ShouldNotBeNull();
        accepted.ShouldNotBeSameAs(client);
    }

    [Fact]
    public void Send_クライアントからサーバへ送る_サーバ側でReceivedが同じ内容で発火する()
    {
        var server = new FakeServerTransport();
        ITransport? serverSide = null;
        server.ClientConnected += t => serverSide = t;
        var client = server.Connect();

        byte[]? received = null;
        serverSide!.Received += payload => received = payload;
        client.Send([1, 2, 3]);

        received.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Send_サーバからクライアントへ送る_クライアント側でReceivedが同じ内容で発火する()
    {
        var server = new FakeServerTransport();
        ITransport? serverSide = null;
        server.ClientConnected += t => serverSide = t;
        var client = server.Connect();

        byte[]? received = null;
        client.Received += payload => received = payload;
        serverSide!.Send([9, 8, 7]);

        received.ShouldBe(new byte[] { 9, 8, 7 });
    }

    [Fact]
    public void Disconnect_片方が切断する_両方でDisconnectedが発火する()
    {
        var server = new FakeServerTransport();
        ITransport? serverSide = null;
        server.ClientConnected += t => serverSide = t;
        var client = server.Connect();

        var clientNotified = false;
        var serverNotified = false;
        client.Disconnected += () => clientNotified = true;
        serverSide!.Disconnected += () => serverNotified = true;

        client.Disconnect();

        clientNotified.ShouldBeTrue();
        serverNotified.ShouldBeTrue();
    }
}
