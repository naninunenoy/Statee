namespace Wavee;

/// <summary>
/// モノラル float サンプル列を 16bit PCM の WAV(RIFF)バイト列にする。
/// 圧縮なし・依存ゼロ。ヘッダ 44 バイト + サンプル 2 バイトずつ。
/// </summary>
public static class WavEncoder
{
    public static byte[] Encode(int sampleRate, ReadOnlySpan<float> samples)
    {
        var dataSize = samples.Length * 2;
        using var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16); // fmt チャンクサイズ
        writer.Write((short)1); // PCM
        writer.Write((short)1); // モノラル
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // バイトレート
        writer.Write((short)2); // ブロックアライン
        writer.Write((short)16); // ビット深度

        writer.Write("data"u8);
        writer.Write(dataSize);
        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1f, 1f);
            writer.Write((short)MathF.Round(clamped * short.MaxValue));
        }
        return stream.ToArray();
    }
}
