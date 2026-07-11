namespace Statee.Scenario.Tests;

/// <summary>テスト用のスパイ IScenarioClient。ScenarioRunnerTest / マルチターゲットのテストで共用する。</summary>
internal sealed class SpyClient : IScenarioClient
{
    public string Payload { get; init; } = "";

    /// <summary>設定すると Invoke がこのメッセージで失敗する。</summary>
    public string? ErrorMessage { get; init; }

    public List<(string Command, Dictionary<string, string>? Args)> Invocations { get; } = [];

    public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null)
    {
        Invocations.Add((command, args is null ? null : new Dictionary<string, string>(args)));
        return ErrorMessage is null ? Payload : throw new InvalidOperationException(ErrorMessage);
    }
}
