using System.Diagnostics;
using Shouldly;

namespace Statee.Core.Tests;

public class WaitCommandTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void OnFrame_非ポーズ中_FrameCountが増える()
    {
        var timeControl = new TimeControl();

        timeControl.OnFrame();
        timeControl.OnFrame();

        timeControl.FrameCount.ShouldBe(2);
    }

    [Fact]
    public void OnFrame_ポーズ中_FrameCountは増えない()
    {
        var timeControl = new TimeControl();
        timeControl.Pause();

        timeControl.OnFrame();

        timeControl.FrameCount.ShouldBe(0);
    }

    [Fact]
    public async Task WaitForNextFrame_フレームが進む_trueで戻る()
    {
        var timeControl = new TimeControl();

        var wait = Task.Run(() => timeControl.WaitForNextFrame(0, Timeout));
        RunFramesUntil(timeControl, () => wait.IsCompleted);

        (await wait).ShouldBeTrue();
    }

    [Fact]
    public void WaitForNextFrame_フレームが進まない_タイムアウトでfalseを返す()
    {
        var timeControl = new TimeControl();

        timeControl.WaitForNextFrame(0, TimeSpan.FromMilliseconds(50)).ShouldBeFalse();
    }

    [Fact]
    public void CaptureState_登録済みパス_スナップショットを返す()
    {
        var host = new StateeHost();
        host.RegisterStateProvider(new FakeStateProvider("test", () => new { Value = 42 }));

        var state = host.CaptureState("test");

        state.ShouldNotBeNull();
    }

    [Fact]
    public void CaptureState_未知のパス_KeyNotFoundException()
    {
        var host = new StateeHost();

        Should.Throw<KeyNotFoundException>(() => host.CaptureState("unknown"));
    }

    [Fact]
    public void Wait_条件が既に満たされている_フレームを進めず即座に応答が返る()
    {
        var (host, _) = CreateHostWithCounter(initialValue: 10);

        var response = host.HandleRequest(WaitRequest("Value", "ge", "10"));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldNotBeNull();
        response.Payload.ShouldContain("0"); // 消費フレーム 0
    }

    [Fact]
    public async Task Wait_ポーズ中_条件成立までstepで進み応答が返る()
    {
        var (host, timeControl) = CreateHostWithCounter(initialValue: 0);
        timeControl.Pause();

        var handle = Task.Run(() => host.HandleRequest(WaitRequest("Value", "ge", "3")));
        RunFramesUntil(timeControl, () => handle.IsCompleted);

        var response = await handle;
        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsPaused.ShouldBeTrue();
        timeControl.FrameCount.ShouldBe(3);
    }

    [Fact]
    public async Task Wait_実行中_フレーム進行で条件成立を検知して応答が返る()
    {
        var (host, timeControl) = CreateHostWithCounter(initialValue: 0);

        var handle = Task.Run(() => host.HandleRequest(WaitRequest("Value", "ge", "3")));
        RunFramesUntil(timeControl, () => handle.IsCompleted);

        var response = await handle;
        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsPaused.ShouldBeFalse();
    }

    [Fact]
    public void Wait_条件が満たされない_タイムアウトのエラー応答になる()
    {
        var (host, timeControl) = CreateHostWithCounter(
            initialValue: 0,
            waitTimeout: TimeSpan.FromMilliseconds(50)
        );
        timeControl.Pause();
        // フレームを進めるゲームループが居ないため、条件は永遠に満たされない

        var response = host.HandleRequest(WaitRequest("Value", "ge", "1"));

        response.Status.ShouldBe(StateeResponse.StatusError);
        response.Error.ShouldNotBeNull();
        response.Error.ShouldContain("Value");
    }

    [Theory]
    [InlineData("eq", "10")]
    [InlineData("ne", "0")]
    [InlineData("gt", "9")]
    [InlineData("le", "10")]
    [InlineData("lt", "11")]
    public void Wait_各比較演算子で成立している_即座に応答が返る(string op, string value)
    {
        var (host, _) = CreateHostWithCounter(initialValue: 10);

        var response = host.HandleRequest(WaitRequest("Value", op, value));

        response.Status.ShouldBe(StateeResponse.StatusOk);
    }

    [Fact]
    public void Wait_未知の演算子_エラー応答になる()
    {
        var (host, _) = CreateHostWithCounter(initialValue: 10);

        var response = host.HandleRequest(WaitRequest("Value", "like", "10"));

        response.Status.ShouldBe(StateeResponse.StatusError);
    }

    [Fact]
    public void Wait_未知のフィールド_エラー応答になる()
    {
        var (host, _) = CreateHostWithCounter(initialValue: 10);

        var response = host.HandleRequest(WaitRequest("Unknown", "eq", "10"));

        response.Status.ShouldBe(StateeResponse.StatusError);
    }

    /// <summary>
    /// OnFrame のたびに 1 増えるカウンタを State("test" の Value)として公開するホストを作る。
    /// </summary>
    private static (StateeHost Host, TimeControl TimeControl) CreateHostWithCounter(
        int initialValue,
        TimeSpan? waitTimeout = null
    )
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl, waitTimeout: waitTimeout);
        host.RegisterStateProvider(
            new FakeStateProvider(
                "test",
                () => new { Value = initialValue + timeControl.FrameCount }
            )
        );
        return (host, timeControl);
    }

    private static StateeRequest WaitRequest(string field, string op, string value) =>
        new(
            "1",
            "wait",
            new Dictionary<string, string>
            {
                ["path"] = "test",
                ["field"] = field,
                ["op"] = op,
                ["value"] = value,
            }
        );

    /// <summary>条件が満たされるまでフレームを進める(固定スリープ禁止: GUIDELINE.md §3.1)。</summary>
    private static void RunFramesUntil(TimeControl timeControl, Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException("条件が満たされないまま待機時間を超えた");
            }

            if (!timeControl.IsPaused)
            {
                timeControl.OnFrame();
            }

            Thread.Yield();
        }
    }

    private sealed class FakeStateProvider(string path, Func<object> capture) : IStateProvider
    {
        public string Path => path;

        public object CaptureState() => capture();
    }
}
