using Shouldly;

namespace Syncee.Tests;

public class AuthorityLogTest
{
    [Fact]
    public void TrySubmit_合法なコマンド_確定してEntriesに積まれCommittedが発火する()
    {
        var log = new AuthorityLog((_, _, _) => true);
        CommandEnvelope? committed = null;
        log.Committed += e => committed = e;

        var ok = log.TrySubmit("client1", "place", new Dictionary<string, string> { ["x"] = "2" });

        ok.ShouldBeTrue();
        log.Entries.Count.ShouldBe(1);
        log.Entries[0].ClientId.ShouldBe("client1");
        log.Entries[0].Command.ShouldBe("place");
        log.Entries[0].Sequence.ShouldBe(0);
        committed.ShouldBe(log.Entries[0]);
    }

    [Fact]
    public void TrySubmit_非合法なコマンド_確定せずEntriesに積まれない()
    {
        var log = new AuthorityLog((_, _, _) => false);
        var committed = false;
        log.Committed += _ => committed = true;

        var ok = log.TrySubmit("client1", "place", null);

        ok.ShouldBeFalse();
        log.Entries.ShouldBeEmpty();
        committed.ShouldBeFalse();
    }

    [Fact]
    public void TrySubmit_複数回呼ぶ_Sequenceが連番になる()
    {
        var log = new AuthorityLog((_, _, _) => true);

        log.TrySubmit("client1", "place", null);
        log.TrySubmit("client2", "place", null);

        log.Entries[0].Sequence.ShouldBe(0);
        log.Entries[1].Sequence.ShouldBe(1);
    }
}
