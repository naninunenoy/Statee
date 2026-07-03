namespace Statee.Core;

/// <summary>リクエストへの応答。ワイヤ上は1行 JSON。Payload は TOON 文字列(docs/MEMO.md D-018)。</summary>
public sealed record StateeResponse(
    string Id,
    string Status,
    string? Payload = null,
    string? Error = null
)
{
    public const string StatusOk = "ok";
    public const string StatusError = "error";

    public static StateeResponse Ok(string id, string payload) => new(id, StatusOk, payload);

    public static StateeResponse Fail(string id, string error) =>
        new(id, StatusError, Error: error);
}
