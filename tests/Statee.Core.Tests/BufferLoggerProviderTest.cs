using Microsoft.Extensions.Logging;
using Shouldly;

namespace Statee.Core.Tests;

public class BufferLoggerProviderTest
{
    [Fact]
    public void Log_ILogger経由の出力_LogBufferに記録される()
    {
        var buffer = new LogBuffer(16);
        using var provider = new BufferLoggerProvider(buffer);
        var logger = provider.CreateLogger("Game.Main");

        logger.LogInformation("こんにちはログ");

        buffer.Count.ShouldBe(1);
        var entry = buffer.Tail(1)[0];
        entry.Category.ShouldBe("Game.Main");
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldContain("こんにちはログ");
    }
}
