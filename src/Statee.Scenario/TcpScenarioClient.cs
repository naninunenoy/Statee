using Statee.Cli;

namespace Statee.Scenario;

/// <summary>StateeClient(TCP)を IScenarioClient に適合させる。</summary>
public sealed class TcpScenarioClient(StateeClient client) : IScenarioClient
{
    /// <inheritdoc/>
    public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null) =>
        client.Invoke(command, args);
}
