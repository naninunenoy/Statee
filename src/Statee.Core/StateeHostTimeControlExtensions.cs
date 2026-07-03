namespace Statee.Core;

/// <summary>
/// TimeControl を標準コマンド(pause / resume / step)として StateeHost に登録する。
/// step は指定フレームの進行が完了してから応答を返す。
/// </summary>
public static class StateeHostTimeControlExtensions
{
    public static void RegisterTimeControl(
        this StateeHost host,
        TimeControl timeControl,
        TimeSpan? stepTimeout = null
    ) { }
}
