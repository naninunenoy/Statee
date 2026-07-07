using Shouldly;

namespace Declaree.Tests;

public class UiTreeTest
{
    [Fact]
    public void Describe_Label_TypeとTextが変換される()
    {
        var descriptor = UiTree.Describe(new Label("Score: 120"));

        descriptor.Type.ShouldBe("Label");
        descriptor.Props.ShouldBe(new Dictionary<string, string> { ["text"] = "Score: 120" });
        descriptor.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Describe_Button_TypeとTextとOnClickが変換される()
    {
        var descriptor = UiTree.Describe(new Button("Restart", OnClick: "game/restart"));

        descriptor.Type.ShouldBe("Button");
        descriptor.Props.ShouldBe(
            new Dictionary<string, string> { ["text"] = "Restart", ["onClick"] = "game/restart" }
        );
        descriptor.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Describe_子を持つVBox_子が順序どおり変換される()
    {
        var descriptor = UiTree.Describe(new VBox(new Label("A"), new Label("B")));

        descriptor.Type.ShouldBe("VBox");
        descriptor.Props.ShouldBeEmpty();
        descriptor.Children.Select(c => c.Props["text"]).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Describe_子を持たないHBox_Childrenが空で変換される()
    {
        var descriptor = UiTree.Describe(new HBox());

        descriptor.Type.ShouldBe("HBox");
        descriptor.Props.ShouldBeEmpty();
        descriptor.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Describe_入れ子のコンテナ_深さを保って変換される()
    {
        var descriptor = UiTree.Describe(
            new VBox(new HBox(new Button("OK", OnClick: "dialog/ok")))
        );

        descriptor.Children[0].Type.ShouldBe("HBox");
        descriptor.Children[0].Children[0].Type.ShouldBe("Button");
    }
}
