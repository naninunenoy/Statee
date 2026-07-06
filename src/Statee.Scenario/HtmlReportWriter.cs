namespace Statee.Scenario;

/// <summary>
/// 記録済みステップから人間レビュー用の HTML レポートを生成する(D-034)。
/// 単一の自己完結 HTML(CSS インライン)。画像のみ shots/ への相対参照。
/// </summary>
public static class HtmlReportWriter
{
    /// <summary>レポート HTML を文字列として組み立てる。</summary>
    public static string Render(IReadOnlyList<ScenarioStep> steps) =>
        throw new NotImplementedException();

    /// <summary>reportDir/report.html にレポートを書き出し、そのパスを返す。</summary>
    public static string Write(IReadOnlyList<ScenarioStep> steps, string reportDir) =>
        throw new NotImplementedException();
}
