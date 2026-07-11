using System.Globalization;

namespace Declaree;

/// <summary>UiNode ツリーに対する操作。</summary>
public static class UiTree
{
    private static readonly IReadOnlyList<UiDescriptor> NoChildren = [];

    /// <summary>
    /// ツリーを <see cref="UiDescriptor"/> に変換する。Rect は実行時採取(UiSnapshot)の担当で、ここでは常に null。
    /// 全要素にツリー位置由来の安定 id(<see cref="UiNodeId"/>)を付与する。
    /// </summary>
    public static UiDescriptor Describe(UiNode node) => Describe(node, UiNodeId.Root);

    private static UiDescriptor Describe(UiNode node, UiNodeId id)
    {
        var props = new Dictionary<string, string> { ["id"] = id.AsPrimitive() };
        if (node.Name is { } name)
        {
            props["name"] = name;
        }
        if (!node.Visible)
        {
            props["visible"] = "false";
        }
        if (node.MinWidth is { } minWidth)
        {
            props["minWidth"] = minWidth.ToString(CultureInfo.InvariantCulture);
        }
        if (node.MinHeight is { } minHeight)
        {
            props["minHeight"] = minHeight.ToString(CultureInfo.InvariantCulture);
        }
        if (node.Explain is { } explain)
        {
            props["explain"] = explain;
        }

        return node switch
        {
            VBox vbox => new UiDescriptor("VBox", props, Describe(vbox.Children, id)),
            HBox hbox => new UiDescriptor("HBox", props, Describe(hbox.Children, id)),
            Center center => new UiDescriptor(
                "Center",
                props,
                [Describe(center.Child, id.Child(0))]
            ),
            Margin margin => new UiDescriptor(
                "Margin",
                Add(props, "all", margin.All.ToString(CultureInfo.InvariantCulture)),
                [Describe(margin.Child, id.Child(0))]
            ),
            Label label => new UiDescriptor("Label", Add(props, "text", label.Text), NoChildren),
            Button button => new UiDescriptor("Button", AddButtonProps(props, button), NoChildren),
            LineEdit lineEdit => new UiDescriptor(
                "LineEdit",
                AddLineEditProps(props, lineEdit),
                NoChildren
            ),
            _ => throw new ArgumentException(
                $"未知のノード型: {node.GetType().Name}",
                nameof(node)
            ),
        };
    }

    /// <summary>記述子ツリーから name が一致するノードを深さ優先で探す。見つからなければ null。</summary>
    public static UiDescriptor? FindByName(UiDescriptor descriptor, string name)
    {
        if (descriptor.Props.TryGetValue("name", out var value) && value == name)
        {
            return descriptor;
        }

        foreach (var child in descriptor.Children)
        {
            if (FindByName(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>記述子ツリーから id が一致するノードを深さ優先で探す。見つからなければ null。</summary>
    public static UiDescriptor? FindById(UiDescriptor descriptor, UiNodeId id)
    {
        if (descriptor.Props.TryGetValue("id", out var value) && value == id.AsPrimitive())
        {
            return descriptor;
        }

        foreach (var child in descriptor.Children)
        {
            if (FindById(child, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static Dictionary<string, string> Add(
        Dictionary<string, string> props,
        string key,
        string value
    )
    {
        props[key] = value;
        return props;
    }

    private static Dictionary<string, string> AddButtonProps(
        Dictionary<string, string> props,
        Button button
    )
    {
        props["text"] = button.Text;
        props["onClick"] = button.OnClick;
        if (button.Disabled)
        {
            props["disabled"] = "true";
        }
        return props;
    }

    private static Dictionary<string, string> AddLineEditProps(
        Dictionary<string, string> props,
        LineEdit lineEdit
    )
    {
        props["text"] = lineEdit.Text;
        props["placeholder"] = lineEdit.PlaceholderText;
        return props;
    }

    private static IReadOnlyList<UiDescriptor> Describe(
        IReadOnlyList<UiNode> children,
        UiNodeId parent
    ) => [.. children.Select((child, index) => Describe(child, parent.Child(index)))];
}
