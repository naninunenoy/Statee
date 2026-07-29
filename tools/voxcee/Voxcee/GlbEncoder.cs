using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Voxcee;

/// <summary>面カリング済みボクセルメッシュを GLB 2.0 にエンコードする。</summary>
public static class GlbEncoder
{
    public static byte[] Encode(VoxelMesh mesh)
    {
        var posBytes = FloatSpanToBytes(mesh.Positions);
        var normalBytes = FloatSpanToBytes(mesh.Normals);
        var colorBytes = FloatSpanToBytes(mesh.Colors);
        var indexBytes = IndexSpanToBytes(mesh.Indices);

        var posOffset = 0;
        var normalOffset = Align4(posOffset + posBytes.Length);
        var colorOffset = Align4(normalOffset + normalBytes.Length);
        var indexOffset = Align4(colorOffset + colorBytes.Length);
        var binLength = Align4(indexOffset + indexBytes.Length);

        var bin = new byte[binLength];
        posBytes.CopyTo(bin.AsSpan(posOffset));
        normalBytes.CopyTo(bin.AsSpan(normalOffset));
        colorBytes.CopyTo(bin.AsSpan(colorOffset));
        indexBytes.CopyTo(bin.AsSpan(indexOffset));

        var indexComponentType = mesh.Indices.Any(i => i > ushort.MaxValue) ? 5125 : 5123;
        var (posMin, posMax) = MinMaxVec3(mesh.Positions);

        var gltf = new
        {
            asset = new { version = "2.0", generator = "voxcee" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new
                            {
                                POSITION = 0,
                                NORMAL = 1,
                                COLOR_0 = 2,
                            },
                            indices = 3,
                            mode = 4,
                        },
                    },
                },
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126,
                    count = mesh.Positions.Length / 3,
                    type = "VEC3",
                    min = posMin,
                    max = posMax,
                },
                new
                {
                    bufferView = 1,
                    componentType = 5126,
                    count = mesh.Normals.Length / 3,
                    type = "VEC3",
                },
                new
                {
                    bufferView = 2,
                    componentType = 5126,
                    count = mesh.Colors.Length / 4,
                    type = "VEC4",
                },
                new
                {
                    bufferView = 3,
                    componentType = indexComponentType,
                    count = mesh.Indices.Length,
                    type = "SCALAR",
                },
            },
            bufferViews = new object[]
            {
                new
                {
                    buffer = 0,
                    byteOffset = posOffset,
                    byteLength = posBytes.Length,
                    target = 34962,
                },
                new
                {
                    buffer = 0,
                    byteOffset = normalOffset,
                    byteLength = normalBytes.Length,
                    target = 34962,
                },
                new
                {
                    buffer = 0,
                    byteOffset = colorOffset,
                    byteLength = colorBytes.Length,
                    target = 34962,
                },
                new
                {
                    buffer = 0,
                    byteOffset = indexOffset,
                    byteLength = indexBytes.Length,
                    target = 34963,
                },
            },
            buffers = new[] { new { byteLength = binLength } },
        };

        var json = JsonSerializer.Serialize(
            gltf,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
        var jsonChunkLength = jsonBytes.Length + jsonPadding;
        var binPadding = (4 - (binLength % 4)) % 4;
        var binChunkLength = binLength + binPadding;
        var totalLength = 12 + 8 + jsonChunkLength + 8 + binChunkLength;

        var glb = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)totalLength);

        var pos = 12;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(pos), (uint)jsonChunkLength);
        pos += 4;
        Encoding.ASCII.GetBytes("JSON").CopyTo(glb.AsSpan(pos));
        pos += 4;
        jsonBytes.CopyTo(glb.AsSpan(pos));
        pos += jsonBytes.Length;
        for (var i = 0; i < jsonPadding; i++)
        {
            glb[pos++] = 0x20;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(pos), (uint)binChunkLength);
        pos += 4;
        Encoding.ASCII.GetBytes("BIN\u0000").CopyTo(glb.AsSpan(pos));
        pos += 4;
        bin.CopyTo(glb.AsSpan(pos));
        return glb;
    }

    private static (float[] Min, float[] Max) MinMaxVec3(float[] values)
    {
        var min = new float[]
        {
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity,
        };
        var max = new float[]
        {
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity,
        };
        for (var i = 0; i < values.Length; i += 3)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                min[axis] = Math.Min(min[axis], values[i + axis]);
                max[axis] = Math.Max(max[axis], values[i + axis]);
            }
        }
        return (min, max);
    }

    private static byte[] FloatSpanToBytes(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), values[i]);
        }
        return bytes;
    }

    private static byte[] IndexSpanToBytes(uint[] indices)
    {
        if (indices.Any(i => i > ushort.MaxValue))
        {
            var bytes = new byte[indices.Length * 4];
            for (var i = 0; i < indices.Length; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), indices[i]);
            }
            return bytes;
        }

        var shortBytes = new byte[indices.Length * 2];
        for (var i = 0; i < indices.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(shortBytes.AsSpan(i * 2), (ushort)indices[i]);
        }
        return shortBytes;
    }

    private static int Align4(int value) => (value + 3) & ~3;
}
