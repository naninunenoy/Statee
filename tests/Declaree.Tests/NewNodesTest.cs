using Shouldly;

namespace Declaree.Tests;

public class NewNodesTest
{
    [Fact]
    public void Describe_CheckBox_TypeとTextとOnToggleが変換される()
    {
        var descriptor = UiTree.Describe(new CheckBox("牛乳を買う", OnToggle: "Toggle:1"));

        descriptor.Type.ShouldBe("CheckBox");
        descriptor.Props.ShouldBe(
            new Dictionary<string, string>
            {
                ["id"] = "0",
                ["text"] = "牛乳を買う",
                ["onToggle"] = "Toggle:1",
            }
        );
        descriptor.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Describe_チェック済みのCheckBox_checkedがtrueで変換される()
    {
        var descriptor = UiTree.Describe(
            new CheckBox("done", OnToggle: "Toggle:1") { Checked = true }
        );

        descriptor.Props["checked"].ShouldBe("true");
    }

    [Fact]
    public void Describe_Slider_TypeとMinMaxValueStepとOnChangeが変換される()
    {
        var descriptor = UiTree.Describe(
            new Slider(Min: 12, Max: 32, Value: 16.5, OnChange: "FontSizeChanged") { Step = 0.5 }
        );

        descriptor.Type.ShouldBe("Slider");
        descriptor.Props.ShouldBe(
            new Dictionary<string, string>
            {
                ["id"] = "0",
                ["min"] = "12",
                ["max"] = "32",
                ["value"] = "16.5",
                ["step"] = "0.5",
                ["onChange"] = "FontSizeChanged",
            }
        );
        descriptor.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Describe_Stack_子が順序どおり変換される()
    {
        var descriptor = UiTree.Describe(new Stack(new Label("base"), new Label("front")));

        descriptor.Type.ShouldBe("Stack");
        descriptor.Children.Select(c => c.Props["text"]).ShouldBe(["base", "front"]);
    }

    [Fact]
    public void Describe_Overlay_Typeと子が変換される()
    {
        var descriptor = UiTree.Describe(new Overlay(new Label("dialog")));

        descriptor.Type.ShouldBe("Overlay");
        descriptor.Children.Count.ShouldBe(1);
        descriptor.Children[0].Props["text"].ShouldBe("dialog");
    }

    [Fact]
    public void Describe_ReorderList_TypeとOnReorderと子が変換される()
    {
        var descriptor = UiTree.Describe(
            new ReorderList("Reorder", new Label("a"), new Label("b"))
        );

        descriptor.Type.ShouldBe("ReorderList");
        descriptor.Props["onReorder"].ShouldBe("Reorder");
        descriptor.Children.Select(c => c.Props["text"]).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Describe_ドラッグ中のReorderList_draggingIndexとdropIndexが変換される()
    {
        var descriptor = UiTree.Describe(
            new ReorderList("Reorder", new Label("a"), new Label("b"))
            {
                DraggingIndex = 0,
                DropIndex = 1,
            }
        );

        descriptor.Props["draggingIndex"].ShouldBe("0");
        descriptor.Props["dropIndex"].ShouldBe("1");
    }

    [Fact]
    public void Describe_ドラッグしていないReorderList_draggingIndexとdropIndexは現れない()
    {
        var descriptor = UiTree.Describe(new ReorderList("Reorder", new Label("a")));

        descriptor.Props.ShouldNotContainKey("draggingIndex");
        descriptor.Props.ShouldNotContainKey("dropIndex");
    }

    [Fact]
    public void Describe_FontSize付きノード_fontSizeが変換される()
    {
        var descriptor = UiTree.Describe(new Label("big") { FontSize = 24 });

        descriptor.Props["fontSize"].ShouldBe("24");
    }
}
