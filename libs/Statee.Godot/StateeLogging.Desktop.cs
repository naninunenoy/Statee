using Microsoft.Extensions.Logging;
using Statee.Core;
using ZLogger.Providers;

namespace Statee.Godot;

public static partial class StateeLogging
{
    private static partial ILoggerFactory CreateCore(LogBuffer buffer) =>
        new TeeLoggerFactory(
            new BufferLoggerProvider(buffer),
            new ZLoggerConsoleLoggerProvider(new ZLoggerConsoleOptions())
        );
}
