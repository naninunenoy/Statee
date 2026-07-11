using Shouldly;

namespace Statee.Scenario.Tests;

/// <summary>
/// マルチインスタンス検証語彙(D-051)のテスト。複数ターゲット接続・宛先指定・
/// クロスインスタンス wait を、実ソケットではなくスパイクライアントで検証する。
/// </summary>
public class MultiTargetScenarioTest
{
    [Fact]
    public void target_ポート指定で名前付きターゲットに接続する_connectファクトリにポートが渡る()
    {
        var main = new SpyClient();
        var spy = new SpyClient { Payload = "connected" };
        var ports = new List<int>();
        var runner = new ScenarioRunner(
            main,
            TextWriter.Null,
            connect: port =>
            {
                ports.Add(port);
                return spy;
            }
        );

        var exit = runner.Run("""target :client1, port: 5001""");

        exit.ShouldBe(0);
        ports.ShouldBe([5001]);
    }

    [Fact]
    public void on_ブロック内のsendは指定したターゲットへ送られる()
    {
        var main = new SpyClient();
        var client1 = new SpyClient();
        var runner = new ScenarioRunner(main, TextWriter.Null, connect: _ => client1);

        var exit = runner.Run(
            """
            target :client1, port: 5001
            on(:client1) { send "place", "x=2", "y=3" }
            send "ping"
            """
        );

        exit.ShouldBe(0);
        client1.Invocations.Single().Command.ShouldBe("place");
        client1
            .Invocations.Single()
            .Args.ShouldBe(new Dictionary<string, string> { ["x"] = "2", ["y"] = "3" });
        main.Invocations.ShouldBe([("ping", null)]);
    }

    [Fact]
    public void on_ブロックを抜けると宛先が既定に戻る()
    {
        var main = new SpyClient();
        var client1 = new SpyClient();
        var runner = new ScenarioRunner(main, TextWriter.Null, connect: _ => client1);

        var exit = runner.Run(
            """
            target :client1, port: 5001
            on(:client1) { send "a" }
            send "b"
            """
        );

        exit.ShouldBe(0);
        client1.Invocations.ShouldBe([("a", null)]);
        main.Invocations.ShouldBe([("b", null)]);
    }

    [Fact]
    public void on_未知のターゲット名_失敗する()
    {
        var main = new SpyClient();
        var runner = new ScenarioRunner(main, TextWriter.Null, connect: _ => new SpyClient());

        var exit = runner.Run("""on(:nope) { send "a" }""");

        exit.ShouldBe(1);
    }

    [Fact]
    public void target未使用でtargetを呼ぶ_connectファクトリ未設定なら失敗する()
    {
        var main = new SpyClient();
        var runner = new ScenarioRunner(main, TextWriter.Null);

        var exit = runner.Run("""target :client1, port: 5001""");

        exit.ShouldBe(1);
    }

    [Fact]
    public void wait_all_列挙した全ターゲットへ同じwait条件が送られる()
    {
        var main = new SpyClient();
        var client1 = new SpyClient();
        var client2 = new SpyClient();
        var queue = new Queue<SpyClient>([client1, client2]);
        var runner = new ScenarioRunner(main, TextWriter.Null, connect: _ => queue.Dequeue());

        var exit = runner.Run(
            """
            target :client1, port: 5001
            target :client2, port: 5002
            wait_all [:client1, :client2], "game/board", "Black", "eq", 4
            """
        );

        exit.ShouldBe(0);
        var expectedArgs = new Dictionary<string, string>
        {
            ["path"] = "game/board",
            ["field"] = "Black",
            ["op"] = "eq",
            ["value"] = "4",
        };
        client1.Invocations.Single().Command.ShouldBe("wait");
        client1.Invocations.Single().Args.ShouldBe(expectedArgs);
        client2.Invocations.Single().Command.ShouldBe("wait");
        client2.Invocations.Single().Args.ShouldBe(expectedArgs);
    }
}
