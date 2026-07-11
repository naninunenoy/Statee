using Reversi.Logic;
using Syncee;

namespace Reversi.Server;

/// <summary>
/// 権威サーバの中核(Statee 非依存。テスト可能な形にするため Program.cs から切り出した)。
/// 接続順に座席(黒/白)を割り当て、着手要求の検証・確定・配布、および切断検知(D-050。
/// 対局中の切断は相手の不戦勝として Result へ遷移)を担う。
/// </summary>
public sealed class ReversiAuthority
{
    private readonly AuthorityLog _authorityLog;
    private readonly Dictionary<ITransport, string> _clients = [];
    private readonly Dictionary<string, Disc> _seats = [];
    private int _nextClientNumber;

    public ReversiAuthority(IServerTransport transport)
    {
        _authorityLog = new AuthorityLog(Validate);
        _authorityLog.Committed += envelope =>
        {
            Apply(envelope);
            Committed?.Invoke(envelope);
            Broadcast(envelope);
        };
        transport.ClientConnected += OnClientConnected;
    }

    public ReversiGame Game { get; } = new();

    /// <summary>確定コマンドが1件配布されるたびに発火する(Program.cs の State 更新・ログ用)。</summary>
    public event Action<CommandEnvelope>? Committed;

    public int ConnectedClientCount => _clients.Count;
    public long CommittedCount => _authorityLog.Entries.Count;
    public string? LastCommand =>
        _authorityLog.Entries.Count > 0 ? _authorityLog.Entries[^1].Command : null;

    /// <summary>Statee コマンド(運用・検証用)からの直接投入。実クライアントの投入と同じ経路を通る。</summary>
    public bool TrySubmitLocal(string command, IReadOnlyDictionary<string, string>? args) =>
        _authorityLog.TrySubmit("local", command, args);

    private void OnClientConnected(ITransport clientTransport)
    {
        var clientId = $"client-{++_nextClientNumber}";
        _clients[clientTransport] = clientId;
        if (_nextClientNumber == 1)
        {
            _seats[clientId] = Disc.Black;
        }
        else if (_nextClientNumber == 2)
        {
            _seats[clientId] = Disc.White;
        }

        clientTransport.Received += bytes =>
        {
            var request = SyncWire.DeserializeRequest(bytes);
            _authorityLog.TrySubmit(clientId, request.Command, request.Args);
        };
        clientTransport.Disconnected += () =>
        {
            _clients.Remove(clientTransport);
            if (Game.Phase == GamePhase.Playing && _seats.TryGetValue(clientId, out var seat))
            {
                _authorityLog.TrySubmit(
                    "server",
                    "disconnect",
                    new Dictionary<string, string> { ["seat"] = seat.ToString() }
                );
            }
        };
    }

    private bool Validate(
        string clientId,
        string command,
        IReadOnlyDictionary<string, string>? args
    ) =>
        command switch
        {
            "start" => Game.Phase == GamePhase.Title,
            "place" => Game.Phase == GamePhase.Playing
                && TryParseCell(args, out var x, out var y)
                && Game.Board.GetLegalMoves(Game.CurrentPlayer).Contains((x, y)),
            "disconnect" => Game.Phase == GamePhase.Playing,
            _ => false,
        };

    private void Apply(CommandEnvelope envelope)
    {
        switch (envelope.Command)
        {
            case "start":
                Game.Start(GameMode.Network);
                break;
            case "place":
                TryParseCell(envelope.Args, out var x, out var y);
                Game.TryPlace(x, y);
                break;
            case "disconnect":
                Game.EndByDisconnect(Enum.Parse<Disc>(envelope.Args!["seat"]));
                break;
        }
    }

    private void Broadcast(CommandEnvelope envelope)
    {
        var payload = SyncWire.Serialize(envelope);
        foreach (var client in _clients.Keys)
        {
            client.Send(payload);
        }
    }

    private static bool TryParseCell(
        IReadOnlyDictionary<string, string>? args,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;
        return args is not null
            && args.TryGetValue("x", out var xs)
            && args.TryGetValue("y", out var ys)
            && int.TryParse(xs, out x)
            && int.TryParse(ys, out y);
    }
}
