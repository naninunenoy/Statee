using System;
using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;

namespace Statee.Godot;

public static partial class StateeLogging
{
    private static partial ILoggerFactory CreateCore(LogBuffer buffer) =>
        new TeeLoggerFactory(new BufferLoggerProvider(buffer), new GodotPrintLoggerProvider());
}

/// <summary>Web では GD.Print がブラウザのコンソールへ出る(D-083)。</summary>
internal sealed class GodotPrintLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new GodotPrintLogger(categoryName);

    public void Dispose() { }

    private sealed class GodotPrintLogger(string category) : ILogger
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

            var message = formatter(state, exception);
            if (exception is null)
            {
                GD.Print($"[{logLevel}] {category}: {message}");
            }
            else
            {
                GD.Print($"[{logLevel}] {category}: {message} {exception}");
            }
        }
    }
}
