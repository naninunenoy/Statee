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
    private readonly TickBundleAuthority _authority = new(expectedClientCount: 2);
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
        if (request.Command != "input")
        {
            return;
        }
        if (
            request.Args is null
            || !request.Args.TryGetValue("tick", out var tickText)
            || !int.TryParse(tickText, out var tick)
        )
        {
            return;
        }
        _authority.Submit(tick, clientId, request.Args);
    }

    private void Apply(TickBundle bundle)
    {
        var player1Action = ParseAction(bundle.InputsByClient.GetValueOrDefault("client-1"));
        var player2Action = ParseAction(bundle.InputsByClient.GetValueOrDefault("client-2"));
        Game.Step(player1Action, player2Action);
    }

    private static PlayerAction ParseAction(IReadOnlyDictionary<string, string>? args) =>
        args is not null
        && args.TryGetValue("action", out var action)
        && action.Equals("attack", StringComparison.OrdinalIgnoreCase)
            ? PlayerAction.Attack
            : PlayerAction.Idle;
}
