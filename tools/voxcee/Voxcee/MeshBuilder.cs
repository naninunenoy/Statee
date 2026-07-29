namespace Voxcee;

/// <summary>面カリング済み三角形メッシュ。</summary>
public sealed class VoxelMesh
{
    public required float[] Positions { get; init; }
    public required float[] Normals { get; init; }
    public required float[] Colors { get; init; }
    public required uint[] Indices { get; init; }

    public int VertexCount => Positions.Length / 3;
    public int TriangleCount => Indices.Length / 3;
}

/// <summary>固体ボクセルから外側面だけを取り出してメッシュ化する。</summary>
public static class MeshBuilder
{
    public static VoxelMesh Build(VoxelModel model)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var colors = new List<float>();
        var indices = new List<uint>();
        uint baseIndex = 0;

        foreach (var voxel in model.Voxels)
        {
            AddFaces(model, voxel, positions, normals, colors, indices, ref baseIndex);
        }

        return new VoxelMesh
        {
            Positions = [.. positions],
            Normals = [.. normals],
            Colors = [.. colors],
            Indices = [.. indices],
        };
    }

    private static void AddFaces(
        VoxelModel model,
        Voxel voxel,
        List<float> positions,
        List<float> normals,
        List<float> colors,
        List<uint> indices,
        ref uint baseIndex
    )
    {
        TryAddFace(model, voxel, 1, 0, 0, positions, normals, colors, indices, ref baseIndex);
        TryAddFace(model, voxel, -1, 0, 0, positions, normals, colors, indices, ref baseIndex);
        TryAddFace(model, voxel, 0, 1, 0, positions, normals, colors, indices, ref baseIndex);
        TryAddFace(model, voxel, 0, -1, 0, positions, normals, colors, indices, ref baseIndex);
        TryAddFace(model, voxel, 0, 0, 1, positions, normals, colors, indices, ref baseIndex);
        TryAddFace(model, voxel, 0, 0, -1, positions, normals, colors, indices, ref baseIndex);
    }

    private static void TryAddFace(
        VoxelModel model,
        Voxel voxel,
        int nx,
        int ny,
        int nz,
        List<float> positions,
        List<float> normals,
        List<float> colors,
        List<uint> indices,
        ref uint baseIndex
    )
    {
        if (model.IsSolid(voxel.X + nx, voxel.Y + ny, voxel.Z + nz))
        {
            return;
        }

        AddQuad(voxel, nx, ny, nz, positions, normals, colors, indices, ref baseIndex);
    }

    private static void AddQuad(
        Voxel voxel,
        int nx,
        int ny,
        int nz,
        List<float> positions,
        List<float> normals,
        List<float> colors,
        List<uint> indices,
        ref uint baseIndex
    )
    {
        var x = (float)voxel.X;
        var y = (float)voxel.Y;
        var z = (float)voxel.Z;
        var x1 = x + 1f;
        var y1 = y + 1f;
        var z1 = z + 1f;

        ReadOnlySpan<(float X, float Y, float Z)> corners = (nx, ny, nz) switch
        {
            (1, 0, 0) => [(x1, y, z), (x1, y1, z), (x1, y1, z1), (x1, y, z1)],
            (-1, 0, 0) => [(x, y, z1), (x, y1, z1), (x, y1, z), (x, y, z)],
            (0, 1, 0) => [(x, y1, z1), (x1, y1, z1), (x1, y1, z), (x, y1, z)],
            (0, -1, 0) => [(x, y, z), (x1, y, z), (x1, y, z1), (x, y, z1)],
            (0, 0, 1) => [(x1, y, z1), (x1, y1, z1), (x, y1, z1), (x, y, z1)],
            (0, 0, -1) => [(x, y, z), (x, y1, z), (x1, y1, z), (x1, y, z)],
            _ => throw new InvalidOperationException("不正な法線方向"),
        };

        var normalX = (float)nx;
        var normalY = (float)ny;
        var normalZ = (float)nz;

        for (var i = 0; i < 4; i++)
        {
            positions.Add(corners[i].X);
            positions.Add(corners[i].Y);
            positions.Add(corners[i].Z);
            normals.Add(normalX);
            normals.Add(normalY);
            normals.Add(normalZ);
            colors.Add(voxel.Color.Rf);
            colors.Add(voxel.Color.Gf);
            colors.Add(voxel.Color.Bf);
            colors.Add(voxel.Color.Af);
        }

        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
        baseIndex += 4;
    }
}
