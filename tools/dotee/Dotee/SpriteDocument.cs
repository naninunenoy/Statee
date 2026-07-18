namespace Dotee;

/// <summary>RGBA 1 ピクセルの色。</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A);

/// <summary>
/// スプライトシートの 1 フレーム。`frame: 名前` 節で定義され、名前とドットグリッドを持つ。
/// </summary>
public sealed class SpriteFrame
{
    public string Name { get; }
    public IReadOnlyList<string> Grid { get; }

    private readonly IReadOnlyDictionary<char, Rgba> _palette;

    internal SpriteFrame(string name, List<string> grid, IReadOnlyDictionary<char, Rgba> palette)
    {
        Name = name;
        Grid = grid;
        _palette = palette;
    }

    public Rgba PixelAt(int x, int y) => _palette[Grid[y][x]];
}

/// <summary>
/// スプライト定義テキスト(.sprite.txt)のパース結果。
/// 形式: `palette:` 節に「文字 = #RRGGBB(AA)」、続けて
/// `grid:` 節(単一フレーム)または `frame: 名前` 節の繰り返し(複数フレーム)。
/// 1 行 = 1 走査線。行頭 # はコメント、palette 行は行末 # コメント可。
/// grid の文字はパレット定義済みであること。全フレームは同一寸法であること。
/// </summary>
public sealed class SpriteDocument
{
    public IReadOnlyDictionary<char, Rgba> Palette { get; }

    /// <summary>定義順のフレーム一覧。`grid:` 形式(単一フレーム)では 1 件になる。</summary>
    public IReadOnlyList<SpriteFrame> Frames { get; }

    /// <summary>先頭フレームのグリッド(単一フレーム形式との互換用)。</summary>
    public IReadOnlyList<string> Grid => Frames[0].Grid;

    /// <summary>1 フレームの幅。</summary>
    public int Width => Grid[0].Length;

    /// <summary>1 フレームの高さ。</summary>
    public int Height => Grid.Count;

    /// <summary>フレームを横に並べたシート全体の幅(= Width × フレーム数)。</summary>
    public int SheetWidth => Width * Frames.Count;

    private SpriteDocument(Dictionary<char, Rgba> palette, List<SpriteFrame> frames)
    {
        Palette = palette;
        Frames = frames;
    }

    public Rgba PixelAt(int x, int y) => Frames[0].PixelAt(x, y);

    /// <summary>シート座標(フレームを横並びに合成した座標系)のピクセル色。</summary>
    public Rgba SheetPixelAt(int x, int y) => Frames[x / Width].PixelAt(x % Width, y);

    public static SpriteDocument Parse(string text)
    {
        var palette = new Dictionary<char, Rgba>();
        var frames = new List<(string Name, int LineNo, List<string> Grid)>();
        var section = Section.None;
        var legacyGrid = false;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var line = lines[i];

            // グリッド節はドットをそのまま読む(# もパレット文字になり得ないので安全)。
            // ただし節の切り替え行(frame:)だけは先読みする(grid: との混在検出も兼ねる)
            if (section == Section.Grid && !line.TrimStart().StartsWith("frame:"))
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }
                frames[^1].Grid.Add(line.TrimEnd());
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
                if (frames.Count > 0)
                {
                    throw new FormatException($"{lineNo} 行目: grid: と frame: は混在できません");
                }
                legacyGrid = true;
                frames.Add(("", lineNo, []));
                section = Section.Grid;
                continue;
            }
            if (trimmed.StartsWith("frame:"))
            {
                if (palette.Count == 0)
                {
                    throw new FormatException(
                        $"{lineNo} 行目: frame: の前に palette: 節が必要です"
                    );
                }
                if (legacyGrid)
                {
                    throw new FormatException($"{lineNo} 行目: grid: と frame: は混在できません");
                }
                var name = trimmed["frame:".Length..].Trim();
                if (name.Length == 0)
                {
                    throw new FormatException($"{lineNo} 行目: frame: にはフレーム名が必要です");
                }
                if (frames.Any(f => f.Name == name))
                {
                    throw new FormatException(
                        $"{lineNo} 行目: フレーム名 '{name}' が重複しています"
                    );
                }
                frames.Add((name, lineNo, []));
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

        if (frames.Count == 0 || frames[0].Grid.Count == 0)
        {
            throw new FormatException("grid: / frame: 節がないか、ドット行が 1 行もありません");
        }
        var (w, h) = (frames[0].Grid[0].Length, frames[0].Grid.Count);
        foreach (var frame in frames)
        {
            if (frame.Grid.Count == 0)
            {
                throw new FormatException(
                    $"{frame.LineNo} 行目: フレーム '{frame.Name}' にドット行が 1 行もありません"
                );
            }
            ValidateGrid(frame.Grid, palette);
            if (frame.Grid[0].Length != w || frame.Grid.Count != h)
            {
                throw new FormatException(
                    $"{frame.LineNo} 行目: フレーム '{frame.Name}' の寸法が揃っていません"
                        + $"(先頭フレームは {w}x{h}、このフレームは {frame.Grid[0].Length}x{frame.Grid.Count})"
                );
            }
        }
        return new SpriteDocument(
            palette,
            [.. frames.Select(f => new SpriteFrame(f.Name, f.Grid, palette))]
        );
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
