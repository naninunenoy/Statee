using Shouldly;

namespace Syncee.Tests;

public class ReplicaLogTest
{
    [Fact]
    public void OnReceived_確定コマンドを受信する_Entriesに積まれapplyが呼ばれる()
    {
        var applied = new List<CommandEnvelope>();
        var log = new ReplicaLog(e => applied.Add(e));
        var envelope = new CommandEnvelope(0, "client1", "place", null);

        log.OnReceived(envelope);

        log.Entries.ShouldBe([envelope]);
        applied.ShouldBe([envelope]);
    }

    [Fact]
    public void OnReceived_複数回受信する_受信順にEntriesへ積まれる()
    {
        var log = new ReplicaLog(_ => { });
        var first = new CommandEnvelope(0, "client1", "place", null);
        var second = new CommandEnvelope(1, "client2", "place", null);

        log.OnReceived(first);
        log.OnReceived(second);

        log.Entries.ShouldBe([first, second]);
    }
}
