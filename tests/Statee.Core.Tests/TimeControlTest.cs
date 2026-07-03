using System.Diagnostics;
using Shouldly;

namespace Statee.Core.Tests;

public class TimeControlTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Pause_実行直後_IsPausedがtrueになる()
    {
        var timeControl = new TimeControl();

        timeControl.Pause();

        timeControl.IsPaused.ShouldBeTrue();
    }

    [Fact]
    public void Resume_ポーズ中_IsPausedがfalseになる()
    {
        var timeControl = new TimeControl();
        timeControl.Pause();

        timeControl.Resume();

        timeControl.IsPaused.ShouldBeFalse();
    }

    [Fact]
    public void Step_実行直後_ポーズが解除される()
    {
        var timeControl = new TimeControl();
        timeControl.Pause();

        timeControl.Step(3);

        timeControl.IsPaused.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void OnFrame_stepの指定フレーム数に達した_自動で再ポーズする(int frames)
    {
        var timeControl = new TimeControl();
        timeControl.Pause();
        timeControl.Step(frames);

        for (var i = 0; i < frames; i++)
        {
            timeControl.OnFrame();
        }

        timeControl.IsPaused.ShouldBeTrue();
    }

    [Fact]
    public void OnFrame_stepの残りフレームがある_ポーズ解除のまま()
    {
        var timeControl = new TimeControl();
        timeControl.Pause();
        timeControl.Step(3);

        timeControl.OnFrame();
        timeControl.OnFrame();

        timeControl.IsPaused.ShouldBeFalse();
    }

    [Fact]
    public void OnFrame_ポーズ中_ポーズのまま変化しない()
    {
        var timeControl = new TimeControl();
        timeControl.Pause();

        timeControl.OnFrame();

        timeControl.IsPaused.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Step_0以下のフレーム数_ArgumentOutOfRangeException(int frames)
    {
        var timeControl = new TimeControl();

        Should.Throw<ArgumentOutOfRangeException>(() => timeControl.Step(frames));
    }

    [Fact]
    public void WaitForStep_step完了済み_trueを返す()
    {
        var timeControl = new TimeControl();
        timeControl.Pause();
        timeControl.Step(1);
        timeControl.OnFrame();

        timeControl.WaitForStep(Timeout).ShouldBeTrue();
    }

    [Fact]
    public void WaitForStep_フレームが進まない_タイムアウトしてfalseを返す()
    {
        var timeControl = new TimeControl();
        timeControl.Pause();
        timeControl.Step(1);

        timeControl.WaitForStep(TimeSpan.FromMilliseconds(50)).ShouldBeFalse();
    }

    [Fact]
    public void Pause_step進行中_stepを打ち切りWaitForStepが解除される()
    {
        var timeControl = new TimeControl();
        timeControl.Step(5);

        timeControl.Pause();

        timeControl.WaitForStep(Timeout).ShouldBeTrue();
        timeControl.IsPaused.ShouldBeTrue();
    }

    [Fact]
    public void RegisterTimeControl_pauseコマンド_ポーズして応答が返る()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl);

        var response = host.HandleRequest(new StateeRequest("1", "pause", null));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsPaused.ShouldBeTrue();
    }

    [Fact]
    public void RegisterTimeControl_resumeコマンド_ポーズが解除される()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl);
        timeControl.Pause();

        var response = host.HandleRequest(new StateeRequest("1", "resume", null));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsPaused.ShouldBeFalse();
    }

    [Fact]
    public async Task RegisterTimeControl_stepコマンド_フレーム進行の完了後に応答が返る()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl);
        timeControl.Pause();

        var handle = Task.Run(() =>
            host.HandleRequest(
                new StateeRequest("1", "step", new Dictionary<string, string> { ["frames"] = "3" })
            )
        );
        // ゲームループ役: step が終わる(=再ポーズ)までフレームを供給する
        RunFramesUntil(timeControl, () => handle.IsCompleted);

        var response = await handle;
        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsPaused.ShouldBeTrue();
    }

    [Fact]
    public void RegisterTimeControl_stepコマンドでフレームが進まない_エラー応答になる()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl, stepTimeout: TimeSpan.FromMilliseconds(50));
        timeControl.Pause();

        var response = host.HandleRequest(
            new StateeRequest("1", "step", new Dictionary<string, string> { ["frames"] = "1" })
        );

        response.Status.ShouldBe(StateeResponse.StatusError);
    }

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
}
