using System.Globalization;

namespace Statee.Core;

/// <summary>コマンド引数(文字列キー・値)への型付きアクセスを提供する。</summary>
public sealed class CommandArgs
{
    private readonly IReadOnlyDictionary<string, string>? _values;

    public CommandArgs(IReadOnlyDictionary<string, string>? values)
    {
        _values = values;
    }

    public string? GetString(string name) =>
        _values is not null && _values.TryGetValue(name, out var value) ? value : null;

    public int GetInt(string name, int defaultValue)
    {
        var value = GetString(name);
        return value is null ? defaultValue : int.Parse(value, CultureInfo.InvariantCulture);
    }
}
