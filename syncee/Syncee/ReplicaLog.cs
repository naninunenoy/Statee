namespace Syncee;

/// <summary>
/// クライアント側のレプリカロール(D-050)。サーバから配布された確定コマンドを
/// 受信順に <paramref name="apply"/> で適用する。適用先(ドメインロジック)は
/// 呼び出し側が注入するため、このクラス自体は何のゲームかを知らない。
/// </summary>
public sealed class ReplicaLog(Action<CommandEnvelope> apply)
{
    private readonly Action<CommandEnvelope> _apply = apply;

    public IReadOnlyList<CommandEnvelope> Entries => [];

    /// <summary>サーバから確定コマンドを1件受信したときに呼ぶ。</summary>
    public void OnReceived(CommandEnvelope envelope) { }
}
