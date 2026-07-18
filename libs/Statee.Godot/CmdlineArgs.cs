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

    /// <summary>
    /// `--frozen` 形式のフラグ引数があるか。起動直後から freeze しておきたい場合
    /// (実時間で tick が進むと接続タイミングで盤面が変わるため。D-073)などに使う。
    /// </summary>
    public static bool HasFlag(string name)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (string.Equals(arg, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string ParseString(string prefix, string defaultValue)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
            {
                return arg[prefix.Length..];
            }
        }

        return defaultValue;
    }
}
