using System.Diagnostics;
using Shouldly;

namespace Statee.Core.Tests;

public class MainThreadDispatcherTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Run_Pumpが呼ばれる_結果が呼び出し元に返る()
    {
        var dispatcher = new MainThreadDispatcher(Timeout);

        var run = Task.Run(() => dispatcher.Run(() => 42));
        PumpUntil(dispatcher, () => run.IsCompleted);

        (await run).ShouldBe(42);
    }

    [Fact]
    public async Task Run_処理が例外を投げる_呼び出し元に伝播する()
    {
        var dispatcher = new MainThreadDispatcher(Timeout);

        var run = Task.Run(() =>
            dispatcher.Run(() => throw new InvalidOperationException("処理失敗"))
        );
        PumpUntil(dispatcher, () => run.IsCompleted);

        var exception = await run.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("処理失敗");
    }

    [Fact]
    public async Task Run_Pumpが呼ばれない_タイムアウト例外になる()
    {
        var dispatcher = new MainThreadDispatcher(TimeSpan.FromMilliseconds(50));

        var run = Task.Run(() => dispatcher.Run(() => 42));

        await run.ShouldThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Pump_実行スレッド_Pumpを呼んだスレッドで処理が走る()
    {
        var dispatcher = new MainThreadDispatcher(Timeout);
        var pumpingThreadId = Environment.CurrentManagedThreadId;

        var run = Task.Run(() => dispatcher.Run(() => Environment.CurrentManagedThreadId));
        PumpUntil(dispatcher, () => run.IsCompleted);

        (await run).ShouldBe(pumpingThreadId);
    }

    [Fact]
    public async Task RegisterMainThreadCommand_ディスパッチャ設定済み_Pump側で実行され応答が返る()
    {
        var dispatcher = new MainThreadDispatcher(Timeout);
        var host = new StateeHost { MainThreadDispatcher = dispatcher };
        host.RegisterMainThreadCommand("greet", _ => new { Message = "hello" });

        var handle = Task.Run(() => host.HandleRequest(new StateeRequest("1", "greet", null)));
        PumpUntil(dispatcher, () => handle.IsCompleted);

        var response = await handle;
        response.Status.ShouldBe(StateeResponse.StatusOk);
        response.Payload.ShouldNotBeNull();
        response.Payload.ShouldContain("hello");
    }

    [Fact]
    public void RegisterMainThreadCommand_ディスパッチャ未設定_エラー応答になる()
    {
        var host = new StateeHost();
        host.RegisterMainThreadCommand("greet", _ => new { Message = "hello" });

        var response = host.HandleRequest(new StateeRequest("1", "greet", null));

        response.Status.ShouldBe(StateeResponse.StatusError);
        response.Error.ShouldNotBeNull();
        response.Error.ShouldContain("MainThreadDispatcher");
    }

    /// <summary>条件が満たされるまで Pump を繰り返す(固定スリープ禁止: GUIDELINE.md §3.1)。</summary>
    private static void PumpUntil(MainThreadDispatcher dispatcher, Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException("条件が満たされないまま待機時間を超えた");
            }

            dispatcher.Pump();
            Thread.Yield();
        }
    }
}
