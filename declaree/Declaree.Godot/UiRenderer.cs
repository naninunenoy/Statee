using System;
using GdButton = Godot.Button;
using GdControl = Godot.Control;
using GdLabel = Godot.Label;

namespace Declaree.Godot;

/// <summary>
/// UiNode ツリーを Godot の Control ノードに変換する。
/// 全破棄・全再構築方式(D-035)。差分適用は必要が証明されてから導入する。
/// </summary>
public static class UiRenderer
{
    /// <summary>
    /// ツリーから Control を生成する。Button 押下時は <paramref name="dispatch"/> に
    /// イベント ID(<see cref="Declaree.Button.OnClick"/>)が渡る。
    /// 返されたノードの解放は呼び出し側の責任(再構築時は QueueFree)。
    /// </summary>
    public static GdControl Render(UiNode node, Action<string> dispatch)
    {
        var control = CreateControl(node, dispatch);
        control.Visible = node.Visible;
        if (node.MinWidth is not null || node.MinHeight is not null)
        {
            control.CustomMinimumSize = new global::Godot.Vector2(
                node.MinWidth ?? 0,
                node.MinHeight ?? 0
            );
        }
        return control;
    }

    private static GdControl CreateControl(UiNode node, Action<string> dispatch)
    {
        switch (node)
        {
            case VBox vbox:
                return RenderContainer(new global::Godot.VBoxContainer(), vbox.Children, dispatch);
            case HBox hbox:
                return RenderContainer(new global::Godot.HBoxContainer(), hbox.Children, dispatch);
            case Margin margin:
            {
                var container = new global::Godot.MarginContainer();
                container.AddThemeConstantOverride("margin_left", margin.All);
                container.AddThemeConstantOverride("margin_top", margin.All);
                container.AddThemeConstantOverride("margin_right", margin.All);
                container.AddThemeConstantOverride("margin_bottom", margin.All);
                container.AddChild(Render(margin.Child, dispatch));
                return container;
            }
            case Label label:
                return new GdLabel { Text = label.Text };
            case Button button:
            {
                var gdButton = new GdButton { Text = button.Text, Disabled = button.Disabled };
                var eventId = button.OnClick;
                gdButton.Pressed += () => dispatch(eventId);
                return gdButton;
            }
            default:
                throw new ArgumentException($"未知のノード型: {node.GetType().Name}", nameof(node));
        }
    }

    private static GdControl RenderContainer(
        global::Godot.Container container,
        UiNode[] children,
        Action<string> dispatch
    )
    {
        foreach (var child in children)
        {
            container.AddChild(Render(child, dispatch));
        }
        return container;
    }
}
