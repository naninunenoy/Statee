using System.Net;
using System.Net.Sockets;
using System.Text;
using Shouldly;
using Statee.Core;

namespace Statee.Remote.Tests;

[Trait("Category", "Integration")]
public class StateeTcpServerTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static StateeHost CreateHostWithPing()
    {
        var host = new StateeHost();
        host.RegisterCommand(
            "ping",
            args => new { Pong = true, Message = args.GetString("message") }
        );
        return host;
    }

    private static async Task<(
        TcpClient Client,
        StreamReader Reader,
        StreamWriter Writer
    )> ConnectAsync(int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(Timeout);
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        return (client, reader, writer);
    }

    [Fact]
    public async Task 接続してpingリクエストを送信_ok応答を受信する()
    {
        await using var server = new StateeTcpServer(CreateHostWithPing(), 0);
        server.Start();
        var (client, reader, writer) = await ConnectAsync(server.Port);
        using var _ = client;

        var request = new StateeRequest(
            "1",
            "ping",
            new Dictionary<string, string> { ["message"] = "hello" }
        );
        await writer.WriteLineAsync(StateeJson.Serialize(request)).WaitAsync(Timeout);
        var line = await reader.ReadLineAsync().WaitAsync(Timeout);

        line.ShouldNotBeNull();
        var response = StateeJson.DeserializeResponse(line);
        response.ShouldNotBeNull();
        response.Id.ShouldBe("1");
        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldContain("hello");
    }

    [Fact]
    public async Task 不正なJSON行を送信_error応答が返り接続は維持される()
    {
        await using var server = new StateeTcpServer(CreateHostWithPing(), 0);
        server.Start();
        var (client, reader, writer) = await ConnectAsync(server.Port);
        using var _ = client;

        await writer.WriteLineAsync("これはJSONではない").WaitAsync(Timeout);
        var errorLine = await reader.ReadLineAsync().WaitAsync(Timeout);
        errorLine.ShouldNotBeNull();
        StateeJson.DeserializeResponse(errorLine)!.Status.ShouldBe(StateeResponse.StatusError);

        var request = new StateeRequest(
            "2",
            "ping",
            new Dictionary<string, string> { ["message"] = "まだ生きてる" }
        );
        await writer.WriteLineAsync(StateeJson.Serialize(request)).WaitAsync(Timeout);
        var okLine = await reader.ReadLineAsync().WaitAsync(Timeout);
        okLine.ShouldNotBeNull();
        var response = StateeJson.DeserializeResponse(okLine);
        response.ShouldNotBeNull();
        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldContain("まだ生きてる");
    }
}
