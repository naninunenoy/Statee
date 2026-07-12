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
        if (node.Name is { } name)
        {
            // シーンツリー上でも安定 ID として追跡できるようにする(GUIDELINE 3.4)
            control.Name = name;
        }
        control.Visible = node.Visible;
        if (node.MinWidth is not null || node.MinHeight is not null)
        {
            control.CustomMinimumSize = new global::Godot.Vector2(
                node.MinWidth ?? 0,
                node.MinHeight ?? 0
            );
        }
        if (node.FontSize is { } fontSize)
        {
            control.AddThemeFontSizeOverride("font_size", fontSize);
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
            case Center center:
            {
                var container = new global::Godot.CenterContainer();
                container.AddChild(Render(center.Child, dispatch));
                return container;
            }
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
            case CheckBox checkBox:
            {
                var gdCheckBox = new global::Godot.CheckBox
                {
                    Text = checkBox.Text,
                    ButtonPressed = checkBox.Checked,
                };
                var eventId = checkBox.OnToggle;
                // チェック状態は宣言が正。押下は通知のみ行い、ホストが状態を変えて再構築する
                gdCheckBox.Pressed += () => dispatch(eventId);
                return gdCheckBox;
            }
            case Slider slider:
            {
                var gdSlider = new global::Godot.HSlider
                {
                    MinValue = slider.Min,
                    MaxValue = slider.Max,
                    Step = slider.Step,
                    Value = slider.Value,
                };
                var eventId = slider.OnChange;
                // ドラッグ中の ValueChanged で dispatch すると全再構築(D-035)がスライダー自身を
                // 破棄してドラッグが途切れるため、確定タイミング(ドラッグ終了・キー操作)のみ通知する
                var dragging = false;
                gdSlider.DragStarted += () => dragging = true;
                gdSlider.DragEnded += changed =>
                {
                    dragging = false;
                    if (changed)
                    {
                        dispatch(eventId);
                    }
                };
                gdSlider.ValueChanged += _ =>
                {
                    if (!dragging)
                    {
                        dispatch(eventId);
                    }
                };
                return gdSlider;
            }
            case Stack stack:
            {
                // 子を同じ領域に重ねる。コンテナではなく素の Control に全面アンカーで載せる
                var container = new GdControl();
                foreach (var child in stack.Children)
                {
                    var rendered = Render(child, dispatch);
                    container.AddChild(rendered);
                    rendered.SetAnchorsAndOffsetsPreset(GdControl.LayoutPreset.FullRect);
                }
                return container;
            }
            case Overlay overlay:
            {
                // 半透明の幕。MouseFilter.Stop が背面 UI へのマウス入力を遮断する
                var veil = new global::Godot.ColorRect
                {
                    Color = new global::Godot.Color(0f, 0f, 0f, 0.5f),
                    MouseFilter = GdControl.MouseFilterEnum.Stop,
                };
                var rendered = Render(overlay.Child, dispatch);
                veil.AddChild(rendered);
                rendered.SetAnchorsAndOffsetsPreset(GdControl.LayoutPreset.FullRect);
                return veil;
            }
            case ReorderList reorderList:
            {
                var container = new ReorderListContainer();
                foreach (var child in reorderList.Children)
                {
                    container.AddChild(Render(child, dispatch));
                }
                var eventId = reorderList.OnReorder;
                container.Reordered += (from, to) => dispatch($"{eventId}:{from}:{to}");
                return container;
            }
            case LineEdit lineEdit:
                // 値の読み出しはリアクティブにせず、ホスト側が Name(D-038)で
                // このコントロールを直接参照して Text を読む(D-035)
                return new global::Godot.LineEdit
                {
                    Text = lineEdit.Text,
                    PlaceholderText = lineEdit.PlaceholderText,
                };
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
