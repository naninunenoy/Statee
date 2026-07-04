using Shouldly;

namespace Statee.Scenario.Tests;

public class ScenarioRunnerTest
{
    [Fact]
    public void Run_sendを呼ぶ_コマンドと引数がクライアントに渡る()
    {
        var client = new SpyClient();
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run("""send "drop", "x=300", "power=2" """);

        exit.ShouldBe(0);
        client.Invocations.Single().Command.ShouldBe("drop");
        client
            .Invocations.Single()
            .Args.ShouldBe(new Dictionary<string, string> { ["x"] = "300", ["power"] = "2" });
    }

    [Fact]
    public void Run_引数なしのsend_引数なしでクライアントに渡る()
    {
        var client = new SpyClient();
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run("""send "quit" """);

        exit.ShouldBe(0);
        client.Invocations.ShouldBe([("quit", null)]);
    }

    [Fact]
    public void Run_stateを呼ぶ_stateコマンドとpath引数に写像される()
    {
        var client = new SpyClient();
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run("""state "game/board" """);

        exit.ShouldBe(0);
        client.Invocations.Single().Command.ShouldBe("state");
        client
            .Invocations.Single()
            .Args.ShouldBe(new Dictionary<string, string> { ["path"] = "game/board" });
    }

    [Fact]
    public void Run_waitを呼ぶ_waitコマンドと条件引数に写像される()
    {
        var client = new SpyClient();
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run("""wait "game/board", "Score", "ge", 1 """);

        exit.ShouldBe(0);
        client.Invocations.Single().Command.ShouldBe("wait");
        client
            .Invocations.Single()
            .Args.ShouldBe(
                new Dictionary<string, string>
                {
                    ["path"] = "game/board",
                    ["field"] = "Score",
                    ["op"] = "ge",
                    ["value"] = "1",
                }
            );
    }

    [Fact]
    public void Run_waitにタイムアウトを渡す_timeoutMs引数が付く()
    {
        var client = new SpyClient();
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run("""wait "game/board", "Score", "ge", 1, 3000 """);

        exit.ShouldBe(0);
        client.Invocations.Single().Args.ShouldNotBeNull();
        client.Invocations.Single().Args!["timeoutMs"].ShouldBe("3000");
    }

    [Fact]
    public void Run_sendの戻り値_payloadがRubyの文字列として使える()
    {
        var client = new SpyClient { Payload = "NextKind: Cherry" };
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run(
            """
            payload = state "game/board"
            assert payload.include?("Cherry")
            """
        );

        exit.ShouldBe(0);
    }

    [Fact]
    public void Run_assertが偽_1を返しメッセージが出力される()
    {
        var client = new SpyClient();
        var output = new StringWriter();
        var runner = new ScenarioRunner(client, output);

        var exit = runner.Run("""assert 1 == 2, "スコアが想定と違う" """);

        exit.ShouldBe(1);
        output.ToString().ShouldContain("スコアが想定と違う");
    }

    [Fact]
    public void Run_assertが真_後続へ進み0を返す()
    {
        var client = new SpyClient();
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run(
            """
            assert true
            send "ping"
            """
        );

        exit.ShouldBe(0);
        client.Invocations.Count.ShouldBe(1);
    }

    [Fact]
    public void Run_コマンドがエラー応答_1を返し後続は実行されない()
    {
        var client = new SpyClient { ErrorMessage = "ゲームオーバー中は投下できない" };
        var output = new StringWriter();
        var runner = new ScenarioRunner(client, output);

        var exit = runner.Run(
            """
            send "drop"
            send "ping"
            """
        );

        exit.ShouldBe(1);
        output.ToString().ShouldContain("ゲームオーバー中は投下できない");
        client.Invocations.Count.ShouldBe(1);
    }

    [Fact]
    public void Run_コマンドエラーをrescueする_シナリオ側で失敗を扱える()
    {
        var client = new SpyClient { ErrorMessage = "ゲームオーバー中は投下できない" };
        var runner = new ScenarioRunner(client, TextWriter.Null);

        var exit = runner.Run(
            """
            failed = false
            begin
              send "drop"
            rescue => e
              failed = true
            end
            assert failed, "drop はエラーになるはず"
            """
        );

        exit.ShouldBe(0);
    }

    [Fact]
    public void Run_Rubyの構文エラー_1を返しエラーが出力される()
    {
        var client = new SpyClient();
        var output = new StringWriter();
        var runner = new ScenarioRunner(client, output);

        var exit = runner.Run("def こわれた(");

        exit.ShouldBe(1);
        output.ToString().ShouldNotBeEmpty();
    }

    private sealed class SpyClient : IScenarioClient
    {
        public string Payload { get; init; } = "";

        /// <summary>設定すると Invoke がこのメッセージで失敗する。</summary>
        public string? ErrorMessage { get; init; }

        public List<(string Command, Dictionary<string, string>? Args)> Invocations { get; } = [];

        public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null)
        {
            Invocations.Add((command, args is null ? null : new Dictionary<string, string>(args)));
            return ErrorMessage is null
                ? Payload
                : throw new InvalidOperationException(ErrorMessage);
        }
    }
}
