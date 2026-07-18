using System.Buffers.Binary;
using System.IO.Compression;
using Shouldly;

namespace Dotee.Tests;

public class PngEncoderTest
{
    [Fact]
    public void PNGシグネチャとIHDRの寸法が正しい()
    {
        var png = PngEncoder.Encode(3, 2, (_, _) => new Rgba(1, 2, 3, 4));

        png[..8].ShouldBe([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        // 最初のチャンクは IHDR(長さ 4 + 型 4 の後にデータ)
        System.Text.Encoding.ASCII.GetString(png[12..16]).ShouldBe("IHDR");
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)).ShouldBe(3);
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)).ShouldBe(2);
    }

    [Fact]
    public void zlibストリームを展開すると走査線データに戻る()
    {
        var png = PngEncoder.Encode(
            2,
            1,
            (x, _) => x == 0 ? new Rgba(10, 20, 30, 40) : new Rgba(50, 60, 70, 80)
        );

        var idat = FindChunk(png, "IDAT");
        using var zlib = new ZLibStream(new MemoryStream(idat), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        // フィルタ 0 + RGBA×2
        raw.ToArray().ShouldBe([0, 10, 20, 30, 40, 50, 60, 70, 80]);
    }

    [Fact]
    public void 大きな画像でもストアドブロック分割で展開できる()
    {
        // 1 走査線 = 1 + 128*4 = 513 バイト、全体 65 KiB 超で複数ブロックになる
        var png = PngEncoder.Encode(128, 140, (x, y) => new Rgba((byte)x, (byte)y, 0, 255));

        var idat = FindChunk(png, "IDAT");
        using var zlib = new ZLibStream(new MemoryStream(idat), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        raw.Length.ShouldBe((1 + 128 * 4) * 140);
    }

    /// <summary>PNG バイト列から指定チャンクのデータ部を取り出す。</summary>
    private static byte[] FindChunk(byte[] png, string type)
    {
        var pos = 8;
        while (pos < png.Length)
        {
            var len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            var t = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            if (t == type)
            {
                return png[(pos + 8)..(pos + 8 + len)];
            }
            pos += 12 + len;
        }
        throw new InvalidOperationException($"チャンクが見つかりません: {type}");
    }
}
