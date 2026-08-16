using Microsoft.Extensions.Logging;

namespace Statee.Core;

/// <summary>
/// 複数の <see cref="ILoggerProvider"/> へ同じログを分配する。
/// MEL の <c>LoggerFactory.Create</c>(DI)を起動経路に載せないための受け皿(D-083)。
/// </summary>
public sealed class TeeLoggerFactory(params ILoggerProvider[] providers) : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotSupportedException("プロバイダはコンストラクタで渡す(D-083)");
    }

    public ILogger CreateLogger(string categoryName)
    {
        var loggers = new ILogger[providers.Length];
        for (var i = 0; i < providers.Length; i++)
        {
            loggers[i] = providers[i].CreateLogger(categoryName);
        }

        return new TeeLogger(loggers);
    }

    public void Dispose()
    {
        foreach (var provider in providers)
        {
            provider.Dispose();
        }
    }

    private sealed class TeeLogger(ILogger[] inners) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            foreach (var inner in inners)
            {
                if (inner.IsEnabled(logLevel))
                {
                    return true;
                }
            }

            return false;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            foreach (var inner in inners)
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
