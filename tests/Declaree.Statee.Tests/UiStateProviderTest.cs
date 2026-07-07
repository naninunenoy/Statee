using Cysharp.AI;
using Shouldly;

namespace Declaree.Statee.Tests;

public class UiStateProviderTest
{
    [Fact]
    public void Path_登録したパスを返す()
    {
        var provider = new UiStateProvider("game/ui-tree", () => new VBox());

        provider.Path.ShouldBe("game/ui-tree");
    }

    [Fact]
    public void CaptureState_現在のツリーの記述子を返す()
    {
        var provider = new UiStateProvider("game/ui-tree", () => new Label("Score: 0"));

        var state = provider.CaptureState();

        var descriptor = state.ShouldBeOfType<UiDescriptor>();
        descriptor.Type.ShouldBe("Label");
        descriptor.Props["text"].ShouldBe("Score: 0");
    }

    [Fact]
    public void CaptureState_ツリー差し替え後_最新のツリーを反映する()
    {
        UiNode tree = new Label("Score: 0");
        var provider = new UiStateProvider("game/ui-tree", () => tree);
        provider.CaptureState();

        tree = new Label("Score: 120");

        var descriptor = (UiDescriptor)provider.CaptureState();
        descriptor.Props["text"].ShouldBe("Score: 120");
    }

    [Fact]
    public void CaptureState_戻り値はTOONエンコードできる()
    {
        var provider = new UiStateProvider(
            "game/ui-tree",
            () => new VBox(new Label("Score: 0"), new Button("Restart", OnClick: "game/restart"))
        );

        var encoded = ToonEncoder.Encode(provider.CaptureState());

        encoded.ShouldNotBeNullOrWhiteSpace();
        encoded.ShouldContain("Restart");
    }
}
