using System.Diagnostics;
using Shouldly;

namespace Statee.Core.Tests;

public class TimeControlTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Freeze_実行直後_IsFrozenがtrueになる()
    {
        var timeControl = new TimeControl();

        timeControl.Freeze();

        timeControl.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void Unfreeze_凍結中_IsFrozenがfalseになる()
    {
        var timeControl = new TimeControl();
        timeControl.Freeze();

        timeControl.Unfreeze();

        timeControl.IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public void Step_実行直後_凍結が解除される()
    {
        var timeControl = new TimeControl();
        timeControl.Freeze();

        timeControl.Step(3);

        timeControl.IsFrozen.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void OnFrame_stepの指定フレーム数に達した_自動で再凍結する(int frames)
    {
        var timeControl = new TimeControl();
        timeControl.Freeze();
        timeControl.Step(frames);

        for (var i = 0; i < frames; i++)
        {
            timeControl.OnFrame();
        }

        timeControl.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void OnFrame_stepの残りフレームがある_凍結解除のまま()
    {
        var timeControl = new TimeControl();
        timeControl.Freeze();
        timeControl.Step(3);

        timeControl.OnFrame();
        timeControl.OnFrame();

        timeControl.IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public void OnFrame_凍結中_凍結のまま変化しない()
    {
        var timeControl = new TimeControl();
        timeControl.Freeze();

        timeControl.OnFrame();

        timeControl.IsFrozen.ShouldBeTrue();
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
        timeControl.Freeze();
        timeControl.Step(1);
        timeControl.OnFrame();

        timeControl.WaitForStep(Timeout).ShouldBeTrue();
    }

    [Fact]
    public void WaitForStep_フレームが進まない_タイムアウトしてfalseを返す()
    {
        var timeControl = new TimeControl();
        timeControl.Freeze();
        timeControl.Step(1);

        timeControl.WaitForStep(TimeSpan.FromMilliseconds(50)).ShouldBeFalse();
    }

    [Fact]
    public void Freeze_step進行中_stepを打ち切りWaitForStepが解除される()
    {
        var timeControl = new TimeControl();
        timeControl.Step(5);

        timeControl.Freeze();

        timeControl.WaitForStep(Timeout).ShouldBeTrue();
        timeControl.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void RegisterTimeControl_freezeコマンド_凍結して応答が返る()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl);

        var response = host.HandleRequest(new StateeRequest("1", "freeze", null));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void RegisterTimeControl_unfreezeコマンド_凍結が解除される()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl);
        timeControl.Freeze();

        var response = host.HandleRequest(new StateeRequest("1", "unfreeze", null));

        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public async Task RegisterTimeControl_stepコマンド_フレーム進行の完了後に応答が返る()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl);
        timeControl.Freeze();

        var handle = Task.Run(() =>
            host.HandleRequest(
                new StateeRequest("1", "step", new Dictionary<string, string> { ["frames"] = "3" })
            )
        );
        // ゲームループ役: step が終わる(=再凍結)までフレームを供給する
        RunFramesUntil(timeControl, () => handle.IsCompleted);

        var response = await handle;
        response.Status.ShouldBe(StateeResponse.StatusOk);
        timeControl.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void RegisterTimeControl_stepコマンドでフレームが進まない_エラー応答になる()
    {
        var host = new StateeHost();
        var timeControl = new TimeControl();
        host.RegisterTimeControl(timeControl, stepTimeout: TimeSpan.FromMilliseconds(50));
        timeControl.Freeze();

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

            if (!timeControl.IsFrozen)
            {
                timeControl.OnFrame();
            }

            Thread.Yield();
        }
    }
}
