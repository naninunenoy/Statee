using Shouldly;

namespace Statee.Scenario.Tests;

public class HtmlReportWriterTest
{
    private static ScenarioStep Step(
        string command = "drop",
        string? expectation = null,
        string? payload = "ok",
        string? screenshotPath = null,
        string? stateToon = null,
        string? error = null
    ) => new(command, Args: null, payload, expectation, screenshotPath, stateToon, error);

    [Fact]
    public void Render_期待セクション_expectの説明文が見出しとして含まれる()
    {
        var html = HtmlReportWriter.Render([
            Step(expectation: "スコアが増える"),
            Step(expectation: "スコアが増える"),
            Step(expectation: "ゲームオーバーになる"),
        ]);

        html.ShouldContain("スコアが増える");
        html.ShouldContain("ゲームオーバーになる");
        // 同一セクションの見出しは1回だけ
        CountOf(html, "スコアが増える").ShouldBe(1);
    }

    [Fact]
    public void Render_コマンドと引数とpayload_ステップに表示される()
    {
        var step = Step(command: "drop") with
        {
            Args = new Dictionary<string, string> { ["x"] = "300" },
        };

        var html = HtmlReportWriter.Render([step]);

        html.ShouldContain("drop");
        html.ShouldContain("x=300");
        html.ShouldContain("ok");
    }

    [Fact]
    public void Render_スクショ_shotsへの相対パスでimg参照される()
    {
        var abs = Path.GetFullPath(Path.Combine("out", "shots", "step-001.png"));

        var html = HtmlReportWriter.Render([Step(screenshotPath: abs)]);

        html.ShouldContain("""<img src="shots/step-001.png""");
    }

    [Fact]
    public void Render_StateのTOON_HTMLエスケープされて含まれる()
    {
        var html = HtmlReportWriter.Render([Step(stateToon: "Label: <b>A & B</b>")]);

        html.ShouldContain("Label: &lt;b&gt;A &amp; B&lt;/b&gt;");
        html.ShouldNotContain("<b>A & B</b>");
    }

    [Fact]
    public void Render_エラー付きステップ_エラーメッセージが表示される()
    {
        var html = HtmlReportWriter.Render([Step(payload: null, error: "投下できない")]);

        html.ShouldContain("投下できない");
    }

    [Fact]
    public void Render_ステップなし_空のレポートとして成立する()
    {
        var html = HtmlReportWriter.Render([]);

        html.ShouldContain("<html");
        html.ShouldContain("</html>");
    }

    [Fact]
    public void Write_reportHtmlが書き出されパスが返る()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"statee-report-{Guid.NewGuid():N}");
        try
        {
            var path = HtmlReportWriter.Write([Step()], dir);

            path.ShouldBe(Path.Combine(dir, "report.html"));
            File.ReadAllText(path).ShouldContain("drop");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        for (
            var index = text.IndexOf(needle, StringComparison.Ordinal);
            index >= 0;
            index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal)
        )
        {
            count++;
        }

        return count;
    }
}
