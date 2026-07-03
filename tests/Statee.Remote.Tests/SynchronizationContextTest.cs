using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shouldly;
using Statee.Core;

namespace Statee.Remote.Tests;

/// <summary>
/// Godot .NET は await の継続をメインスレッドへ戻す SynchronizationContext を
/// インストールする。サーバーがそれを捕捉すると、メインスレッドコマンドの待機が
/// メインスレッド自身をブロックして自己デッドロックする(docs/NOTES.md)。その再現テスト。
/// </summary>
[Trait("Category", "Integration")]
public class SynchronizationContextTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Godot 風に、Post された継続を「メインスレッド」のポンプで実行するコンテキスト。</summary>
    private sealed class MainThreadSyncContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted =
            new();

        public override void Post(SendOrPostCallback d, object? state) =>
            _posted.Enqueue((d, state));

        public void Pump()
        {
            while (_posted.TryDequeue(out var item))
            {
                item.Callback(item.State);
            }
        }
    }

    [Fact]
    public async Task MainThreadCommand_Godot風の同期コンテキスト下_デッドロックせず応答が返る()
    {
        var dispatcher = new MainThreadDispatcher(TimeSpan.FromSeconds(1));
        var host = new StateeHost { MainThreadDispatcher = dispatcher };
        host.RegisterMainThreadCommand("work", _ => new { Done = true });

        var context = new MainThreadSyncContext();
        var original = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var server = new StateeTcpServer(host, port: 0);
            server.Start();

            var clientTask = Task.Run(() => SendRequest(server.Port));
            PumpUntil(context, dispatcher, () => clientTask.IsCompleted);
            var line = await clientTask.WaitAsync(Timeout);

            // 破棄も同期コンテキストに継続が積まれるため、ポンプしながら完了を待つ
            var disposeTask = server.DisposeAsync().AsTask();
            PumpUntil(context, dispatcher, () => disposeTask.IsCompleted);
            await disposeTask.WaitAsync(Timeout);

            line.ShouldNotBeNull();
            line.ShouldContain("\"ok\"");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    private static void PumpUntil(
        MainThreadSyncContext context,
        MainThreadDispatcher dispatcher,
        Func<bool> condition
    )
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < Timeout)
        {
            context.Pump();
            dispatcher.Pump();
            Thread.Yield();
        }
    }

    private static string? SendRequest(int port)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        writer.WriteLine("""{"id":"1","command":"work"}""");
        return reader.ReadLine();
    }
}
