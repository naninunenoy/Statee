using System;
using Godot;

namespace Statee.Godot;

/// <summary>`--seed=123` 形式の起動引数(`--` 以降のユーザー引数)を読む。</summary>
public static class CmdlineArgs
{
    public static int ParseInt(string prefix, int defaultValue)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (
                arg.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(arg[prefix.Length..], out var value)
            )
            {
                return value;
            }
        }

        return defaultValue;
    }
}
