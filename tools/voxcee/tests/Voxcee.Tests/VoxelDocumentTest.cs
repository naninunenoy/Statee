using Shouldly;

namespace Voxcee.Tests;

public class VoxelDocumentTest
{
    private const string Valid = """
        palette:
        . = #00000000
        R = #FF0000
        layers:
        .R
        R.

        R.
        .R
        """;

    [Fact]
    public void 正常な定義をパースできる()
    {
        var doc = VoxelDocument.Parse(Valid);

        doc.Layers.Count.ShouldBe(2);
        doc.Layers[0][0].ShouldBe(".R");
        doc.Layers[1][1].ShouldBe(".R");
    }

    [Fact]
    public void ToModel_固体ボクセルだけを残す()
    {
        var model = VoxelDocument.Parse(Valid).ToModel();

        model.VoxelCount.ShouldBe(4);
        model.IsSolid(1, 0, 0).ShouldBeTrue();
        model.ColorAt(1, 0, 0).ShouldBe(new Rgba(0xFF, 0, 0, 0xFF));
    }

    [Fact]
    public void パレットにない文字はエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            VoxelDocument.Parse("palette:\nR = #FF0000\nlayers:\nRX")
        );

        e.Message.ShouldContain("'X'");
    }

    [Fact]
    public void スライス寸法が揃わないとエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            VoxelDocument.Parse("palette:\nR = #FF0000\nlayers:\nR\n\nRR")
        );

        e.Message.ShouldContain("寸法");
    }

    [Fact]
    public void 固体ボクセルが無いとToModelはエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            VoxelDocument.Parse("palette:\n. = #00000000\nlayers:\n.").ToModel()
        );

        e.Message.ShouldContain("固体");
    }
}
