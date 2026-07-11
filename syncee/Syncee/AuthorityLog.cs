namespace Syncee;

/// <summary>
/// サーバ側の権威ロール(D-050)。クライアントから送られたコマンドを
/// <paramref name="validate"/> で検証し、合法なら確定コマンドとして順序付きログへ積み、
/// <see cref="Committed"/> で配布側(トランスポート層)へ通知する。
/// ドメイン(リバーシ等)の合法性判定は呼び出し側が注入するため、このクラス自体は
/// 何のゲームかを知らない(syncee/README.md の境界)。
/// </summary>
public sealed class AuthorityLog(
    Func<string, string, IReadOnlyDictionary<string, string>?, bool> validate
)
{
    private readonly List<CommandEnvelope> _entries = [];

    public IReadOnlyList<CommandEnvelope> Entries => _entries;

    public event Action<CommandEnvelope>? Committed;

    /// <summary>クライアントからのコマンドを検証し、合法なら確定してログへ積む。戻り値は成否。</summary>
    public bool TrySubmit(
        string clientId,
        string command,
        IReadOnlyDictionary<string, string>? args
    )
    {
        if (!validate(clientId, command, args))
        {
            return false;
        }

        var envelope = new CommandEnvelope(_entries.Count, clientId, command, args);
        _entries.Add(envelope);
        Committed?.Invoke(envelope);
        return true;
    }
}
