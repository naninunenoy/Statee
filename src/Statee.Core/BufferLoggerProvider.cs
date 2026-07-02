using Microsoft.Extensions.Logging;

namespace Statee.Core;

/// <summary>ILogger への出力を LogBuffer に記録する LoggerProvider。</summary>
public sealed class BufferLoggerProvider : ILoggerProvider
{
    public BufferLoggerProvider(LogBuffer buffer) { }

    public ILogger CreateLogger(string categoryName) => null!;

    public void Dispose() { }
}
