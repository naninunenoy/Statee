using Microsoft.Extensions.Logging;
using Reversi.Logic;
using Reversi.Server;
using Statee.Core;
using Statee.Remote;
using Syncee;
using Syncee.LiteNetLib;
using Syncee.Statee;
using ZLogger;

// リバーシの権威サーバ(D-050)。純C#コンソールプロセスとして Reversi.Logic をそのまま権威状態にし、
// Statee を組み込んで権威 State(game/board・game/turn)を直接観測できるようにする。
// 対局クライアント(Reversi.Godot)は LiteNetLib で接続し着手を送る(N-4)。
// Statee 側の place/start コマンドは運用・検証用の別経路として残す(N-2 から継続)。

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

var game = new ReversiGame();
var boardState = new BoardState();
var turnState = new TurnState();

void RefreshState()
{
    boardState.Update(game.Board);
    turnState.Update(game);
}

RefreshState();

// サーバの合法性判定(D-050「サーバが合法性を検証」)。副作用は持たず、判定のみ行う。
bool Validate(string clientId, string command, IReadOnlyDictionary<string, string>? cmdArgs) =>
    command switch
    {
        "start" => game.Phase == GamePhase.Title,
        "place" => game.Phase == GamePhase.Playing
            && TryParseCell(cmdArgs, out var x, out var y)
            && game.Board.GetLegalMoves(game.CurrentPlayer).Contains((x, y)),
        _ => false,
    };

var authorityLog = new AuthorityLog(Validate);

// 接続中の対局クライアント(N-4)。ClientId は接続順に割り当てる。切断検知(N-5)はまだ行わず、
// 一覧から外すだけ
var clients = new Dictionary<ITransport, string>();
var nextClientNumber = 0;

authorityLog.Committed += envelope =>
{
    switch (envelope.Command)
    {
        case "start":
            game.Start(GameMode.Network);
            break;
        case "place":
            TryParseCell(envelope.Args, out var x, out var y);
            game.TryPlace(x, y);
            break;
    }
    RefreshState();
    logger.ZLogInformation(
        $"確定 #{envelope.Sequence} {envelope.ClientId} {envelope.Command} → phase={game.Phase} turn={game.CurrentPlayer}"
    );
    var payload = SyncWire.Serialize(envelope);
    foreach (var client in clients.Keys)
    {
        client.Send(payload);
    }
};

var gameTransport = new LiteNetLibServerTransport(gamePort);
gameTransport.ClientConnected += transport =>
{
    var clientId = $"client-{++nextClientNumber}";
    clients[transport] = clientId;
    transport.Received += bytes =>
    {
        var request = SyncWire.DeserializeRequest(bytes);
        if (!authorityLog.TrySubmit(clientId, request.Command, request.Args))
        {
            logger.ZLogWarning(
                $"{clientId} からの {request.Command} は拒否された(phase={game.Phase})"
            );
        }
    };
    transport.Disconnected += () =>
    {
        clients.Remove(transport);
        logger.ZLogInformation($"{clientId} が切断した");
    };
    logger.ZLogInformation($"{clientId} が接続した");
};

static bool TryParseCell(IReadOnlyDictionary<string, string>? args, out int x, out int y)
{
    x = 0;
    y = 0;
    return args is not null
        && args.TryGetValue("x", out var xs)
        && args.TryGetValue("y", out var ys)
        && int.TryParse(xs, out x)
        && int.TryParse(ys, out y);
}

var dispatcher = new MainThreadDispatcher();
var host = new StateeHost(logBuffer) { MainThreadDispatcher = dispatcher };
host.RegisterStateProvider(boardState);
host.RegisterStateProvider(turnState);
host.RegisterStateProvider(
    new SyncStateProvider(
        "game/sync",
        () =>
            new SyncSnapshot(
                clients.Count,
                authorityLog.Entries.Count,
                authorityLog.Entries.Count > 0 ? authorityLog.Entries[^1].Command : null
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
        Phase = game.Phase.ToString(),
        CurrentPlayer = game.CurrentPlayer.ToString(),
        MoveCount = game.MoveCount,
        Winner = game.Winner.ToString(),
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
    cmdArgs =>
    {
        if (!authorityLog.TrySubmit("local", "start", null))
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
        var clientId = cmdArgs.GetString("client") ?? "local";
        if (
            !authorityLog.TrySubmit(
                clientId,
                "place",
                new Dictionary<string, string> { ["x"] = x.ToString(), ["y"] = y.ToString() }
            )
        )
        {
            throw new InvalidOperationException(
                $"({x},{y}) は {game.CurrentPlayer} の合法手ではない(phase={game.Phase})"
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
