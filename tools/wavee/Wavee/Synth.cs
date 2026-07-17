namespace Wavee;

/// <summary>
/// SfxDocument からモノラルの波形サンプル(-1〜1 の float 列)を合成する。
/// 位相累積 + 音量エンベロープ(attack で立ち上がり、末尾 decay でフェードアウト)。
/// 乱数は xorshift32 の自前実装で、.NET のバージョンに依らず決定論を保つ。
/// </summary>
public static class Synth
{
    public static float[] Render(SfxDocument doc)
    {
        var count = (int)(doc.Duration * doc.SampleRate);
        var samples = new float[count];
        var dt = 1f / doc.SampleRate;

        var freq = doc.Freq;
        var phase = 0f;
        var rng = (uint)(doc.Seed == 0 ? 1 : doc.Seed);
        var noiseValue = NextNoise(ref rng);

        for (var i = 0; i < count; i++)
        {
            var raw = doc.Wave switch
            {
                WaveForm.Square => phase < doc.Duty ? 1f : -1f,
                WaveForm.Saw => 2f * phase - 1f,
                WaveForm.Triangle => phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase,
                WaveForm.Sine => MathF.Sin(2f * MathF.PI * phase),
                WaveForm.Noise => noiseValue,
                _ => 0f,
            };
            samples[i] = raw * doc.Volume * Envelope(doc, i * dt);

            freq = MathF.Max(0f, freq + doc.FreqSlide * dt);
            phase += freq * dt;
            if (phase >= 1f)
            {
                phase -= MathF.Floor(phase);
                // noise は位相一周ごとに引き直す(freq が音程感を持つサンプル&ホールド)
                noiseValue = NextNoise(ref rng);
            }
        }
        return samples;
    }

    /// <summary>時刻 t 秒での音量係数(0〜1)。attack と decay が重なる場合は小さい方を採る。</summary>
    private static float Envelope(SfxDocument doc, float t)
    {
        var gain = 1f;
        if (doc.Attack > 0f && t < doc.Attack)
        {
            gain = t / doc.Attack;
        }
        if (doc.Decay > 0f)
        {
            var remain = doc.Duration - t;
            if (remain < doc.Decay)
            {
                gain = MathF.Min(gain, remain / doc.Decay);
            }
        }
        return MathF.Max(0f, gain);
    }

    private static float NextNoise(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state / (float)uint.MaxValue * 2f - 1f;
    }
}
