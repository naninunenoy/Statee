using Shouldly;

namespace Statee.Scenario.Tests;

public class RecordingScenarioClientTest
{
    private static readonly string ReportDir = Path.Combine(Path.GetTempPath(), "statee-report");

    [Fact]
    public void Invoke_通常コマンド_成功後にscreenshotとstateを追加送信し記録する()
    {
        var inner = new SpyClient { Payload = "Score: 12" };
        var recorder = new StepRecorder();
        var client = new RecordingScenarioClient(inner, recorder, ReportDir, "game/board");

        var payload = client.Invoke("drop", new Dictionary<string, string> { ["x"] = "300" });

        payload.ShouldBe("Score: 12");
        inner.Invocations.Select(i => i.Command).ShouldBe(["drop", "screenshot", "state"]);
        inner.Invocations[2].Args!["path"].ShouldBe("game/board");

        var step = recorder.Steps.Single();
        step.Command.ShouldBe("drop");
        step.Args!["x"].ShouldBe("300");
        step.Payload.ShouldBe("Score: 12");
        step.StateToon.ShouldBe("Score: 12");
        step.Error.ShouldBeNull();
    }

    [Fact]
    public void Invoke_screenshotのパス_reportDir配下shotsの絶対パスで連番になる()
    {
        var inner = new SpyClient();
        var recorder = new StepRecorder();
        var client = new RecordingScenarioClient(inner, recorder, ReportDir, "game/board");

        client.Invoke("drop");
        client.Invoke("wait");

        var paths = inner
            .Invocations.Where(i => i.Command == "screenshot")
            .Select(i => i.Args!["path"])
            .ToList();
        paths.Count.ShouldBe(2);
        paths[0].ShouldBe(Path.GetFullPath(Path.Combine(ReportDir, "shots", "step-001.png")));
        paths[1].ShouldBe(Path.GetFullPath(Path.Combine(ReportDir, "shots", "step-002.png")));
        recorder.Steps[0].ScreenshotPath.ShouldBe(paths[0]);
        recorder.Steps[1].ScreenshotPath.ShouldBe(paths[1]);
    }

    [Fact]
    public void Invoke_stateとscreenshot自身_素通しして記録しない()
    {
        var inner = new SpyClient();
        var recorder = new StepRecorder();
        var client = new RecordingScenarioClient(inner, recorder, ReportDir, "game/board");

        client.Invoke("state", new Dictionary<string, string> { ["path"] = "game/ui" });
        client.Invoke("screenshot", new Dictionary<string, string> { ["path"] = "x.png" });

        inner.Invocations.Select(i => i.Command).ShouldBe(["state", "screenshot"]);
        recorder.Steps.ShouldBeEmpty();
    }

    [Fact]
    public void Invoke_statePathがnull_stateは送らずStateToonはnull()
    {
        var inner = new SpyClient();
        var recorder = new StepRecorder();
        var client = new RecordingScenarioClient(inner, recorder, ReportDir);

        client.Invoke("drop");

        inner.Invocations.Select(i => i.Command).ShouldBe(["drop", "screenshot"]);
        recorder.Steps.Single().StateToon.ShouldBeNull();
    }

    [Fact]
    public void Invoke_コマンドが失敗_Error付きで記録し例外はそのまま伝播する()
    {
        var inner = new SpyClient { ErrorCommand = "drop", ErrorMessage = "投下できない" };
        var recorder = new StepRecorder();
        var client = new RecordingScenarioClient(inner, recorder, ReportDir, "game/board");

        Should.Throw<InvalidOperationException>(() => client.Invoke("drop"));

        // 失敗時は screenshot / state を追加送信しない
        inner.Invocations.Select(i => i.Command).ShouldBe(["drop"]);
        var step = recorder.Steps.Single();
        step.Command.ShouldBe("drop");
        step.Error.ShouldBe("投下できない");
        step.Payload.ShouldBeNull();
        step.ScreenshotPath.ShouldBeNull();
    }

    [Fact]
    public void Invoke_screenshotが失敗_ステップは残りシナリオは止めない()
    {
        var inner = new SpyClient { ErrorCommand = "screenshot", ErrorMessage = "保存失敗" };
        var recorder = new StepRecorder();
        var client = new RecordingScenarioClient(inner, recorder, ReportDir, "game/board");

        var payload = Should.NotThrow(() => client.Invoke("drop"));

        payload.ShouldBe("");
        var step = recorder.Steps.Single();
        step.ScreenshotPath.ShouldBeNull();
        step.Error.ShouldNotBeNull();
        step.Error.ShouldContain("保存失敗");
    }

    [Fact]
    public void Invoke_expect実行後_ステップに現在の期待説明が付く()
    {
        var inner = new SpyClient();
        var recorder = new StepRecorder();
        var client = new RecordingScenarioClient(inner, recorder, ReportDir, "game/board");

        client.Invoke("drop"); // expect 前
        recorder.BeginExpectation("スコアが増える");
        client.Invoke("drop");
        client.Invoke("wait");
        recorder.BeginExpectation("ゲームオーバーになる");
        client.Invoke("drop");

        recorder
            .Steps.Select(s => s.Expectation)
            .ShouldBe([null, "スコアが増える", "スコアが増える", "ゲームオーバーになる"]);
    }

    [Fact]
    public void Run_expect語彙_recorderに期待説明が渡りワイヤには何も送らない()
    {
        var inner = new SpyClient();
        var recorder = new StepRecorder();
        var runner = new ScenarioRunner(inner, TextWriter.Null, recorder);

        var exit = runner.Run("""expect "タイトルが表示される" """);

        exit.ShouldBe(0);
        inner.Invocations.ShouldBeEmpty();
        recorder.CurrentExpectation.ShouldBe("タイトルが表示される");
    }

    [Fact]
    public void Run_recorder無しでexpectを呼ぶ_無害に成功する()
    {
        var inner = new SpyClient();
        var runner = new ScenarioRunner(inner, TextWriter.Null);

        var exit = runner.Run(
            """
            expect "説明"
            send "ping"
            """
        );

        exit.ShouldBe(0);
        inner.Invocations.Select(i => i.Command).ShouldBe(["ping"]);
    }

    private sealed class SpyClient : IScenarioClient
    {
        public string Payload { get; init; } = "";

        /// <summary>このコマンドが来たら ErrorMessage で失敗する。</summary>
        public string? ErrorCommand { get; init; }

        public string? ErrorMessage { get; init; }

        public List<(string Command, Dictionary<string, string>? Args)> Invocations { get; } = [];

        public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null)
        {
            Invocations.Add((command, args is null ? null : new Dictionary<string, string>(args)));
            return command == ErrorCommand
                ? throw new InvalidOperationException(ErrorMessage)
                : Payload;
        }
    }
}
