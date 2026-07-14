using Shouldly;

namespace Declaree.Tests;

public class AutoFocusTest
{
    [Fact]
    public void Describe_AutoFocus指定あり_autoFocusがtrueで変換される()
    {
        var descriptor = UiTree.Describe(new Button("OK", OnClick: "Ok") { AutoFocus = true });

        descriptor.Props["autoFocus"].ShouldBe("true");
    }

    [Fact]
    public void Describe_AutoFocus指定なし_autoFocusは現れない()
    {
        var descriptor = UiTree.Describe(new Button("OK", OnClick: "Ok"));

        descriptor.Props.ShouldNotContainKey("autoFocus");
    }
}
