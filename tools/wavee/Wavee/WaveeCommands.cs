using ConsoleAppFramework;

namespace Wavee;

public class WaveeCommands
{
    /// <summary>効果音定義テキスト(.sfx.txt)を WAV に変換する。</summary>
    /// <param name="input">-i, 効果音定義ファイルのパス</param>
    /// <param name="outDir">-o, 出力先ディレクトリ(既定は入力と同じ場所)</param>
    [Command("render")]
    public int Render(string input, string? outDir = null)
    {
        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"error: ファイルがありません: {input}");
            return 1;
        }

        SfxDocument doc;
        try
        {
            doc = SfxDocument.Parse(File.ReadAllText(input));
        }
        catch (FormatException e)
        {
            Console.Error.WriteLine($"error: {input}: {e.Message}");
            return 1;
        }

        var dir = outDir ?? (Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".");
        Directory.CreateDirectory(dir);
        var wav = Path.Combine(dir, $"{BaseName(input)}.wav");
        File.WriteAllBytes(wav, WavEncoder.Encode(doc.SampleRate, Synth.Render(doc)));
        Console.WriteLine(Path.GetFullPath(wav));
        return 0;
    }

    /// <summary>拡張子 .sfx.txt / .txt を取り除いたファイル名を返す。</summary>
    private static string BaseName(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".sfx.txt", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^".sfx.txt".Length];
        }
        return Path.GetFileNameWithoutExtension(name);
    }
}
