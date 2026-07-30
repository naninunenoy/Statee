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

    [Fact]
    public void 属性セマンティクスが大文字のまま残る()
    {
        var mesh = MeshBuilder.Build(
            VoxelModel.FromVoxels([new Voxel(0, 0, 0, new Rgba(0xFF, 0, 0, 0xFF))])
        );
        var glb = GlbEncoder.Encode(mesh);
        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        var json = Encoding.UTF8.GetString(glb, 20, jsonLength).TrimEnd();

        json.ShouldContain("\"POSITION\":");
        json.ShouldContain("\"NORMAL\":");
        json.ShouldContain("\"COLOR_0\":");
        // Shouldly の string Contains は既定で大小無視なので、厳密比較する
        json.Contains("\"position\":", StringComparison.Ordinal).ShouldBeFalse();
        json.Contains("\"coloR_0\":", StringComparison.Ordinal).ShouldBeFalse();
    }
}
