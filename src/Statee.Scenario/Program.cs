using ConsoleAppFramework;
using Statee.Cli;
using Statee.Scenario;

var app = ConsoleApp.Create();

// シナリオ(.rb)を実行する。exit code: 0 = シナリオ成功 / 1 = 失敗
// --report-dir を指定すると、ステップごとのスクショ + State を記録し
// 終了時(失敗時も)に <report-dir>/report.html を出力する(D-034)
app.Add(
    "run",
    (string script, int port = 9310, string? reportDir = null, string? reportState = null) =>
    {
        var source = File.ReadAllText(script);
        IScenarioClient client = new TcpScenarioClient(new StateeClient(port));
        if (reportDir is null)
        {
            return new ScenarioRunner(client, Console.Out).Run(source);
        }

        var recorder = new StepRecorder();
        client = new RecordingScenarioClient(client, recorder, reportDir, reportState);
        try
        {
            return new ScenarioRunner(client, Console.Out, recorder).Run(source);
        }
        finally
        {
            var reportPath = HtmlReportWriter.Write(recorder.Steps, reportDir);
            Console.WriteLine($"レポート: {reportPath}");
        }
    }
);
app.Run(args);
