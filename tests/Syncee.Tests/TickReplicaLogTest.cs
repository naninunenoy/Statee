using Shouldly;

namespace Syncee.Tests;

public class TickReplicaLogTest
{
    [Fact]
    public void OnReceived_確定バンドルを受信する_Entriesに積まれapplyが呼ばれる()
    {
        var applied = new List<TickBundle>();
        var log = new TickReplicaLog(b => applied.Add(b));
        var bundle = new TickBundle(
            0,
            new Dictionary<string, IReadOnlyDictionary<string, string>?>()
        );

        log.OnReceived(bundle);

        log.Entries.ShouldBe([bundle]);
        applied.ShouldBe([bundle]);
    }

    [Fact]
    public void OnReceived_複数回受信する_受信順にEntriesへ積まれる()
    {
        var log = new TickReplicaLog(_ => { });
        var first = new TickBundle(
            0,
            new Dictionary<string, IReadOnlyDictionary<string, string>?>()
        );
        var second = new TickBundle(
            1,
            new Dictionary<string, IReadOnlyDictionary<string, string>?>()
        );

        log.OnReceived(first);
        log.OnReceived(second);

        log.Entries.ShouldBe([first, second]);
    }
}
