using System.Globalization;

namespace Wavee;

/// <summary>波形の種類。noise は位相が一周するたびに値を引き直すサンプル&amp;ホールド型。</summary>
public enum WaveForm
{
    Square,
    Saw,
    Triangle,
    Sine,
    Noise,
}

/// <summary>
/// 効果音定義テキスト(.sfx.txt)のパース結果。
/// 形式: 1 行 1 パラメタの「key: value」。行頭 # はコメント、行末 # 以降もコメント。
/// 必須は wave と duration。他は既定値を持つ(sfxr 系の最小パラメタセット)。
/// </summary>
public sealed record SfxDocument
{
    /// <summary>波形(wave: square / saw / triangle / sine / noise)。</summary>
    public required WaveForm Wave { get; init; }

    /// <summary>音の長さ 秒(duration)。0 より大きく 10 以下。</summary>
    public required float Duration { get; init; }

    /// <summary>開始周波数 Hz(freq)。</summary>
    public float Freq { get; init; } = 440f;

    /// <summary>ピッチスイープ Hz/秒(freq-slide)。負で下降。0 Hz 未満には落ちない。</summary>
    public float FreqSlide { get; init; }

    /// <summary>音量の立ち上がり 秒(attack)。</summary>
    public float Attack { get; init; }

    /// <summary>末尾のフェードアウト 秒(decay)。</summary>
    public float Decay { get; init; }

    /// <summary>音量 0〜1(volume)。</summary>
    public float Volume { get; init; } = 0.5f;

    /// <summary>矩形波のデューティ比 0〜1(duty)。square 以外では無視。</summary>
    public float Duty { get; init; } = 0.5f;

    /// <summary>noise の乱数シード(seed)。同じ定義からは同じ波形が出る(決定論)。</summary>
    public int Seed { get; init; } = 1;

    /// <summary>サンプリングレート Hz(sample-rate)。</summary>
    public int SampleRate { get; init; } = 44100;

    public static SfxDocument Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var line = lines[i];
            var hash = line.IndexOf('#');
            if (hash >= 0)
            {
                line = line[..hash];
            }
            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            var parts = line.Split(':', 2);
            if (parts.Length != 2)
            {
                throw new FormatException(
                    $"{lineNo} 行目: 「key: value」の形式ではありません: {line}"
                );
            }
            var key = parts[0].Trim();
            if (!values.TryAdd(key, parts[1].Trim()))
            {
                throw new FormatException($"{lineNo} 行目: キー '{key}' が重複しています");
            }
        }

        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "wave",
            "duration",
            "freq",
            "freq-slide",
            "attack",
            "decay",
            "volume",
            "duty",
            "seed",
            "sample-rate",
        };
        foreach (var key in values.Keys)
        {
            if (!known.Contains(key))
            {
                throw new FormatException($"未知のキー '{key}'({string.Join(" / ", known)})");
            }
        }

        if (!values.TryGetValue("wave", out var waveText))
        {
            throw new FormatException("必須キー wave がありません");
        }
        if (!Enum.TryParse<WaveForm>(waveText, ignoreCase: true, out var wave))
        {
            throw new FormatException(
                $"wave は square / saw / triangle / sine / noise から選びます: {waveText}"
            );
        }
        if (!values.ContainsKey("duration"))
        {
            throw new FormatException("必須キー duration がありません");
        }

        var doc = new SfxDocument
        {
            Wave = wave,
            Duration = ParseFloat(values, "duration", 0f),
            Freq = ParseFloat(values, "freq", 440f),
            FreqSlide = ParseFloat(values, "freq-slide", 0f),
            Attack = ParseFloat(values, "attack", 0f),
            Decay = ParseFloat(values, "decay", 0f),
            Volume = ParseFloat(values, "volume", 0.5f),
            Duty = ParseFloat(values, "duty", 0.5f),
            Seed = ParseInt(values, "seed", 1),
            SampleRate = ParseInt(values, "sample-rate", 44100),
        };
        Validate(doc);
        return doc;
    }

    private static void Validate(SfxDocument doc)
    {
        if (doc.Duration <= 0f || doc.Duration > 10f)
        {
            throw new FormatException($"duration は 0 より大きく 10 秒以下です: {doc.Duration}");
        }
        if (doc.Volume is < 0f or > 1f)
        {
            throw new FormatException($"volume は 0〜1 です: {doc.Volume}");
        }
        if (doc.Duty is <= 0f or >= 1f)
        {
            throw new FormatException($"duty は 0 より大きく 1 未満です: {doc.Duty}");
        }
        if (doc.Freq < 0f)
        {
            throw new FormatException($"freq は 0 以上です: {doc.Freq}");
        }
        if (doc.Attack < 0f || doc.Decay < 0f)
        {
            throw new FormatException("attack / decay は 0 以上です");
        }
        if (doc.SampleRate is < 8000 or > 96000)
        {
            throw new FormatException($"sample-rate は 8000〜96000 です: {doc.SampleRate}");
        }
    }

    private static float ParseFloat(Dictionary<string, string> values, string key, float fallback)
    {
        if (!values.TryGetValue(key, out var text))
        {
            return fallback;
        }
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"{key} が数値として読めません: {text}");
        }
        return value;
    }

    private static int ParseInt(Dictionary<string, string> values, string key, int fallback)
    {
        if (!values.TryGetValue(key, out var text))
        {
            return fallback;
        }
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"{key} が整数として読めません: {text}");
        }
        return value;
    }
}
