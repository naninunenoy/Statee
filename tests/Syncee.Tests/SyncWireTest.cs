using Shouldly;

namespace Syncee.Tests;

public class SyncWireTest
{
    [Fact]
    public void CommandRequest_シリアライズ往復_内容が一致する()
    {
        var request = new CommandRequest(
            "place",
            new Dictionary<string, string> { ["x"] = "2", ["y"] = "3" }
        );

        var restored = SyncWire.DeserializeRequest(SyncWire.Serialize(request));

        restored.Command.ShouldBe("place");
        restored.Args.ShouldNotBeNull();
        restored.Args!["x"].ShouldBe("2");
        restored.Args!["y"].ShouldBe("3");
    }

    [Fact]
    public void CommandRequest_引数なし_シリアライズ往復できる()
    {
        var request = new CommandRequest("start", null);

        var restored = SyncWire.DeserializeRequest(SyncWire.Serialize(request));

        restored.Command.ShouldBe("start");
        restored.Args.ShouldBeNull();
    }

    [Fact]
    public void CommandEnvelope_シリアライズ往復_内容が一致する()
    {
        var envelope = new CommandEnvelope(
            3,
            "client-1",
            "place",
            new Dictionary<string, string> { ["x"] = "0", ["y"] = "1" }
        );

        var restored = SyncWire.DeserializeEnvelope(SyncWire.Serialize(envelope));

        restored.Sequence.ShouldBe(envelope.Sequence);
        restored.ClientId.ShouldBe(envelope.ClientId);
        restored.Command.ShouldBe(envelope.Command);
        restored.Args.ShouldNotBeNull();
        restored.Args!["x"].ShouldBe("0");
        restored.Args!["y"].ShouldBe("1");
    }
}
