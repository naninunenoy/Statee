using Shouldly;

namespace Syncee.Tests;

public class TickBundleAuthorityTest
{
    [Fact]
    public void Submit_1人分だけ_確定しない()
    {
        var authority = new TickBundleAuthority(expectedClientCount: 2);
        TickBundle? committed = null;
        authority.Committed += b => committed = b;

        authority.Submit(0, "client1", new Dictionary<string, string> { ["action"] = "attack" });

        authority.Entries.ShouldBeEmpty();
        committed.ShouldBeNull();
    }

    [Fact]
    public void Submit_全員分揃う_確定してEntriesに積まれCommittedが発火する()
    {
        var authority = new TickBundleAuthority(expectedClientCount: 2);
        TickBundle? committed = null;
        authority.Committed += b => committed = b;

        authority.Submit(0, "client1", new Dictionary<string, string> { ["action"] = "attack" });
        authority.Submit(0, "client2", new Dictionary<string, string> { ["action"] = "idle" });

        authority.Entries.Count.ShouldBe(1);
        authority.Entries[0].Tick.ShouldBe(0);
        authority.Entries[0].InputsByClient["client1"]!["action"].ShouldBe("attack");
        authority.Entries[0].InputsByClient["client2"]!["action"].ShouldBe("idle");
        committed.ShouldBe(authority.Entries[0]);
    }

    [Fact]
    public void Submit_複数Tickを順に揃える_Tick順にEntriesが積まれる()
    {
        var authority = new TickBundleAuthority(expectedClientCount: 2);

        authority.Submit(0, "client1", null);
        authority.Submit(0, "client2", null);
        authority.Submit(1, "client1", null);
        authority.Submit(1, "client2", null);

        authority.Entries.Count.ShouldBe(2);
        authority.Entries[0].Tick.ShouldBe(0);
        authority.Entries[1].Tick.ShouldBe(1);
    }

    [Fact]
    public void Submit_確定済みTickへの再送は無視される()
    {
        var authority = new TickBundleAuthority(expectedClientCount: 2);
        authority.Submit(0, "client1", null);
        authority.Submit(0, "client2", null);
        var committedCount = 0;
        authority.Committed += _ => committedCount++;

        authority.Submit(0, "client1", null);

        authority.Entries.Count.ShouldBe(1);
        committedCount.ShouldBe(0);
    }
}
