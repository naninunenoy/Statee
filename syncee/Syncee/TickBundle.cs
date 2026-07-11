namespace Syncee;

/// <summary>
/// 確定済みのTick(D-054)。そのTickに参加した全クライアント分の入力を、
/// クライアントID をキーに持つ。入力の意味(移動・攻撃等)はドメイン側の知識であり、
/// このレコード自体は関知しない(syncee/README.md の境界)。
/// </summary>
public sealed record TickBundle(
    int Tick,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>?> InputsByClient
);
