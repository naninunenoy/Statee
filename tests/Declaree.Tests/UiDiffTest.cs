using Shouldly;

namespace Declaree.Tests;

public class UiDiffTest
{
    [Fact]
    public void CanPatch_同型でテキストだけ違うLabel_trueを返す()
    {
        UiDiff
            .CanPatch(new Label("before") { Name = "A" }, new Label("after") { Name = "A" })
            .ShouldBeTrue();
    }

    [Fact]
    public void CanPatch_型が違う_falseを返す()
    {
        UiDiff.CanPatch(new Label("x"), new Button("x", OnClick: "Ev")).ShouldBeFalse();
    }

    [Fact]
    public void CanPatch_Nameが違う_falseを返す()
    {
        UiDiff
            .CanPatch(new Label("x") { Name = "A" }, new Label("x") { Name = "B" })
            .ShouldBeFalse();
    }

    [Fact]
    public void CanPatch_Nameなし同士_trueを返す()
    {
        UiDiff.CanPatch(new Label("x"), new Label("y")).ShouldBeTrue();
    }

    [Fact]
    public void CanPatch_ButtonのOnClickが違う_falseを返す()
    {
        UiDiff
            .CanPatch(new Button("x", OnClick: "A"), new Button("x", OnClick: "B"))
            .ShouldBeFalse();
    }

    [Fact]
    public void CanPatch_ButtonのTextとDisabledだけ違う_trueを返す()
    {
        UiDiff
            .CanPatch(
                new Button("x", OnClick: "A"),
                new Button("y", OnClick: "A") { Disabled = true }
            )
            .ShouldBeTrue();
    }

    [Fact]
    public void CanPatch_CheckBoxのOnToggleが違う_falseを返す()
    {
        UiDiff
            .CanPatch(new CheckBox("x", OnToggle: "A"), new CheckBox("x", OnToggle: "B"))
            .ShouldBeFalse();
    }

    [Fact]
    public void CanPatch_CheckBoxのCheckedだけ違う_trueを返す()
    {
        UiDiff
            .CanPatch(
                new CheckBox("x", OnToggle: "A"),
                new CheckBox("x", OnToggle: "A") { Checked = true }
            )
            .ShouldBeTrue();
    }

    [Fact]
    public void CanPatch_SliderのOnChangeが違う_falseを返す()
    {
        UiDiff
            .CanPatch(new Slider(0, 10, 5, OnChange: "A"), new Slider(0, 10, 5, OnChange: "B"))
            .ShouldBeFalse();
    }

    [Fact]
    public void CanPatch_SliderのValueだけ違う_trueを返す()
    {
        UiDiff
            .CanPatch(new Slider(0, 10, 5, OnChange: "A"), new Slider(0, 10, 7, OnChange: "A"))
            .ShouldBeTrue();
    }

    [Fact]
    public void CanPatch_ReorderListのOnReorderが違う_falseを返す()
    {
        UiDiff
            .CanPatch(new ReorderList("A", new Label("x")), new ReorderList("B", new Label("x")))
            .ShouldBeFalse();
    }

    [Fact]
    public void CanPatch_LineEditのTextだけ違う_trueを返す()
    {
        UiDiff.CanPatch(new LineEdit("a"), new LineEdit("b")).ShouldBeTrue();
    }

    [Fact]
    public void CanPatch_FontSizeだけ違う_trueを返す()
    {
        UiDiff
            .CanPatch(new Label("x") { FontSize = 16 }, new Label("x") { FontSize = 24 })
            .ShouldBeTrue();
    }
}
