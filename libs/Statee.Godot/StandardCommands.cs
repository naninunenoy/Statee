using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;

namespace Statee.Godot;

/// <summary>
/// どのゲームにも共通の標準コマンド(ping / key / screenshot / quit)を一括登録する。
/// ping は組み込みではないため、疎通確認の起点としてここで必ず登録する。
/// Web では TCP 入口が無いので実装を載せない(D-084)。
/// </summary>
public static partial class StandardCommands
{
    public static void Register(StateeHost host, Node node, ILogger logger) =>
        RegisterCore(host, node, logger);

    static partial void RegisterCore(StateeHost host, Node node, ILogger logger);
}
