namespace Voxcee;

/// <summary>占有ボクセル 1 個。</summary>
public readonly record struct Voxel(int X, int Y, int Z, Rgba Color);

/// <summary>面カリング用のボクセル集合。</summary>
public sealed class VoxelModel
{
    private readonly Dictionary<(int X, int Y, int Z), Rgba> _voxels;

    public IReadOnlyCollection<Voxel> Voxels =>
        _voxels.Select(kv => new Voxel(kv.Key.X, kv.Key.Y, kv.Key.Z, kv.Value)).ToList();

    public int VoxelCount => _voxels.Count;

    private VoxelModel(Dictionary<(int X, int Y, int Z), Rgba> voxels)
    {
        _voxels = voxels;
    }

    public static VoxelModel FromVoxels(IEnumerable<Voxel> voxels)
    {
        var map = new Dictionary<(int X, int Y, int Z), Rgba>();
        foreach (var voxel in voxels)
        {
            if (!voxel.Color.IsSolid)
            {
                continue;
            }
            map[(voxel.X, voxel.Y, voxel.Z)] = voxel.Color;
        }
        if (map.Count == 0)
        {
            throw new FormatException("固体ボクセルが 1 個もありません");
        }
        return new VoxelModel(map);
    }

    public bool IsSolid(int x, int y, int z) => _voxels.ContainsKey((x, y, z));

    public Rgba ColorAt(int x, int y, int z) => _voxels[(x, y, z)];
}
