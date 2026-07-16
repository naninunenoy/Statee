using System.Buffers.Binary;

namespace Dotee;

/// <summary>
/// 依存なしの最小 PNG エンコーダ(8bit RGBA・zlib 非圧縮ストアドブロック)。
/// ドット絵サイズ(数十ピクセル四方)前提で、圧縮率より単純さを優先する。
/// </summary>
public static class PngEncoder
{
    public static byte[] Encode(int width, int height, Func<int, int, Rgba> pixelAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        // 走査線: 先頭にフィルタ種別 0 を付けた RGBA の生データ
        var stride = 1 + width * 4;
        var raw = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            raw[row] = 0;
            for (var x = 0; x < width; x++)
            {
                var p = pixelAt(x, y);
                var o = row + 1 + x * 4;
                raw[o] = p.R;
                raw[o + 1] = p.G;
                raw[o + 2] = p.B;
                raw[o + 3] = p.A;
            }
        }

        using var ms = new MemoryStream();
        ms.Write(PngSignature);
        WriteChunk(ms, "IHDR", BuildIhdr(width, height));
        WriteChunk(ms, "IDAT", BuildZlibStored(raw));
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static byte[] BuildIhdr(int width, int height)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), height);
        data[8] = 8; // ビット深度
        data[9] = 6; // カラータイプ: RGBA
        return data; // 圧縮・フィルタ・インターレースは 0
    }

    /// <summary>zlib ストリーム(非圧縮ストアドブロック列 + Adler-32)を組み立てる。</summary>
    private static byte[] BuildZlibStored(byte[] raw)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); // CMF: deflate, 32KB window
        ms.WriteByte(0x01); // FLG: チェックサム調整のみ
        const int MaxBlock = 65535;
        for (var pos = 0; pos < raw.Length; pos += MaxBlock)
        {
            var len = Math.Min(MaxBlock, raw.Length - pos);
            var final = pos + len >= raw.Length;
            ms.WriteByte((byte)(final ? 1 : 0));
            ms.WriteByte((byte)len);
            ms.WriteByte((byte)(len >> 8));
            ms.WriteByte((byte)~len);
            ms.WriteByte((byte)(~len >> 8));
            ms.Write(raw, pos, len);
        }
        var adler = Adler32(raw);
        Span<byte> tail = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(tail, adler);
        ms.Write(tail);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        s.Write(crcBytes);
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint Mod = 65521;
        uint a = 1,
            b = 0;
        foreach (var t in data)
        {
            a = (a + t) % Mod;
            b = (b + a) % Mod;
        }
        return (b << 16) | a;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var t in type)
        {
            c = CrcTable[(c ^ t) & 0xFF] ^ (c >> 8);
        }
        foreach (var t in data)
        {
            c = CrcTable[(c ^ t) & 0xFF] ^ (c >> 8);
        }
        return c ^ 0xFFFFFFFFu;
    }
}
