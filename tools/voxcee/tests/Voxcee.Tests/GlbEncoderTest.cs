using System.Buffers.Binary;
using System.Text;
using Shouldly;

namespace Voxcee.Tests;

public class GlbEncoderTest
{
    [Fact]
    public void GLBヘッダとJSONチャンク種別が正しい()
    {
        var mesh = MeshBuilder.Build(
            VoxelModel.FromVoxels([new Voxel(0, 0, 0, new Rgba(0xFF, 0, 0, 0xFF))])
        );
        var glb = GlbEncoder.Encode(mesh);

        ((int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(0))).ShouldBe(0x46546C67);
        ((int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4))).ShouldBe(2);
        Encoding.ASCII.GetString(glb, 16, 4).ShouldBe("JSON");
    }

    [Fact]
    public void BINチャンクが続く()
    {
        var mesh = MeshBuilder.Build(
            VoxelModel.FromVoxels([new Voxel(0, 0, 0, new Rgba(0xFF, 0, 0, 0xFF))])
        );
        var glb = GlbEncoder.Encode(mesh);

        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        var binTypeOffset = 12 + 8 + jsonLength;
        Encoding.ASCII.GetString(glb, binTypeOffset + 4, 3).ShouldBe("BIN");
    }
}
