using Microsoft.Extensions.Logging;
using Shouldly;

namespace Statee.Core.Tests;

public class LogBufferTest
{
    private static LogEntry Entry(string message) =>
        new(DateTimeOffset.UtcNow, LogLevel.Information, "test", message);

    [Fact]
    public void Add_容量以内_全件を時系列順で保持する()
    {
        var buffer = new LogBuffer(3);

        buffer.Add(Entry("1件目"));
        buffer.Add(Entry("2件目"));

        buffer.Count.ShouldBe(2);
        buffer.Tail(10).Select(e => e.Message).ShouldBe(["1件目", "2件目"]);
    }

    [Fact]
    public void Add_容量超過_古いエントリから破棄される()
    {
        var buffer = new LogBuffer(3);

        buffer.Add(Entry("1件目"));
        buffer.Add(Entry("2件目"));
        buffer.Add(Entry("3件目"));
        buffer.Add(Entry("4件目"));

        buffer.Count.ShouldBe(3);
        buffer.Tail(10).Select(e => e.Message).ShouldBe(["2件目", "3件目", "4件目"]);
    }

    [Fact]
    public void Tail_保持件数より小さい指定_新しい方からN件を時系列順で返す()
    {
        var buffer = new LogBuffer(10);

        buffer.Add(Entry("1件目"));
        buffer.Add(Entry("2件目"));
        buffer.Add(Entry("3件目"));

        buffer.Tail(2).Select(e => e.Message).ShouldBe(["2件目", "3件目"]);
    }
}
