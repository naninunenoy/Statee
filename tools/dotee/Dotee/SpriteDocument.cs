namespace Dotee;

/// <summary>RGBA 1 ピクセルの色。</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A);

/// <summary>
/// スプライト定義テキスト(.sprite.txt)のパース結果。
/// 形式: `palette:` 節に「文字 = #RRGGBB(AA)」、`grid:` 節に 1 行 = 1 走査線。
/// 行頭 # はコメント、palette 行は行末 # コメント可。grid の文字はパレット定義済みであること。
/// </summary>
public sealed class SpriteDocument
{
    public IReadOnlyDictionary<char, Rgba> Palette { get; }
    public IReadOnlyList<string> Grid { get; }
    public int Width => Grid[0].Length;
    public int Height => Grid.Count;

    private SpriteDocument(Dictionary<char, Rgba> palette, List<string> grid)
    {
        Palette = palette;
        Grid = grid;
    }

    public Rgba PixelAt(int x, int y) => Palette[Grid[y][x]];

    public static SpriteDocument Parse(string text)
    {
        var palette = new Dictionary<char, Rgba>();
        var grid = new List<string>();
        var section = Section.None;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var line = lines[i];

            // grid 節はドットをそのまま読む(# もパレット文字になり得ないので安全)
            if (section == Section.Grid)
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }
                grid.Add(line.TrimEnd());
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }
            if (trimmed == "palette:")
            {
                section = Section.Palette;
                continue;
            }
            if (trimmed == "grid:")
            {
                if (palette.Count == 0)
                {
                    throw new FormatException($"{lineNo} 行目: grid: の前に palette: 節が必要です");
                }
                section = Section.Grid;
                continue;
            }
            if (section != Section.Palette)
            {
                throw new FormatException(
                    $"{lineNo} 行目: palette: 節の外に定義行があります: {trimmed}"
                );
            }
            ParsePaletteLine(trimmed, lineNo, palette);
        }

        if (grid.Count == 0)
        {
            throw new FormatException("grid: 節がないか、ドット行が 1 行もありません");
        }
        ValidateGrid(grid, palette);
        return new SpriteDocument(palette, grid);
    }

    private static void ParsePaletteLine(string line, int lineNo, Dictionary<char, Rgba> palette)
    {
        // 例: `H = #8F3049   # 髪(深紅)`
        var parts = line.Split('=', 2);
        if (parts.Length != 2 || parts[0].Trim().Length != 1)
        {
            throw new FormatException(
                $"{lineNo} 行目: 「文字 = #RRGGBB」の形式ではありません: {line}"
            );
        }
        var key = parts[0].Trim()[0];
        if (key == '#')
        {
            throw new FormatException(
                $"{lineNo} 行目: # はコメント開始文字のためパレット文字に使えません"
            );
        }
        if (palette.ContainsKey(key))
        {
            throw new FormatException($"{lineNo} 行目: パレット文字 '{key}' が重複しています");
        }

        var value = parts[1].Trim();
        if (!value.StartsWith('#'))
        {
            throw new FormatException(
                $"{lineNo} 行目: 色は #RRGGBB または #RRGGBBAA で指定します: {value}"
            );
        }
        // 色の後ろの # 以降は行末コメント
        var hexEnd = value.IndexOf('#', 1);
        var hex = (hexEnd < 0 ? value[1..] : value[1..hexEnd]).Trim();
        if (hex.Length != 6 && hex.Length != 8)
        {
            throw new FormatException(
                $"{lineNo} 行目: 色は 6 桁か 8 桁の 16 進数で指定します: #{hex}"
            );
        }
        if (!uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgba))
        {
            throw new FormatException($"{lineNo} 行目: 16 進数として読めません: #{hex}");
        }
        if (hex.Length == 6)
        {
            rgba = (rgba << 8) | 0xFF;
        }
        palette[key] = new Rgba(
            (byte)(rgba >> 24),
            (byte)(rgba >> 16),
            (byte)(rgba >> 8),
            (byte)rgba
        );
    }

    private static void ValidateGrid(List<string> grid, Dictionary<char, Rgba> palette)
    {
        var width = grid[0].Length;
        for (var y = 0; y < grid.Count; y++)
        {
            if (grid[y].Length != width)
            {
                throw new FormatException(
                    $"grid の {y + 1} 行目: 幅が揃っていません(1 行目は {width}、この行は {grid[y].Length})"
                );
            }
            foreach (var c in grid[y])
            {
                if (!palette.ContainsKey(c))
                {
                    throw new FormatException(
                        $"grid の {y + 1} 行目: パレットにない文字 '{c}' があります"
                    );
                }
            }
        }
    }

    private enum Section
    {
        None,
        Palette,
        Grid,
    }
}
