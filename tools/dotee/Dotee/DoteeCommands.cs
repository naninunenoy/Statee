using ConsoleAppFramework;

namespace Dotee;

public class DoteeCommands
{
    /// <summary>スプライト定義テキスト(.sprite.txt)を PNG に変換する。</summary>
    /// <param name="input">-i, スプライト定義ファイルのパス</param>
    /// <param name="outDir">-o, 出力先ディレクトリ(既定は入力と同じ場所)</param>
    /// <param name="scale">-s, 確認用拡大 PNG の倍率(1 なら等倍のみ出力)</param>
    [Command("render")]
    public int Render(string input, string? outDir = null, int scale = 16)
    {
        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"error: ファイルがありません: {input}");
            return 1;
        }
        if (scale < 1)
        {
            Console.Error.WriteLine($"error: --scale は 1 以上を指定します: {scale}");
            return 1;
        }

        SpriteDocument doc;
        try
        {
            doc = SpriteDocument.Parse(File.ReadAllText(input));
        }
        catch (FormatException e)
        {
            Console.Error.WriteLine($"error: {input}: {e.Message}");
            return 1;
        }

        var dir = outDir ?? (Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".");
        Directory.CreateDirectory(dir);
        var name = BaseName(input);

        var png = Path.Combine(dir, $"{name}.png");
        File.WriteAllBytes(png, PngEncoder.Encode(doc.Width, doc.Height, doc.PixelAt));
        Console.WriteLine(Path.GetFullPath(png));

        if (scale > 1)
        {
            var scaled = Path.Combine(dir, $"{name}_x{scale}.png");
            File.WriteAllBytes(
                scaled,
                PngEncoder.Encode(
                    doc.Width * scale,
                    doc.Height * scale,
                    (x, y) => doc.PixelAt(x / scale, y / scale)
                )
            );
            Console.WriteLine(Path.GetFullPath(scaled));
        }
        return 0;
    }

    /// <summary>拡張子 .sprite.txt / .txt を取り除いたファイル名を返す。</summary>
    private static string BaseName(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".sprite.txt", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^".sprite.txt".Length];
        }
        return Path.GetFileNameWithoutExtension(name);
    }
}
