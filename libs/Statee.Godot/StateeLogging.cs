using Microsoft.Extensions.Logging;
using Statee.Core;

namespace Statee.Godot;

/// <summary>
/// Statee の logs コマンド用バッファと、プラットフォーム向けコンソールへ流すロガーの定型構築。
/// PC は ZLogger、Web は GD.Print。返した ILoggerFactory はゲーム側が _ExitTree で Dispose する(D-083)。
/// </summary>
public static partial class StateeLogging
{
    public static ILoggerFactory CreateLoggerFactory(LogBuffer buffer) => CreateCore(buffer);

    private static partial ILoggerFactory CreateCore(LogBuffer buffer);
}
