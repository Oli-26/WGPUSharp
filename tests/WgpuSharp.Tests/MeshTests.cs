using System.Numerics;
using WgpuSharp.Mesh;

namespace WgpuSharp.Tests;

public class MeshTests
{
    [Fact]
    public void GetInterleavedVertices_PositionOnly_Returns3FloatsPerVertex()
    {
        var mesh = new WgpuSharp.Mesh.Mesh
        {
            Positions = [new(1, 2, 3), new(4, 5, 6)],
        };

        var data = mesh.GetInterleavedVertices();

        Assert.Equal(6, data.Length); // 2 verts * 3 floats
        Assert.Equal(1f, data[0]);
        Assert.Equal(4f, data[3]);
    }

    [Fact]
    public void GetInterleavedVertices_WithNormalsAndUVs_Returns8FloatsPerVertex()
    {
        var mesh = new WgpuSharp.Mesh.Mesh
        {
            Positions = [new(1, 0, 0)],
            Normals = [new(0, 1, 0)],
            TexCoords = [new(0.5f, 0.5f)],
        };

        var data = mesh.GetInterleavedVertices();

        Assert.Equal(8, data.Length); // pos(3) + normal(3) + uv(2)
        Assert.Equal(1f, data[0]);   // pos.x
        Assert.Equal(1f, data[4]);   // normal.y
        Assert.Equal(0.5f, data[6]); // uv.x
    }

    [Fact]
    public void ComputeFlatNormals_CreatesPerFaceNormals()
    {
        var mesh = new WgpuSharp.Mesh.Mesh
        {
            Positions = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)],
            Indices = [0, 1, 2],
        };

        var result = mesh.ComputeFlatNormals();

        Assert.True(result.HasNormals);
        Assert.Equal(3, result.VertexCount);
        // Normal of triangle in XY plane should point in Z
        Assert.True(MathF.Abs(result.Normals[0].Z) > 0.9f);
    }

    [Fact]
    public void GenerateBoxUVs_ProducesUVsForAllVertices()
    {
        var mesh = new WgpuSharp.Mesh.Mesh
        {
            Positions = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)],
            Normals = [new(0, 0, 1), new(0, 0, 1), new(0, 0, 1)],
            Indices = [0, 1, 2],
        };

        var result = mesh.GenerateBoxUVs();

        Assert.True(result.HasTexCoords);
        Assert.Equal(3, result.TexCoords.Length);
    }

    [Fact]
    public void MeshLoader_Load_UnsupportedFormat_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            MeshLoader.Load(new byte[] { 1, 2, 3 }, "model.fbx"));
    }

    [Fact]
    public void MeshLoader_Load_EmptyData_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MeshLoader.Load([], "model.obj"));
    }
}
