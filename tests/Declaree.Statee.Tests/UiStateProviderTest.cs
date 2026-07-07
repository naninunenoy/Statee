using Cysharp.AI;
using Shouldly;

namespace Declaree.Statee.Tests;

public class UiStateProviderTest
{
    [Fact]
    public void Path_登録したパスを返す()
    {
        var provider = new UiStateProvider("game/ui-tree", () => UiTree.Describe(new VBox()));

        provider.Path.ShouldBe("game/ui-tree");
    }

    [Fact]
    public void CaptureState_現在のスナップショットを返す()
    {
        var provider = new UiStateProvider(
            "game/ui-tree",
            () => UiTree.Describe(new Label("Score: 0"))
        );

        var state = provider.CaptureState();

        var descriptor = state.ShouldBeOfType<UiDescriptor>();
        descriptor.Type.ShouldBe("Label");
        descriptor.Props["text"].ShouldBe("Score: 0");
    }

    [Fact]
    public void CaptureState_スナップショット差し替え後_最新を反映する()
    {
        var snapshot = UiTree.Describe(new Label("Score: 0"));
        var provider = new UiStateProvider("game/ui-tree", () => snapshot);
        provider.CaptureState();

        snapshot = UiTree.Describe(new Label("Score: 120"));

        var descriptor = (UiDescriptor)provider.CaptureState();
        descriptor.Props["text"].ShouldBe("Score: 120");
    }

    [Fact]
    public void CaptureState_Rect付きスナップショットはTOONエンコードできる()
    {
        var snapshot = UiTree.Describe(
            new VBox(new Label("Score: 0"), new Button("Restart", OnClick: "game/restart"))
        ) with
        {
            Rect = new UiRect(0f, 0f, 320f, 96f),
        };
        var provider = new UiStateProvider("game/ui-tree", () => snapshot);

        var encoded = ToonEncoder.Encode(provider.CaptureState());

        encoded.ShouldNotBeNullOrWhiteSpace();
        encoded.ShouldContain("320");
    }
}
