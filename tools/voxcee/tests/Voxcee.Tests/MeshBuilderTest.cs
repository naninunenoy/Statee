using Shouldly;

namespace Voxcee.Tests;

public class MeshBuilderTest
{
    [Fact]
    public void 単体ボクセルは6面分の三角形を作る()
    {
        var model = VoxelModel.FromVoxels([new Voxel(0, 0, 0, new Rgba(0xFF, 0, 0, 0xFF))]);
        var mesh = MeshBuilder.Build(model);

        mesh.VertexCount.ShouldBe(24);
        mesh.TriangleCount.ShouldBe(12);
    }

    [Fact]
    public void 隣接面はカリングされる()
    {
        var color = new Rgba(0xFF, 0, 0, 0xFF);
        var model = VoxelModel.FromVoxels(
            [new Voxel(0, 0, 0, color), new Voxel(1, 0, 0, color)]
        );
        var mesh = MeshBuilder.Build(model);

        mesh.TriangleCount.ShouldBe(20);
    }
}
