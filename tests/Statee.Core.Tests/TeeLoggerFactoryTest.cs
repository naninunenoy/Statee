using Microsoft.Extensions.Logging;
using Shouldly;

namespace Statee.Core.Tests;

public class TeeLoggerFactoryTest
{
    [Fact]
    public void Log_複数プロバイダ_同じメッセージが両方に届く()
    {
        var first = new List<string>();
        var second = new List<string>();
        using var factory = new TeeLoggerFactory(
            new ListLoggerProvider(first),
            new ListLoggerProvider(second)
        );
        var logger = factory.CreateLogger("Game.Main");

        logger.LogInformation("分配されるログ");

        first.ShouldBe(["分配されるログ"]);
        second.ShouldBe(["分配されるログ"]);
    }

    [Fact]
    public void CreateLogger_渡したカテゴリ_プロバイダへ同じ名前で作る()
    {
        var provider = new RecordingProvider();
        using var factory = new TeeLoggerFactory(provider);

        factory.CreateLogger("SuikaGame.Main");

        provider.Categories.ShouldBe(["SuikaGame.Main"]);
    }

    private sealed class ListLoggerProvider(List<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ListLogger(sink);

        public void Dispose() { }

        private sealed class ListLogger(List<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            )
            {
                sink.Add(formatter(state, exception));
            }
        }
    }

    private sealed class RecordingProvider : ILoggerProvider
    {
        public List<string> Categories { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            Categories.Add(categoryName);
            return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        public void Dispose() { }
    }
}
