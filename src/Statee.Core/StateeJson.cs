using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statee.Core;

/// <summary>ワイヤ(1行 JSON)と DTO の相互変換(docs/MEMO.md D-018)。Remote と CLI が共用する。</summary>
public static class StateeJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(StateeRequest request) =>
        JsonSerializer.Serialize(request, Options);

    public static string Serialize(StateeResponse response) =>
        JsonSerializer.Serialize(response, Options);

    public static StateeRequest? DeserializeRequest(string json) =>
        Deserialize<StateeRequest>(json);

    public static StateeResponse? DeserializeResponse(string json) =>
        Deserialize<StateeResponse>(json);

    private static T? Deserialize<T>(string json)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
