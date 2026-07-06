using System.Net;
using System.Text;

namespace Statee.Scenario;

/// <summary>
/// 記録済みステップから人間レビュー用の HTML レポートを生成する(D-034)。
/// 単一の自己完結 HTML(CSS インライン)。画像のみ shots/ への相対参照。
/// </summary>
public static class HtmlReportWriter
{
    /// <summary>レポート HTML を文字列として組み立てる。</summary>
    public static string Render(IReadOnlyList<ScenarioStep> steps)
    {
        var html = new StringBuilder();
        html.Append(
            """
            <!doctype html>
            <html lang="ja">
            <head>
            <meta charset="utf-8">
            <title>Statee 検証レポート</title>
            <style>
            body { font-family: sans-serif; margin: 2rem; background: #fafafa; color: #222; }
            h2 { border-bottom: 2px solid #888; padding-bottom: .3rem; margin-top: 2.5rem; }
            .step { background: #fff; border: 1px solid #ddd; border-radius: 6px;
                    padding: 1rem; margin: 1rem 0; }
            .step h3 { margin: 0 0 .5rem; font-size: 1rem; }
            .step img { max-width: 480px; border: 1px solid #ccc; display: block; margin: .5rem 0; }
            pre { background: #f4f4f4; padding: .6rem; overflow-x: auto; }
            .error { color: #b00020; font-weight: bold; }
            .meta { color: #666; font-size: .85rem; }
            </style>
            </head>
            <body>
            <h1>Statee 検証レポート</h1>

            """
        );

        string? currentSection = null;
        var stepNumber = 0;
        foreach (var step in steps)
        {
            if (step.Expectation != currentSection)
            {
                currentSection = step.Expectation;
                html.Append("<h2>期待: ")
                    .Append(Escape(currentSection ?? "(期待未記載)"))
                    .Append("</h2>\n");
            }

            stepNumber++;
            html.Append("<div class=\"step\">\n");
            html.Append("<h3>#")
                .Append(stepNumber)
                .Append(' ')
                .Append(Escape(FormatCommand(step)))
                .Append("</h3>\n");
            if (step.Payload is not null)
            {
                html.Append("<p class=\"meta\">payload</p><pre>")
                    .Append(Escape(step.Payload))
                    .Append("</pre>\n");
            }

            if (step.ScreenshotPath is not null)
            {
                var fileName = Path.GetFileName(step.ScreenshotPath);
                html.Append("<img src=\"shots/")
                    .Append(Escape(fileName))
                    .Append("\" alt=\"")
                    .Append(Escape(fileName))
                    .Append("\">\n");
            }

            if (step.StateToon is not null)
            {
                html.Append("<p class=\"meta\">state</p><pre>")
                    .Append(Escape(step.StateToon))
                    .Append("</pre>\n");
            }

            if (step.Error is not null)
            {
                html.Append("<p class=\"error\">").Append(Escape(step.Error)).Append("</p>\n");
            }

            html.Append("</div>\n");
        }

        html.Append("</body>\n</html>\n");
        return html.ToString();
    }

    /// <summary>reportDir/report.html にレポートを書き出し、そのパスを返す。</summary>
    public static string Write(IReadOnlyList<ScenarioStep> steps, string reportDir)
    {
        Directory.CreateDirectory(reportDir);
        var path = Path.Combine(reportDir, "report.html");
        File.WriteAllText(path, Render(steps));
        return path;
    }

    private static string FormatCommand(ScenarioStep step) =>
        step.Args is null or { Count: 0 }
            ? step.Command
            : $"{step.Command} {string.Join(" ", step.Args.Select(a => $"{a.Key}={a.Value}"))}";

    private static string Escape(string text) => WebUtility.HtmlEncode(text);
}
