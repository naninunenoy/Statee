namespace Declaree.Godot;

/// <summary>
/// 描画済みの Control ツリーから、レイアウト確定後の幾何(Rect)入りの
/// UiDescriptor を採取する。メインスレッドから呼ぶこと。
/// </summary>
public static class UiSnapshot
{
    /// <summary>
    /// <paramref name="node"/> と、それを Render した <paramref name="rendered"/> を
    /// 並行に辿り、各記述子に GetGlobalRect の結果を付与する。
    /// </summary>
    public static UiDescriptor Capture(UiNode node, global::Godot.Control rendered) => default!;
}
