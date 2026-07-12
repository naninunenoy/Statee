using RaidBoss.Logic;
using Syncee;

namespace RaidBoss.Server;

/// <summary>
/// 権威サーバの中核(Statee 非依存。テスト可能な形にするため Program.cs から切り出した)。
/// D-054 のロックステップ方式に従い、物理・当たり判定は持たず、両クライアントの
/// Tick 入力が揃うまで待って確定・配布する(<see cref="TickBundleAuthority"/>)。
/// 確定したバンドルは同じ <see cref="RaidBoss.Logic.GameLogic"/> にサーバ側でも
/// 適用し、権威 State として観測できるようにする。
/// 接続管理・ブロードキャストは <see cref="ClientRegistry"/>(D-055)へ委譲する。
/// </summary>
public sealed class RaidBossAuthority
{
    // 開始前は人数未確定のため、誤って確定しないよう到達不能な値にしておく
    private readonly TickBundleAuthority _authority = new(expectedClientCount: int.MaxValue);
    private readonly ClientRegistry _registry;

    public RaidBossAuthority(IServerTransport transport, int seed = 12345)
    {
        Game = new GameLogic(seed);
        _registry = new ClientRegistry(transport);
        _authority.Committed += bundle =>
        {
            Apply(bundle);
            Committed?.Invoke(bundle);
            _registry.Broadcast(SyncWire.Serialize(bundle));
        };
        _registry.Received += OnReceived;
    }

    public GameLogic Game { get; }

    /// <summary>確定バンドルが1件配布されるたびに発火する(Program.cs の State 更新・ログ用)。</summary>
    public event Action<TickBundle>? Committed;

    public int ConnectedClientCount => _registry.ConnectedClientCount;
    public long ConfirmedTickCount => _authority.Entries.Count;

    private void OnReceived(string clientId, byte[] bytes)
    {
        var request = SyncWire.DeserializeRequest(bytes);
        switch (request.Command)
        {
            case "start":
                TryStart();
                return;
            case "input":
                OnInput(clientId, request.Args);
                return;
        }
    }

    /// <summary>
    /// ロビー待機中(Waiting)に接続中の人数(1〜4。1人でも開始できる。D-057)で開始する。
    /// 部屋作成者からの start コマンドで呼ばれる(誰が押しても同じ結果になる冪等な操作)。
    /// </summary>
    private void TryStart()
    {
        if (Game.Phase != GamePhase.Waiting || ConnectedClientCount < GameLogic.MinPlayerCount)
        {
            return;
        }
        Game.Start(ConnectedClientCount);
        _authority.SetExpectedClientCount(ConnectedClientCount);
    }

    private void OnInput(string clientId, IReadOnlyDictionary<string, string>? args)
    {
        if (
            args is null
            || !args.TryGetValue("tick", out var tickText)
            || !int.TryParse(tickText, out var tick)
        )
        {
            return;
        }
        _authority.Submit(tick, clientId, args);
    }

    private void Apply(TickBundle bundle)
    {
        var actions = Enumerable
            .Range(1, Game.PlayerCount)
            .Select(n => ParseAction(bundle.InputsByClient.GetValueOrDefault($"client-{n}")))
            .ToArray();
        Game.Step(actions);
    }

    private static PlayerAction ParseAction(IReadOnlyDictionary<string, string>? args) =>
        args is not null
        && args.TryGetValue("action", out var action)
        && action.Equals("attack", StringComparison.OrdinalIgnoreCase)
            ? PlayerAction.Attack
            : PlayerAction.Idle;
}
