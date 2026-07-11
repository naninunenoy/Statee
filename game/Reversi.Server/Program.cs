using Microsoft.Extensions.Logging;
using Reversi.Server;
using Statee.Core;
using Statee.Remote;
using Syncee.LiteNetLib;
using Syncee.Statee;
using ZLogger;

// リバーシの権威サーバ(D-050)。純C#コンソールプロセスとして Reversi.Logic をそのまま権威状態にし、
// Statee を組み込んで権威 State(game/board・game/turn)を直接観測できるようにする。
// 対局クライアント(Reversi.Godot)は LiteNetLib で接続し着手を送る(N-4)。切断は対局中なら
// 相手の不戦勝として扱う(N-5)。Statee 側の place/start コマンドは運用・検証用の別経路として残す。
// 権威判定・座席割当・切断検知の中核は ReversiAuthority(テスト可能な形に切り出し済み)。

var port = 9310;
var gamePort = 9410;
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
var logger = loggerFactory.CreateLogger("Reversi.Server");

var boardState = new BoardState();
var turnState = new TurnState();

var gameTransport = new LiteNetLibServerTransport(gamePort);
var authority = new ReversiAuthority(gameTransport);

void RefreshState()
{
    boardState.Update(authority.Game.Board);
    turnState.Update(authority.Game);
}

RefreshState();
authority.Committed += envelope =>
{
    RefreshState();
    logger.ZLogInformation(
        $"確定 #{envelope.Sequence} {envelope.ClientId} {envelope.Command} → phase={authority.Game.Phase} turn={authority.Game.CurrentPlayer}"
    );
};

var dispatcher = new MainThreadDispatcher();
var host = new StateeHost(logBuffer) { MainThreadDispatcher = dispatcher };
host.RegisterStateProvider(boardState);
host.RegisterStateProvider(turnState);
host.RegisterStateProvider(
    new SyncStateProvider(
        "game/sync",
        () =>
            new SyncSnapshot(
                authority.ConnectedClientCount,
                authority.CommittedCount,
                authority.LastCommand
            )
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

object TurnResult() =>
    new
    {
        Phase = authority.Game.Phase.ToString(),
        CurrentPlayer = authority.Game.CurrentPlayer.ToString(),
        MoveCount = authority.Game.MoveCount,
        Winner = authority.Game.Winner.ToString(),
        EndReason = authority.Game.EndReason.ToString(),
    };

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
host.RegisterMainThreadCommand(
    "start",
    _ =>
    {
        if (!authority.TrySubmitLocal("start", null))
        {
            throw new InvalidOperationException("タイトル画面ではないので開始できない");
        }
        return TurnResult();
    }
);
host.RegisterMainThreadCommand(
    "place",
    cmdArgs =>
    {
        var x = cmdArgs.GetInt("x", -1);
        var y = cmdArgs.GetInt("y", -1);
        if (
            !authority.TrySubmitLocal(
                "place",
                new Dictionary<string, string> { ["x"] = x.ToString(), ["y"] = y.ToString() }
            )
        )
        {
            throw new InvalidOperationException(
                $"({x},{y}) は {authority.Game.CurrentPlayer} の合法手ではない(phase={authority.Game.Phase})"
            );
        }
        return TurnResult();
    }
);

await using var server = new StateeTcpServer(host, port);
server.Start();
logger.ZLogInformation($"Reversi.Server 待ち受け開始 port={server.Port} game-port={gamePort}");

while (running)
{
    dispatcher.Pump();
    gameTransport.PollEvents();
    await Task.Delay(10);
}

gameTransport.Dispose();
logger.ZLogInformation($"Reversi.Server 終了");
