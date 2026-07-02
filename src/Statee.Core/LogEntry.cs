using Microsoft.Extensions.Logging;

namespace Statee.Core;

/// <summary>LogBuffer に保持される1件のログ。</summary>
public readonly record struct LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    string Message
);
