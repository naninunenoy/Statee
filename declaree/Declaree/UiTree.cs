using System.Globalization;

namespace Declaree;

/// <summary>UiNode ツリーに対する操作。</summary>
public static class UiTree
{
    private static readonly IReadOnlyList<UiDescriptor> NoChildren = [];

    /// <summary>ツリーを <see cref="UiDescriptor"/> に変換する。Rect は実行時採取(UiSnapshot)の担当で、ここでは常に null。</summary>
    public static UiDescriptor Describe(UiNode node)
    {
        var props = new Dictionary<string, string>();
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

        return node switch
        {
            VBox vbox => new UiDescriptor("VBox", props, Describe(vbox.Children)),
            HBox hbox => new UiDescriptor("HBox", props, Describe(hbox.Children)),
            Margin margin => new UiDescriptor(
                "Margin",
                Add(props, "all", margin.All.ToString(CultureInfo.InvariantCulture)),
                [Describe(margin.Child)]
            ),
            Label label => new UiDescriptor("Label", Add(props, "text", label.Text), NoChildren),
            Button button => new UiDescriptor("Button", AddButtonProps(props, button), NoChildren),
            _ => throw new ArgumentException(
                $"未知のノード型: {node.GetType().Name}",
                nameof(node)
            ),
        };
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

    private static IReadOnlyList<UiDescriptor> Describe(IReadOnlyList<UiNode> children) =>
        [.. children.Select(Describe)];
}
