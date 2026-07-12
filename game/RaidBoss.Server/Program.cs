using Microsoft.Extensions.Logging;
using RaidBoss.Server;
using Statee.Core;
using Statee.Remote;
using Syncee.LiteNetLib;
using Syncee.Statee;
using ZLogger;

// RaidBoss の権威サーバ(D-053/D-054)。決定論ロックステップの確定係(TickBundleAuthority)を
// 純C#コンソールプロセスとして動かし、Statee を組み込んで権威 State(game/raidboss)を
// 直接観測できるようにする。物理・当たり判定は持たず、Tick 入力の確定・配布に徹する。

var port = 9312;
var gamePort = 9412;
var room = "raidboss";
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
    if (arg.StartsWith("--room=", StringComparison.Ordinal))
    {
        room = arg[7..];
    }
}

var loggerFactory = LoggerFactory.Create(builder =>
    builder.SetMinimumLevel(LogLevel.Information).AddZLoggerConsole()
);
var logBuffer = new LogBuffer(1024);
var logger = loggerFactory.CreateLogger("RaidBoss.Server");

var raidBossState = new RaidBossState();

// 合言葉(ルームパスフレーズ)。D-052 と同じ最小ゲート
var gameTransport = new LiteNetLibServerTransport(gamePort, connectionKey: room);
var authority = new RaidBossAuthority(gameTransport);

void RefreshState() => raidBossState.Update(authority.Game);

RefreshState();
authority.Committed += bundle =>
{
    RefreshState();
    logger.ZLogInformation(
        $"確定 tick={bundle.Tick} → boss={authority.Game.BossHp} p1={authority.Game.Player1Hp} p2={authority.Game.Player2Hp} phase={authority.Game.Phase}"
    );
};

var dispatcher = new MainThreadDispatcher();
var time = new TimeControl();
var host = new StateeHost(logBuffer) { MainThreadDispatcher = dispatcher };
host.RegisterStateProvider(raidBossState);

// wait コマンド(D-028)はフレーム進行が前提のため登録する(N-6 と同型のクロスインスタンス検証用)
host.RegisterTimeControl(time);
host.RegisterStateProvider(
    new SyncStateProvider(
        "game/sync",
        () => new SyncSnapshot(authority.ConnectedClientCount, authority.ConfirmedTickCount, null)
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
logger.ZLogInformation(
    $"RaidBoss.Server 待ち受け開始 port={server.Port} game-port={gamePort} room={room}"
);

while (running)
{
    dispatcher.Pump();
    gameTransport.PollEvents();
    if (!time.IsFrozen)
    {
        time.OnFrame();
    }
    await Task.Delay(10);
}

gameTransport.Dispose();
logger.ZLogInformation($"RaidBoss.Server 終了");
