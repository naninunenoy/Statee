namespace Declaree;

/// <summary>UiNode ツリーに対する操作。</summary>
public static class UiTree
{
    private static readonly IReadOnlyDictionary<string, string> NoProps =
        new Dictionary<string, string>();

    private static readonly IReadOnlyList<UiDescriptor> NoChildren = [];

    /// <summary>ツリーを <see cref="UiDescriptor"/> に変換する。</summary>
    public static UiDescriptor Describe(UiNode node) =>
        node switch
        {
            VBox vbox => new UiDescriptor("VBox", NoProps, Describe(vbox.Children)),
            HBox hbox => new UiDescriptor("HBox", NoProps, Describe(hbox.Children)),
            Label label => new UiDescriptor(
                "Label",
                new Dictionary<string, string> { ["text"] = label.Text },
                NoChildren
            ),
            Button button => new UiDescriptor(
                "Button",
                new Dictionary<string, string>
                {
                    ["text"] = button.Text,
                    ["onClick"] = button.OnClick,
                },
                NoChildren
            ),
            _ => throw new ArgumentException(
                $"未知のノード型: {node.GetType().Name}",
                nameof(node)
            ),
        };

    private static IReadOnlyList<UiDescriptor> Describe(IReadOnlyList<UiNode> children) =>
        [.. children.Select(Describe)];
}
