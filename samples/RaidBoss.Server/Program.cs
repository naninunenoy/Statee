using Microsoft.Extensions.Logging;
using RaidBoss.Server;
using Statee.Core;
using Statee.Remote;
using Syncee.LiteNetLib;
using Syncee.Statee;
using ZLogger;

// RaidBoss の権威サーバ(D-053/D-054/D-056)。決定論ロックステップの確定係(TickBundleAuthority)を
// 純C#コンソールプロセスとして動かし、Statee を組み込んで権威 State(game/raidboss)を
// 直接観測できるようにする。物理・当たり判定は持たず、Tick 入力の確定・配布に徹する。
// 複数部屋(合言葉違い)を1プロセスで同時に扱う(RoomManager。D-056)。接続直後の
// ゲートは持たず、最初の1通(create/join + room引数)で部屋へ振り分ける。

var port = 9312;
var gamePort = 9412;
foreach (var arg in args)
{
    if (arg.StartsWith("--port=", StringComparison.Ordinal) && int.TryParse(arg[7..], out var p))
    {
        port = p;
    }
    if (
        arg.StartsWith("--game-port=", StringComparison.Ordinal)
        && int.TryParse(arg[12..], out var gp)
    )
    {
        gamePort = gp;
    }
}

var loggerFactory = LoggerFactory.Create(builder =>
    builder.SetMinimumLevel(LogLevel.Information).AddZLoggerConsole()
);
var logBuffer = new LogBuffer(1024);
var logger = loggerFactory.CreateLogger("RaidBoss.Server");

var raidBossState = new RaidBossState();

var gameTransport = new LiteNetLibServerTransport(gamePort);
var rooms = new RoomManager(gameTransport);

void RefreshState()
{
    if (rooms.LastTouchedRoom is { } room)
    {
        raidBossState.Update(room.Authority.Game);
    }
}

var dispatcher = new MainThreadDispatcher();
var time = new TimeControl();
var host = new StateeHost(logBuffer) { MainThreadDispatcher = dispatcher };
host.RegisterStateProvider(raidBossState);

// wait コマンド(D-028)はフレーム進行が前提のため登録する(N-6 と同型のクロスインスタンス検証用)
host.RegisterTimeControl(time);
host.RegisterStateProvider(
    new SyncStateProvider(
        "game/sync",
        () =>
            rooms.LastTouchedRoom is { } room
                ? new SyncSnapshot(
                    room.Authority.ConnectedClientCount,
                    room.Authority.ConfirmedTickCount,
                    null
                )
                : new SyncSnapshot(0, 0, null)
    )
);
host.RegisterCommand(
    "ping",
    cmdArgs =>
    {
        var message = cmdArgs.GetString("message") ?? "ping";
        logger.ZLogInformation($"ping を受信: {message}");
        return new { Pong = true, Message = message };
    }
);

var running = true;
host.RegisterMainThreadCommand(
    "quit",
    _ =>
    {
        logger.ZLogInformation($"quit を受信。終了する");
        running = false;
        return new { Quitting = true };
    }
);

await using var server = new StateeTcpServer(host, port);
server.Start();
logger.ZLogInformation($"RaidBoss.Server 待ち受け開始 port={server.Port} game-port={gamePort}");

while (running)
{
    dispatcher.Pump();
    gameTransport.PollEvents();
    RefreshState();
    if (!time.IsFrozen)
    {
        time.OnFrame();
    }
    await Task.Delay(10);
}

gameTransport.Dispose();
logger.ZLogInformation($"RaidBoss.Server 終了");
