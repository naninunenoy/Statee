using Shouldly;

namespace Statee.Core.Tests;

public class StateeJsonTest
{
    [Fact]
    public void DeserializeRequest_D018形式のJSON_各フィールドを復元する()
    {
        var json = """{"id":"1","command":"ping","args":{"message":"hello"}}""";

        var request = StateeJson.DeserializeRequest(json);

        request.ShouldNotBeNull();
        request.Id.ShouldBe("1");
        request.Command.ShouldBe("ping");
        request.Args.ShouldNotBeNull();
        request.Args["message"].ShouldBe("hello");
    }

    [Fact]
    public void DeserializeRequest_argsの無いJSON_Argsはnull()
    {
        var json = """{"id":"2","command":"quit"}""";

        var request = StateeJson.DeserializeRequest(json);

        request.ShouldNotBeNull();
        request.Args.ShouldBeNull();
    }

    [Fact]
    public void DeserializeRequest_不正なJSON_nullを返す()
    {
        StateeJson.DeserializeRequest("これはJSONではない").ShouldBeNull();
    }

    [Fact]
    public void Serialize_複数行のpayloadを含む応答_改行を含まない1行になる()
    {
        var response = StateeResponse.Ok("1", "frame: 42\nuptime: 1.5");

        var line = StateeJson.Serialize(response);

        line.ShouldNotContain("\n");
        line.ShouldNotContain("\r");
    }

    [Fact]
    public void DeserializeResponse_Serializeした応答_元の値を復元する()
    {
        var original = new StateeResponse("9", StateeResponse.StatusOk, "frame: 42\nuptime: 1.5");

        var restored = StateeJson.DeserializeResponse(StateeJson.Serialize(original));

        restored.ShouldBe(original);
    }

    [Fact]
    public void DeserializeRequest_Serializeしたリクエスト_元の値を復元する()
    {
        var original = new StateeRequest(
            "3",
            "ping",
            new Dictionary<string, string> { ["message"] = "hello" }
        );

        var restored = StateeJson.DeserializeRequest(StateeJson.Serialize(original));

        restored.ShouldNotBeNull();
        restored.Id.ShouldBe(original.Id);
        restored.Command.ShouldBe(original.Command);
        restored.Args.ShouldNotBeNull();
        restored.Args["message"].ShouldBe("hello");
    }
}
