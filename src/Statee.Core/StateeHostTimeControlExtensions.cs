namespace Statee.Core;

/// <summary>
/// TimeControl を標準コマンド(pause / resume / step)として StateeHost に登録する。
/// step は指定フレームの進行が完了してから応答を返す(AI の決定論的操作の要)。
/// </summary>
public static class StateeHostTimeControlExtensions
{
    private static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(5);

    public static void RegisterTimeControl(
        this StateeHost host,
        TimeControl timeControl,
        TimeSpan? stepTimeout = null,
        TimeSpan? waitTimeout = null
    )
    {
        var timeout = stepTimeout ?? DefaultStepTimeout;
        host.RegisterCommand(
            "pause",
            _ =>
            {
                timeControl.Pause();
                return new { timeControl.IsPaused };
            }
        );
        host.RegisterCommand(
            "resume",
            _ =>
            {
                timeControl.Resume();
                return new { timeControl.IsPaused };
            }
        );
        host.RegisterCommand(
            "step",
            args =>
            {
                var frames = args.GetInt("frames", 1);
                timeControl.Step(frames);
                if (!timeControl.WaitForStep(timeout))
                {
                    throw new TimeoutException(
                        $"step が {timeout.TotalSeconds} 秒以内に完了しない (ゲームループが動いているか確認する)"
                    );
                }

                return new { timeControl.IsPaused, Frames = frames };
            }
        );
    }
}
