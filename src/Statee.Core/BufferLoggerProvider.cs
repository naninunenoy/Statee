using Microsoft.Extensions.Logging;

namespace Statee.Core;

/// <summary>ILogger への出力を LogBuffer に記録する LoggerProvider。</summary>
public sealed class BufferLoggerProvider(LogBuffer buffer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BufferLogger(buffer, categoryName);

    public void Dispose() { }

    private sealed class BufferLogger(LogBuffer buffer, string category) : ILogger
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
            if (!IsEnabled(logLevel))
            {
                return;
            }

            buffer.Add(
                new LogEntry(DateTimeOffset.UtcNow, logLevel, category, formatter(state, exception))
            );
        }
    }
}
