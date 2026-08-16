using Microsoft.Extensions.Logging;

namespace Statee.Core;

/// <summary>
/// 複数の <see cref="ILoggerProvider"/> へ同じログを分配する。
/// MEL の <c>LoggerFactory.Create</c>(DI)を起動経路に載せないための受け皿(D-083)。
/// </summary>
public sealed class TeeLoggerFactory : ILoggerFactory
{
    public TeeLoggerFactory(params ILoggerProvider[] providers) { }

    public void AddProvider(ILoggerProvider provider) { }

    public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public void Dispose() { }
}
