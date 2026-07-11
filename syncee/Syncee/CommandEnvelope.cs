using MemoryPack;

namespace Syncee;

/// <summary>
/// 確定コマンド1件(D-050「確定コマンドの順序付きログ」)。サーバから全クライアントへ
/// 配布するワイヤ形式そのもの(MemoryPack でシリアライズする)。
/// </summary>
[MemoryPackable]
public sealed partial record CommandEnvelope(
    long Sequence,
    string ClientId,
    string Command,
    IReadOnlyDictionary<string, string>? Args
);
