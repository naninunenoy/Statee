using Microsoft.Extensions.Logging;
using Shouldly;
using Statee.Core;

namespace Statee.Core.Tests;

public class StateeHostTest
{
    private sealed class FakeStateProvider(string path, object state) : IStateProvider
    {
        public string Path => path;

        public object CaptureState() => state;
    }

    private static LogEntry Entry(string message) =>
        new(DateTimeOffset.UtcNow, LogLevel.Information, "test", message);

    [Fact]
    public void HandleRequest_登録済みコマンド_okと結果のpayloadを返す()
    {
        var host = new StateeHost();
        host.RegisterCommand("ping", args => new { Pong = true, Message = args.GetString("message") });

        var response = host.HandleRequest(
            new StateeRequest("1", "ping", new Dictionary<string, string> { ["message"] = "hello" }));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldNotBeNullOrEmpty();
        response.Payload.ShouldContain("hello");
    }

    [Fact]
    public void HandleRequest_応答のid_リクエストのidと一致する()
    {
        var host = new StateeHost();
        host.RegisterCommand("ping", _ => new { Pong = true });

        var response = host.HandleRequest(new StateeRequest("req-7", "ping"));

        response.Id.ShouldBe("req-7");
    }

    [Fact]
    public void HandleRequest_未知のコマンド_errorと理由を返す()
    {
        var host = new StateeHost();

        var response = host.HandleRequest(new StateeRequest("1", "nope"));

        response.Status.ShouldBe(StateeResponse.StatusError);
        response.Error.ShouldNotBeNullOrEmpty();
        response.Error.ShouldContain("nope");
    }

    [Fact]
    public void HandleRequest_ハンドラが例外を投げる_errorと例外メッセージを返す()
    {
        var host = new StateeHost();
        host.RegisterCommand("boom", _ => throw new InvalidOperationException("爆発した"));

        var response = host.HandleRequest(new StateeRequest("1", "boom"));

        response.Status.ShouldBe(StateeResponse.StatusError);
        response.Error.ShouldNotBeNull();
        response.Error.ShouldContain("爆発した");
    }

    [Fact]
    public void HandleRequest_stateコマンド_登録プロバイダのスナップショットを返す()
    {
        var host = new StateeHost();
        host.RegisterStateProvider(new FakeStateProvider("system", new { Frame = 42 }));

        var response = host.HandleRequest(
            new StateeRequest("1", "state", new Dictionary<string, string> { ["path"] = "system" }));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldNotBeNullOrEmpty();
        response.Payload.ShouldContain("42");
    }

    [Fact]
    public void HandleRequest_stateコマンドで未知のパス_errorを返す()
    {
        var host = new StateeHost();
        host.RegisterStateProvider(new FakeStateProvider("system", new { Frame = 42 }));

        var response = host.HandleRequest(
            new StateeRequest("1", "state", new Dictionary<string, string> { ["path"] = "scene" }));

        response.Status.ShouldBe(StateeResponse.StatusError);
        response.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void HandleRequest_logsコマンド_保持しているログを返す()
    {
        var buffer = new LogBuffer(16);
        var host = new StateeHost(buffer);
        buffer.Add(Entry("古いメッセージ"));
        buffer.Add(Entry("新しいメッセージ"));

        var response = host.HandleRequest(new StateeRequest("1", "logs"));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldNotBeNullOrEmpty();
        response.Payload.ShouldContain("古いメッセージ");
        response.Payload.ShouldContain("新しいメッセージ");
    }

    [Fact]
    public void HandleRequest_logsコマンドでtail指定_新しい方からN件のみ返す()
    {
        var buffer = new LogBuffer(16);
        var host = new StateeHost(buffer);
        buffer.Add(Entry("古いメッセージ"));
        buffer.Add(Entry("新しいメッセージ"));

        var response = host.HandleRequest(
            new StateeRequest("1", "logs", new Dictionary<string, string> { ["tail"] = "1" }));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldNotBeNullOrEmpty();
        response.Payload.ShouldContain("新しいメッセージ");
        response.Payload.ShouldNotContain("古いメッセージ");
    }
}
