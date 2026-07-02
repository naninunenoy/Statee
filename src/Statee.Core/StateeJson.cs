namespace Statee.Core;

/// <summary>ワイヤ(1行 JSON)と DTO の相互変換(docs/MEMO.md D-018)。Remote と CLI が共用する。</summary>
public static class StateeJson
{
    public static string Serialize(StateeRequest request) => null!;

    public static string Serialize(StateeResponse response) => null!;

    public static StateeRequest? DeserializeRequest(string json) => null;

    public static StateeResponse? DeserializeResponse(string json) => null;
}
