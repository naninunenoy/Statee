using Microsoft.Extensions.Logging;
using Reversi.Logic;
using Reversi.Server;
using Statee.Core;
using Statee.Remote;
using Syncee;
using ZLogger;

// リバーシの権威サーバ(D-050)。純C#コンソールプロセスとして Reversi.Logic をそのまま権威状態にし、
// Statee を組み込んで権威 State(game/board・game/turn)を直接観測できるようにする。
// N-2 時点ではクライアントは実トランスポート経由でなく Statee コマンド(place/start)を
// 直接叩く形で AuthorityLog を通す。実トランスポート(フェイク/LiteNetLib)は N-3/N-4 で繋ぐ。

var port = 9310;
foreach (var arg in args)
{
    if (arg.StartsWith("--port=", StringComparison.Ordinal) && int.TryParse(arg[7..], out var p))
    {
        port = p;
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
logger.ZLogInformation($"Reversi.Server 待ち受け開始 port={server.Port}");

while (running)
{
    dispatcher.Pump();
    await Task.Delay(10);
}

logger.ZLogInformation($"Reversi.Server 終了");
