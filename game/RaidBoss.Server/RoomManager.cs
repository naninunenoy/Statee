using Syncee;

namespace RaidBoss.Server;

/// <summary>
/// 合言葉で複数の部屋(RaidBossAuthority)を1サーバプロセスで振り分けるロビー層(D-056)。
/// 生の接続は最初の1通(join/create コマンド)を受け取るまでどの部屋にも属さない。
/// join は既存の合言葉の部屋、create は新規の合言葉の部屋を要求する。
/// </summary>
public sealed class RoomManager
{
    private readonly Dictionary<string, Room> _rooms = [];
    private readonly int _seed;

    public RoomManager(IServerTransport rawTransport, int seed = 12345)
    {
        _seed = seed;
        rawTransport.ClientConnected += OnRawClientConnected;
    }

    /// <summary>合言葉ごとの部屋一覧(検証・State公開用)。</summary>
    public IReadOnlyDictionary<string, Room> Rooms => _rooms;

    /// <summary>直近作成/参加された部屋。State公開の簡易な対象選択に使う(D-056の既知の制約)。</summary>
    public Room? LastTouchedRoom { get; private set; }

    public sealed record Room(
        string Keyword,
        RaidBossAuthority Authority,
        ManualServerTransport Transport
    );

    private void OnRawClientConnected(ITransport transport)
    {
        void OnFirstMessage(byte[] bytes)
        {
            transport.Received -= OnFirstMessage;
            var request = SyncWire.DeserializeRequest(bytes);
            var keyword = request.Args?.GetValueOrDefault("room");
            if (string.IsNullOrEmpty(keyword))
            {
                transport.Disconnect();
                return;
            }

            switch (request.Command)
            {
                case "create":
                    if (_rooms.ContainsKey(keyword))
                    {
                        transport.Disconnect();
                        return;
                    }
                    var room = CreateRoom(keyword);
                    room.Transport.Accept(transport);
                    LastTouchedRoom = room;
                    return;
                case "join":
                    if (!_rooms.TryGetValue(keyword, out var existing))
                    {
                        transport.Disconnect();
                        return;
                    }
                    existing.Transport.Accept(transport);
                    LastTouchedRoom = existing;
                    return;
                default:
                    transport.Disconnect();
                    return;
            }
        }

        transport.Received += OnFirstMessage;
    }

    private Room CreateRoom(string keyword)
    {
        var roomTransport = new ManualServerTransport();
        var authority = new RaidBossAuthority(roomTransport, _seed);
        var room = new Room(keyword, authority, roomTransport);
        _rooms[keyword] = room;
        return room;
    }
}
