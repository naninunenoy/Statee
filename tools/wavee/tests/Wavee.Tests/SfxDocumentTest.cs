using Shouldly;
using Wavee;

namespace Wavee.Tests;

public class SfxDocumentTest
{
    private const string Full = """
        # 射撃音
        wave: square
        duration: 0.15
        freq: 1200        # 開始周波数
        freq-slide: -4000
        attack: 0.005
        decay: 0.12
        volume: 0.6
        duty: 0.3
        seed: 7
        sample-rate: 22050
        """;

    [Fact]
    public void Parse_全キー指定_すべて読み取れる()
    {
        var doc = SfxDocument.Parse(Full);

        doc.Wave.ShouldBe(WaveForm.Square);
        doc.Duration.ShouldBe(0.15f);
        doc.Freq.ShouldBe(1200f);
        doc.FreqSlide.ShouldBe(-4000f);
        doc.Attack.ShouldBe(0.005f);
        doc.Decay.ShouldBe(0.12f);
        doc.Volume.ShouldBe(0.6f);
        doc.Duty.ShouldBe(0.3f);
        doc.Seed.ShouldBe(7);
        doc.SampleRate.ShouldBe(22050);
    }

    [Fact]
    public void Parse_必須キーのみ_残りは既定値になる()
    {
        var doc = SfxDocument.Parse("wave: sine\nduration: 1");

        doc.Freq.ShouldBe(440f);
        doc.Volume.ShouldBe(0.5f);
        doc.SampleRate.ShouldBe(44100);
    }

    [Fact]
    public void Parse_waveがない_例外になる()
    {
        Should
            .Throw<FormatException>(() => SfxDocument.Parse("duration: 1"))
            .Message.ShouldContain("wave");
    }

    [Fact]
    public void Parse_未知のキー_例外になる()
    {
        Should
            .Throw<FormatException>(() =>
                SfxDocument.Parse("wave: sine\nduration: 1\nfrequency: 440")
            )
            .Message.ShouldContain("frequency");
    }

    [Fact]
    public void Parse_数値でない値_例外になる()
    {
        Should
            .Throw<FormatException>(() => SfxDocument.Parse("wave: sine\nduration: abc"))
            .Message.ShouldContain("duration");
    }

    [Fact]
    public void Parse_キー重複_行番号つきで例外になる()
    {
        Should
            .Throw<FormatException>(() => SfxDocument.Parse("wave: sine\nduration: 1\nduration: 2"))
            .Message.ShouldContain("3 行目");
    }

    [Fact]
    public void Parse_範囲外のvolume_例外になる()
    {
        Should.Throw<FormatException>(() =>
            SfxDocument.Parse("wave: sine\nduration: 1\nvolume: 1.5")
        );
    }
}
