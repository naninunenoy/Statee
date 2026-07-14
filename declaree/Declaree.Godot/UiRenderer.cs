using System;
using System.Collections.Generic;
using GdButton = Godot.Button;
using GdControl = Godot.Control;
using GdLabel = Godot.Label;

namespace Declaree.Godot;

/// <summary>
/// UiNode ツリーを Godot の Control ノードに変換する。
/// 初回は全構築(<see cref="Render"/>)、更新は差分適用(<see cref="Reconcile"/>)。
/// 全破棄・全再構築(D-035)は Slider のドラッグ破壊と LineEdit の入力消失で
/// 限界が証明されたため、パッチ可能なノードは破棄せずプロパティだけ更新する(D-061)。
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
                // ドラッグ中もライブで通知する。更新側は Reconcile(D-061)がスライダーを
                // 破棄せずパッチするため、ドラッグは途切れない(パッチは SetValueNoSignal を
                // 使うので通知ループにもならない)
                gdSlider.ValueChanged += _ => dispatch(eventId);
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

    /// <summary>
    /// 描画済みツリー <paramref name="current"/>(<paramref name="previous"/> を Render したもの)を
    /// <paramref name="next"/> の内容へ更新する。パッチ可能(UiDiff.CanPatch かつ子の個数一致)なら
    /// Control を破棄せずプロパティだけ更新し、不可能なサブツリーだけ作り直す。
    /// 返り値は更新後の Control(作り直された場合は新しいインスタンス)。
    /// </summary>
    public static GdControl Reconcile(
        GdControl current,
        UiNode previous,
        UiNode next,
        Action<string> dispatch
    )
    {
        var prevChildren = ChildrenOf(previous);
        var nextChildren = ChildrenOf(next);
        if (!UiDiff.CanPatch(previous, next) || prevChildren.Count != nextChildren.Count)
        {
            return Rebuild(current, next, dispatch);
        }
        Patch(current, previous, next);
        for (var i = 0; i < nextChildren.Count; i++)
        {
            Reconcile(current.GetChild<GdControl>(i), prevChildren[i], nextChildren[i], dispatch);
        }
        return current;
    }

    /// <summary>サブツリーを作り直し、親の同じ位置へ差し込む。アンカーは旧 Control から引き継ぐ。</summary>
    private static GdControl Rebuild(GdControl current, UiNode next, Action<string> dispatch)
    {
        var parent = current.GetParent();
        var index = current.GetIndex();
        var rebuilt = Render(next, dispatch);
        // Stack/Overlay 直下の全面アンカー等、親が Render 時に設定したレイアウトを保つ
        rebuilt.AnchorLeft = current.AnchorLeft;
        rebuilt.AnchorTop = current.AnchorTop;
        rebuilt.AnchorRight = current.AnchorRight;
        rebuilt.AnchorBottom = current.AnchorBottom;
        rebuilt.OffsetLeft = current.OffsetLeft;
        rebuilt.OffsetTop = current.OffsetTop;
        rebuilt.OffsetRight = current.OffsetRight;
        rebuilt.OffsetBottom = current.OffsetBottom;
        parent.RemoveChild(current);
        current.QueueFree();
        parent.AddChild(rebuilt);
        parent.MoveChild(rebuilt, index);
        return rebuilt;
    }

    /// <summary>型・Name・イベント ID が一致するノードのプロパティ差分を Control へ反映する。</summary>
    private static void Patch(GdControl control, UiNode previous, UiNode next)
    {
        control.Visible = next.Visible;
        if (previous.MinWidth != next.MinWidth || previous.MinHeight != next.MinHeight)
        {
            control.CustomMinimumSize = new global::Godot.Vector2(
                next.MinWidth ?? 0,
                next.MinHeight ?? 0
            );
        }
        if (previous.FontSize != next.FontSize)
        {
            if (next.FontSize is { } fontSize)
            {
                control.AddThemeFontSizeOverride("font_size", fontSize);
            }
            else
            {
                control.RemoveThemeFontSizeOverride("font_size");
            }
        }

        switch (next)
        {
            case Label label:
                ((GdLabel)control).Text = label.Text;
                break;
            case Button button:
                var gdButton = (GdButton)control;
                gdButton.Text = button.Text;
                gdButton.Disabled = button.Disabled;
                break;
            case CheckBox checkBox:
                var gdCheckBox = (global::Godot.CheckBox)control;
                gdCheckBox.Text = checkBox.Text;
                // Toggled を発火させず宣言状態へ合わせる
                gdCheckBox.SetPressedNoSignal(checkBox.Checked);
                break;
            case LineEdit lineEdit:
                var gdLineEdit = (global::Godot.LineEdit)control;
                gdLineEdit.PlaceholderText = lineEdit.PlaceholderText;
                // 宣言が変わったときだけ上書きする(入力中のユーザーテキストを保護する)
                if (((LineEdit)previous).Text != lineEdit.Text)
                {
                    gdLineEdit.Text = lineEdit.Text;
                }
                break;
            case Slider slider:
                var gdSlider = (global::Godot.HSlider)control;
                gdSlider.MinValue = slider.Min;
                gdSlider.MaxValue = slider.Max;
                gdSlider.Step = slider.Step;
                // ValueChanged(dispatch)を発火させず宣言値へ合わせる。
                // ドラッグ中はホストがスライダー値をロジックへ写した直後なので実質 no-op
                if (((Slider)previous).Value != slider.Value)
                {
                    gdSlider.SetValueNoSignal(slider.Value);
                }
                break;
            case Margin margin when ((Margin)previous).All != margin.All:
                control.AddThemeConstantOverride("margin_left", margin.All);
                control.AddThemeConstantOverride("margin_top", margin.All);
                control.AddThemeConstantOverride("margin_right", margin.All);
                control.AddThemeConstantOverride("margin_bottom", margin.All);
                break;
        }
    }

    /// <summary>IR ノードの子(Render が同順で Control の子にするもの)。</summary>
    private static IReadOnlyList<UiNode> ChildrenOf(UiNode node) =>
        node switch
        {
            VBox vbox => vbox.Children,
            HBox hbox => hbox.Children,
            Stack stack => stack.Children,
            ReorderList reorderList => reorderList.Children,
            Center center => [center.Child],
            Margin margin => [margin.Child],
            Overlay overlay => [overlay.Child],
            _ => [],
        };

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
