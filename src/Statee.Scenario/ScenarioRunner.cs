namespace Statee.Scenario;

/// <summary>
/// Ruby で書かれた動作確認シナリオを実行する(ChibiRuby 埋め込み)。
/// Ruby へ公開する語彙は send / state / wait / assert の4つだけに保つ。
/// </summary>
public sealed class ScenarioRunner(IScenarioClient client, TextWriter output)
{
    private readonly IScenarioClient _client = client;
    private readonly TextWriter _output = output;

    /// <summary>シナリオを実行する。成功なら 0、失敗(assert 失敗・コマンドエラー・構文エラー)なら 1。</summary>
    public int Run(string rubySource) => default;
}
