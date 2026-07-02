namespace Statee.Core;

/// <summary>外部クライアントからの1リクエスト。ワイヤ上は1行 JSON(docs/MEMO.md D-018)。</summary>
public sealed record StateeRequest(
    string Id,
    string Command,
    Dictionary<string, string>? Args = null
);
