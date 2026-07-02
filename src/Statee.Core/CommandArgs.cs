namespace Statee.Core;

/// <summary>コマンド引数(文字列キー・値)への型付きアクセスを提供する。</summary>
public sealed class CommandArgs
{
    public CommandArgs(IReadOnlyDictionary<string, string>? values) { }

    public string? GetString(string name) => null;

    public int GetInt(string name, int defaultValue) => 0;
}
