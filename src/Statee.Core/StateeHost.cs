namespace Statee.Core;

/// <summary>
/// State/Command/Log を束ね、リクエストを処理するフレームワークの中心。
/// 組み込みコマンド: state(path)/ logs(tail)。トランスポートには依存しない。
/// </summary>
public sealed class StateeHost
{
    public StateeHost(LogBuffer? logBuffer = null) { }

    public LogBuffer Logs => null!;

    public void RegisterCommand(string name, CommandHandler handler) { }

    public void RegisterStateProvider(IStateProvider provider) { }

    public StateeResponse HandleRequest(StateeRequest request) => null!;
}
