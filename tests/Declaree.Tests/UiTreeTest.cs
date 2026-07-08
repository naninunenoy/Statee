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
    public void Describe_非表示のノード_visibleがfalseで変換される()
    {
        var descriptor = UiTree.Describe(new Label("hidden") { Visible = false });

        descriptor.Props["visible"].ShouldBe("false");
    }

    [Fact]
    public void Describe_最小サイズ指定のノード_minWidthとminHeightが変換される()
    {
        var descriptor = UiTree.Describe(new Label("sized") { MinWidth = 200, MinHeight = 48 });

        descriptor.Props["minWidth"].ShouldBe("200");
        descriptor.Props["minHeight"].ShouldBe("48");
    }

    [Fact]
    public void Describe_無効化したButton_disabledがtrueで変換される()
    {
        var descriptor = UiTree.Describe(new Button("Locked", OnClick: "noop") { Disabled = true });

        descriptor.Props["disabled"].ShouldBe("true");
    }

    [Fact]
    public void Describe_Margin_Typeとallと子が変換される()
    {
        var descriptor = UiTree.Describe(new Margin(16, new Label("inner")));

        descriptor.Type.ShouldBe("Margin");
        descriptor.Props.ShouldBe(new Dictionary<string, string> { ["all"] = "16" });
        descriptor.Children.Count.ShouldBe(1);
        descriptor.Children[0].Type.ShouldBe("Label");
    }

    [Fact]
    public void Describe_Center_Typeと子が変換される()
    {
        var descriptor = UiTree.Describe(new Center(new Label("inner")));

        descriptor.Type.ShouldBe("Center");
        descriptor.Props.ShouldBeEmpty();
        descriptor.Children.Count.ShouldBe(1);
        descriptor.Children[0].Type.ShouldBe("Label");
    }

    [Fact]
    public void Describe_Explain付きノード_explainが変換される()
    {
        var descriptor = UiTree.Describe(
            new Button("はじめる", OnClick: "StartGameCommand")
            {
                Explain = "ゲームを開始するボタン",
            }
        );

        descriptor.Props["explain"].ShouldBe("ゲームを開始するボタン");
    }

    [Fact]
    public void Describe_Name付きノード_nameが変換される()
    {
        var descriptor = UiTree.Describe(
            new Button("はじめる", OnClick: "StartGameCommand") { Name = "StartButton" }
        );

        descriptor.Props["name"].ShouldBe("StartButton");
    }

    [Fact]
    public void FindByName_入れ子の要素_深さを問わず見つかる()
    {
        var descriptor = UiTree.Describe(
            new VBox(
                new Label("title"),
                new HBox(new Button("OK", OnClick: "dialog/ok") { Name = "OkButton" })
            )
        );

        var found = UiTree.FindByName(descriptor, "OkButton");

        found.ShouldNotBeNull();
        found.Type.ShouldBe("Button");
        found.Props["text"].ShouldBe("OK");
    }

    [Fact]
    public void FindByName_ルート自身が一致_ルートを返す()
    {
        var descriptor = UiTree.Describe(new VBox() { Name = "Root" });

        UiTree.FindByName(descriptor, "Root").ShouldBe(descriptor);
    }

    [Fact]
    public void FindByName_存在しない名前_nullを返す()
    {
        var descriptor = UiTree.Describe(new VBox(new Label("A") { Name = "TitleLabel" }));

        UiTree.FindByName(descriptor, "Missing").ShouldBeNull();
    }

    [Fact]
    public void Describe_RectはIRからは常にnull()
    {
        var descriptor = UiTree.Describe(new Label("A"));

        descriptor.Rect.ShouldBeNull();
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
