using Shouldly;
using Syncee.LiteNetLib;

namespace Syncee.Tests;

/// <summary>
/// 実ソケット(ループバック)での最小疎通確認(D-050「実ソケットの E2E は薄く保つ」)。
/// 厚いテストはフェイクトランスポート(FakeTransportTest)側に置き、ここでは
/// LiteNetLib の配線が正しく ITransport の契約を満たすことだけを確認する。
/// </summary>
public class LiteNetLibTransportTest
{
    private static void PollUntil(Func<bool> condition, params Action[] pollEach)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("実ソケットの疎通がタイムアウトした");
            }
            foreach (var poll in pollEach)
            {
                poll();
            }
            Thread.Sleep(5);
        }
    }

    [Fact]
    public void 接続から送受信と切断まで_実ソケットで一往復する()
    {
        using var server = new LiteNetLibServerTransport(0);
        using var client = new LiteNetLibClientTransport();

        ITransport? serverSide = null;
        server.ClientConnected += t => serverSide = t;
        var clientConnected = false;
        client.Connected += () => clientConnected = true;

        client.Connect("127.0.0.1", server.Port);
        PollUntil(
            () => serverSide is not null && clientConnected,
            server.PollEvents,
            client.PollEvents
        );

        byte[]? serverReceived = null;
        serverSide!.Received += payload => serverReceived = payload;
        client.Send([1, 2, 3]);
        PollUntil(() => serverReceived is not null, server.PollEvents, client.PollEvents);
        serverReceived.ShouldBe(new byte[] { 1, 2, 3 });

        byte[]? clientReceived = null;
        client.Received += payload => clientReceived = payload;
        serverSide.Send([9, 8, 7]);
        PollUntil(() => clientReceived is not null, server.PollEvents, client.PollEvents);
        clientReceived.ShouldBe(new byte[] { 9, 8, 7 });

        var serverNotified = false;
        var clientNotified = false;
        serverSide.Disconnected += () => serverNotified = true;
        client.Disconnected += () => clientNotified = true;
        client.Disconnect();
        PollUntil(() => serverNotified && clientNotified, server.PollEvents, client.PollEvents);
    }
}
