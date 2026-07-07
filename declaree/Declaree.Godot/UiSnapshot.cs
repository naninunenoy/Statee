using GdControl = Godot.Control;

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
    /// Render は IR の子1つにつき Control の子1つを同順で生成するため、構造は一致する。
    /// </summary>
    public static UiDescriptor Capture(UiNode node, GdControl rendered) =>
        Attach(UiTree.Describe(node), rendered);

    private static UiDescriptor Attach(UiDescriptor descriptor, GdControl control)
    {
        var children = new UiDescriptor[descriptor.Children.Count];
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = Attach(descriptor.Children[i], control.GetChild<GdControl>(i));
        }

        var rect = control.GetGlobalRect();
        return descriptor with
        {
            Children = children,
            Rect = new UiRect(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y),
        };
    }
}
