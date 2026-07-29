using ConsoleAppFramework;

namespace Voxcee;

public class VoxceeCommands
{
    /// <summary>ボクセル定義テキスト(.voxel.txt)を GLB に変換する。</summary>
    /// <param name="input">-i, ボクセル定義ファイルのパス</param>
    /// <param name="outDir">-o, 出力先ディレクトリ(既定は入力と同じ場所)</param>
    [Command("render")]
    public int Render(string input, string? outDir = null)
    {
        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"error: ファイルがありません: {input}");
            return 1;
        }

        VoxelModel model;
        try
        {
            model = LoadModel(input);
        }
        catch (FormatException e)
        {
            Console.Error.WriteLine($"error: {input}: {e.Message}");
            return 1;
        }

        var dir = outDir ?? (Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".");
        Directory.CreateDirectory(dir);
        var glb = Path.Combine(dir, $"{BaseName(input)}.glb");
        var mesh = MeshBuilder.Build(model);
        File.WriteAllBytes(glb, GlbEncoder.Encode(mesh));
        Console.WriteLine(Path.GetFullPath(glb));
        return 0;
    }

    private static VoxelModel LoadModel(string input)
    {
        if (
            input.EndsWith(".voxel.txt", StringComparison.OrdinalIgnoreCase)
            || input.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        )
        {
            return VoxelDocument.Parse(File.ReadAllText(input)).ToModel();
        }
        throw new FormatException("入力は .voxel.txt を指定してください");
    }

    /// <summary>拡張子 .voxel.txt / .txt を取り除いたファイル名を返す。</summary>
    private static string BaseName(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".voxel.txt", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^".voxel.txt".Length];
        }
        return Path.GetFileNameWithoutExtension(name);
    }
}
