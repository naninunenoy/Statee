using Microsoft.Extensions.Logging;
using Statee.Core;
using ZLogger;

namespace Statee.Godot;

/// <summary>
/// Statee の logs コマンド用バッファとコンソールの両方へ流すロガーの定型構築。
/// 返した ILoggerFactory はゲーム側が _ExitTree で Dispose する。
/// </summary>
public static class StateeLogging
{
    public static ILoggerFactory CreateLoggerFactory(LogBuffer buffer)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new BufferLoggerProvider(buffer));
            builder.AddZLoggerConsole();
        });
    }
}
