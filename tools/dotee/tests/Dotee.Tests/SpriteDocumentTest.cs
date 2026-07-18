using Shouldly;

namespace Dotee.Tests;

public class SpriteDocumentTest
{
    private const string Valid = """
        # コメント行
        palette:
        . = #00000000   # 透明
        H = #8F3049     # 髪
        grid:
        .H
        H.
        """;

    [Fact]
    public void 正常な定義をパースできる()
    {
        var doc = SpriteDocument.Parse(Valid);

        doc.Width.ShouldBe(2);
        doc.Height.ShouldBe(2);
        doc.PixelAt(0, 0).ShouldBe(new Rgba(0, 0, 0, 0));
        doc.PixelAt(1, 0).ShouldBe(new Rgba(0x8F, 0x30, 0x49, 0xFF));
    }

    [Fact]
    public void 六桁の色は不透明として読む()
    {
        var doc = SpriteDocument.Parse("palette:\nA = #102030\ngrid:\nA");

        doc.PixelAt(0, 0).ShouldBe(new Rgba(0x10, 0x20, 0x30, 0xFF));
    }

    [Fact]
    public void 八桁の色はアルファ付きで読む()
    {
        var doc = SpriteDocument.Parse("palette:\nA = #10203040\ngrid:\nA");

        doc.PixelAt(0, 0).ShouldBe(new Rgba(0x10, 0x20, 0x30, 0x40));
    }

    [Fact]
    public void パレットにない文字はエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\ngrid:\nAB")
        );

        e.Message.ShouldContain("'B'");
    }

    [Fact]
    public void 幅が揃わない行はエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\ngrid:\nAA\nA")
        );

        e.Message.ShouldContain("幅");
    }

    [Fact]
    public void パレット文字の重複はエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\nA = #405060\ngrid:\nA")
        );

        e.Message.ShouldContain("重複");
    }

    [Fact]
    public void 大文字と小文字は別のパレット文字として扱う()
    {
        var doc = SpriteDocument.Parse("palette:\nA = #102030\na = #405060\ngrid:\nAa");

        doc.PixelAt(0, 0).ShouldBe(new Rgba(0x10, 0x20, 0x30, 0xFF));
        doc.PixelAt(1, 0).ShouldBe(new Rgba(0x40, 0x50, 0x60, 0xFF));
    }

    [Fact]
    public void grid節がなければエラー()
    {
        Should.Throw<FormatException>(() => SpriteDocument.Parse("palette:\nA = #102030"));
    }

    private const string TwoFrames = """
        palette:
        A = #102030
        B = #405060
        frame: idle_0
        AA
        AB
        frame: idle_1
        BB
        BA
        """;

    [Fact]
    public void 複数フレームをパースできる()
    {
        var doc = SpriteDocument.Parse(TwoFrames);

        doc.Frames.Count.ShouldBe(2);
        doc.Frames[0].Name.ShouldBe("idle_0");
        doc.Frames[1].Name.ShouldBe("idle_1");
        doc.Width.ShouldBe(2);
        doc.Height.ShouldBe(2);
        doc.Frames[0].PixelAt(1, 1).ShouldBe(new Rgba(0x40, 0x50, 0x60, 0xFF));
        doc.Frames[1].PixelAt(0, 0).ShouldBe(new Rgba(0x40, 0x50, 0x60, 0xFF));
    }

    [Fact]
    public void 単一gridでもFramesは1件になる()
    {
        var doc = SpriteDocument.Parse(Valid);

        doc.Frames.Count.ShouldBe(1);
        doc.Frames[0].PixelAt(1, 0).ShouldBe(new Rgba(0x8F, 0x30, 0x49, 0xFF));
    }

    [Fact]
    public void シート座標はフレームを横に並べた色を返す()
    {
        var doc = SpriteDocument.Parse(TwoFrames);

        doc.SheetWidth.ShouldBe(4);
        // x=0..1 は idle_0、x=2..3 は idle_1
        doc.SheetPixelAt(1, 1).ShouldBe(new Rgba(0x40, 0x50, 0x60, 0xFF));
        doc.SheetPixelAt(2, 0).ShouldBe(new Rgba(0x40, 0x50, 0x60, 0xFF));
        doc.SheetPixelAt(3, 1).ShouldBe(new Rgba(0x10, 0x20, 0x30, 0xFF));
    }

    [Fact]
    public void gridとframeの混在はエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\ngrid:\nA\nframe: x\nA")
        );

        e.Message.ShouldContain("混在");
    }

    [Fact]
    public void フレームの寸法が揃わないとエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\nframe: a\nAA\nframe: b\nA")
        );

        e.Message.ShouldContain("寸法");
    }

    [Fact]
    public void フレーム名の重複はエラー()
    {
        var e = Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\nframe: a\nA\nframe: a\nA")
        );

        e.Message.ShouldContain("重複");
    }

    [Fact]
    public void フレーム名が空ならエラー()
    {
        Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\nframe:\nA")
        );
    }

    [Fact]
    public void ドット行のないフレームはエラー()
    {
        Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #102030\nframe: a\nframe: b\nA")
        );
    }

    [Fact]
    public void 色の形式が不正ならエラー()
    {
        Should.Throw<FormatException>(() => SpriteDocument.Parse("palette:\nA = 102030\ngrid:\nA"));
        Should.Throw<FormatException>(() => SpriteDocument.Parse("palette:\nA = #12345\ngrid:\nA"));
        Should.Throw<FormatException>(() =>
            SpriteDocument.Parse("palette:\nA = #GGGGGG\ngrid:\nA")
        );
    }
}
