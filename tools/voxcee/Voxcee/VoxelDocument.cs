namespace Voxcee;

/// <summary>
/// ボクセル定義テキスト(.voxel.txt)のパース結果。
/// 形式: `palette:` 節に「文字 = #RRGGBB(AA)」、`layers:` 節に水平スライス(Y 昇順)。
/// 空行でスライスを区切る。各スライス行は Z 走査、文字は X。
/// </summary>
public sealed class VoxelDocument
{
    public IReadOnlyDictionary<char, Rgba> Palette { get; }
    public IReadOnlyList<IReadOnlyList<string>> Layers { get; }

    private VoxelDocument(Dictionary<char, Rgba> palette, List<List<string>> layers)
    {
        Palette = palette;
        Layers = layers;
    }

    public VoxelModel ToModel()
    {
        var voxels = new List<Voxel>();
        for (var y = 0; y < Layers.Count; y++)
        {
            var layer = Layers[y];
            for (var z = 0; z < layer.Count; z++)
            {
                var row = layer[z];
                for (var x = 0; x < row.Length; x++)
                {
                    var color = Palette[row[x]];
                    if (color.IsSolid)
                    {
                        voxels.Add(new Voxel(x, y, z, color));
                    }
                }
            }
        }
        return VoxelModel.FromVoxels(voxels);
    }

    public static VoxelDocument Parse(string text)
    {
        var palette = new Dictionary<char, Rgba>();
        var layers = new List<List<string>>();
        var currentLayer = new List<string>();
        var section = Section.None;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var line = lines[i];

            if (section == Section.Layers)
            {
                if (line.Trim().Length == 0)
                {
                    if (currentLayer.Count > 0)
                    {
                        layers.Add(currentLayer);
                        currentLayer = [];
                    }
                    continue;
                }
                currentLayer.Add(line.TrimEnd());
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
            if (trimmed == "layers:")
            {
                if (palette.Count == 0)
                {
                    throw new FormatException(
                        $"{lineNo} 行目: layers: の前に palette: 節が必要です"
                    );
                }
                section = Section.Layers;
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

        if (currentLayer.Count > 0)
        {
            layers.Add(currentLayer);
        }
        if (layers.Count == 0)
        {
            throw new FormatException("layers: 節がないか、スライス行が 1 行もありません");
        }
        ValidateLayers(layers, palette);
        return new VoxelDocument(palette, layers);
    }

    private static void ParsePaletteLine(string line, int lineNo, Dictionary<char, Rgba> palette)
    {
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

    private static void ValidateLayers(List<List<string>> layers, Dictionary<char, Rgba> palette)
    {
        int? width = null;
        int? depth = null;
        for (var y = 0; y < layers.Count; y++)
        {
            var layer = layers[y];
            if (layer.Count == 0)
            {
                throw new FormatException($"layers のスライス {y + 1}: 行が 1 行もありません");
            }
            var layerWidth = layer[0].Length;
            if (width is null)
            {
                width = layerWidth;
                depth = layer.Count;
            }
            else if (layerWidth != width || layer.Count != depth)
            {
                throw new FormatException(
                    $"layers のスライス {y + 1}: 寸法が揃っていません"
                        + $"(期待 {width}x{depth}、実際 {layerWidth}x{layer.Count})"
                );
            }
            for (var z = 0; z < layer.Count; z++)
            {
                if (layer[z].Length != layerWidth)
                {
                    throw new FormatException(
                        $"layers のスライス {y + 1} 行 {z + 1}: 幅が揃っていません"
                    );
                }
                foreach (var c in layer[z])
                {
                    if (!palette.ContainsKey(c))
                    {
                        throw new FormatException(
                            $"layers のスライス {y + 1} 行 {z + 1}: パレットにない文字 '{c}' があります"
                        );
                    }
                }
            }
        }
    }

    private enum Section
    {
        None,
        Palette,
        Layers,
    }
}
