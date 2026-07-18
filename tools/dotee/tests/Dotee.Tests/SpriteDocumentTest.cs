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
