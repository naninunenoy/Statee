using Shouldly;
using Wavee;

namespace Wavee.Tests;

public class WavEncoderTest
{
    [Fact]
    public void Encode_ヘッダ44バイトとサンプル2バイトずつになる()
    {
        var bytes = WavEncoder.Encode(44100, new float[100]);

        bytes.Length.ShouldBe(44 + 200);
    }

    [Fact]
    public void Encode_RIFFとWAVEの識別子を持つ()
    {
        var bytes = WavEncoder.Encode(44100, new float[1]);

        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("RIFF");
        System.Text.Encoding.ASCII.GetString(bytes, 8, 4).ShouldBe("WAVE");
        System.Text.Encoding.ASCII.GetString(bytes, 36, 4).ShouldBe("data");
    }

    [Fact]
    public void Encode_サンプルレートがヘッダに入る()
    {
        var bytes = WavEncoder.Encode(22050, new float[1]);

        BitConverter.ToInt32(bytes, 24).ShouldBe(22050);
    }

    [Fact]
    public void Encode_サンプル値が16bitPCMへ往復できる()
    {
        var bytes = WavEncoder.Encode(44100, [1f, -1f, 0f]);

        BitConverter.ToInt16(bytes, 44).ShouldBe(short.MaxValue);
        BitConverter.ToInt16(bytes, 46).ShouldBe((short)-short.MaxValue);
        BitConverter.ToInt16(bytes, 48).ShouldBe((short)0);
    }

    [Fact]
    public void Encode_範囲外のサンプルはクリップされる()
    {
        var bytes = WavEncoder.Encode(44100, [2f]);

        BitConverter.ToInt16(bytes, 44).ShouldBe(short.MaxValue);
    }
}
