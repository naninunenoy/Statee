namespace Statee.Scenario;

/// <summary>
/// シナリオ DSL がターゲットを呼ぶための境界。本番は StateeClient(TCP)、テストはフェイクを注入する。
/// </summary>
public interface IScenarioClient
{
    /// <summary>コマンドを送り、成功時の payload(TOON)を返す。失敗は例外で表す。</summary>
    string Invoke(string command, IReadOnlyDictionary<string, string>? args = null);
}
