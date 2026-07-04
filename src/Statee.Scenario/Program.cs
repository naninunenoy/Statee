using ConsoleAppFramework;
using Statee.Cli;
using Statee.Scenario;

var app = ConsoleApp.Create();

// シナリオ(.rb)を実行する。exit code: 0 = シナリオ成功 / 1 = 失敗
app.Add(
    "run",
    (string script, int port = 9310) =>
    {
        var source = File.ReadAllText(script);
        var client = new TcpScenarioClient(new StateeClient(port));
        return new ScenarioRunner(client, Console.Out).Run(source);
    }
);
app.Run(args);
