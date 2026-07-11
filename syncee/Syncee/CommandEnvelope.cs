namespace Syncee;

/// <summary>
/// 確定コマンド1件(D-050「確定コマンドの順序付きログ」)。ワイヤ上は MemoryPack で
/// シリアライズする想定だが、コアはシリアライズ方式を知らない(N-0 時点では素の型)。
/// </summary>
public sealed record CommandEnvelope(
    long Sequence,
    string ClientId,
    string Command,
    IReadOnlyDictionary<string, string>? Args
);
