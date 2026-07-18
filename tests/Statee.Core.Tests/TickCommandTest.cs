using System.Diagnostics;
using Shouldly;

namespace Statee.Core.Tests;

public class TickCommandTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed record Harness(
        StateeHost Host,
        MainThreadDispatcher Dispatcher,
        TimeControl Time,
        List<string> Steps
    );

    /// <summary>input 引数をそのまま入力とし、step の呼び出し履歴を記録する tick コマンドを組む。</summary>
    private static Harness Create(int maxFramesPerCall = 3600)
    {
        var dispatcher = new MainThreadDispatcher(Timeout);
        var host = new StateeHost { MainThreadDispatcher = dispatcher };
        var time = new TimeControl();
        var steps = new List<string>();
        host.RegisterTickCommand(
            time,
            parseInput: args => args.GetString("input") ?? "",
            step: steps.Add,
            result: () => new { Count = steps.Count },
            maxFramesPerCall: maxFramesPerCall
        );
        return new Harness(host, dispatcher, time, steps);
    }

    private static StateeResponse Send(Harness harness, string? args)
    {
        var argMap = args
            ?.Split(',')
            .Select(pair => pair.Split('='))
            .ToDictionary(kv => kv[0], kv => kv[1]);
        var handle = Task.Run(() =>
            harness.Host.HandleRequest(new StateeRequest("1", "tick", argMap))
        );
        var stopwatch = Stopwatch.StartNew();
        while (!handle.IsCompleted)
        {
            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException("tick コマンドが完了しない");
            }
            harness.Dispatcher.Pump();
            Thread.Yield();
        }
        return handle.Result;
    }

    [Fact]
    public void Tick_frames指定_指定回数stepが呼ばれOnFrameが進む()
    {
        var harness = Create();

        var response = Send(harness, "frames=3,input=right+fire");

        response.Status.ShouldBe(StateeResponse.StatusOk);
        harness.Steps.ShouldBe(["right+fire", "right+fire", "right+fire"]);
        harness.Time.FrameCount.ShouldBe(3);
    }

    [Fact]
    public void Tick_frames省略_1回だけ進む()
    {
        var harness = Create();

        Send(harness, "input=fire");

        harness.Steps.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_上限超過のframes_上限にクランプされる()
    {
        var harness = Create(maxFramesPerCall: 5);

        Send(harness, "frames=100");

        harness.Steps.Count.ShouldBe(5);
    }

    [Fact]
    public void Tick_結果_resultの戻り値がペイロードになる()
    {
        var harness = Create();

        var response = Send(harness, "frames=2");

        response.Payload.ShouldNotBeNull();
        response.Payload.ShouldContain("2"); // Count = 2
    }

    [Fact]
    public void Tick_入力の解釈が例外を投げる_エラー応答になり進まない()
    {
        var dispatcher = new MainThreadDispatcher(Timeout);
        var host = new StateeHost { MainThreadDispatcher = dispatcher };
        var time = new TimeControl();
        host.RegisterTickCommand<string>(
            time,
            parseInput: _ => throw new ArgumentException("未知の入力トークン"),
            step: _ => { },
            result: () => new { }
        );
        var harness = new Harness(host, dispatcher, time, []);

        var response = Send(harness, "frames=3,input=bogus");

        response.Status.ShouldBe(StateeResponse.StatusError);
        response.Error.ShouldNotBeNull();
        response.Error.ShouldContain("未知の入力トークン");
        harness.Time.FrameCount.ShouldBe(0);
    }
}
