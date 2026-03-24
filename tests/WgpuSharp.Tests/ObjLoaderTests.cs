using WgpuSharp.Mesh;

namespace WgpuSharp.Tests;

public class ObjLoaderTests
{
    [Fact]
    public void Load_SimpleTriangle_ParsesPositionsAndIndices()
    {
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            f 1 2 3
            """;

        var mesh = ObjLoader.Load(obj);

        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(3, mesh.IndexCount);
        Assert.Equal(0f, mesh.Positions[0].X);
        Assert.Equal(1f, mesh.Positions[1].X);
        Assert.Equal(1f, mesh.Positions[2].Y);
    }

    [Fact]
    public void Load_WithNormals_ParsesNormals()
    {
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vn 0 0 1
            f 1//1 2//1 3//1
            """;

        var mesh = ObjLoader.Load(obj);

        Assert.True(mesh.HasNormals);
        Assert.Equal(1f, mesh.Normals[0].Z);
    }

    [Fact]
    public void Load_WithTexCoords_ParsesUVs()
    {
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vt 0.0 0.0
            vt 1.0 0.0
            vt 0.0 1.0
            f 1/1 2/2 3/3
            """;

        var mesh = ObjLoader.Load(obj);

        Assert.True(mesh.HasTexCoords);
        Assert.Equal(0f, mesh.TexCoords[0].X);
        Assert.Equal(1f, mesh.TexCoords[1].X);
    }

    [Fact]
    public void Load_QuadFace_Triangulates()
    {
        var obj = """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            f 1 2 3 4
            """;

        var mesh = ObjLoader.Load(obj);

        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(6, mesh.IndexCount); // 2 triangles
    }

    [Fact]
    public void Load_NegativeIndices_ResolvesCorrectly()
    {
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f -3 -2 -1
            """;

        var mesh = ObjLoader.Load(obj);

        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(3, mesh.IndexCount);
    }

    [Fact]
    public void Load_WithMtl_AppliesMaterial()
    {
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            usemtl red
            f 1 2 3
            """;

        var mtl = """
            newmtl red
            Kd 1.0 0.0 0.0
            """;

        var mesh = ObjLoader.Load(obj, mtl);

        Assert.Equal("red", mesh.Material.Name);
        Assert.Equal(1f, mesh.Material.BaseColor.X);
        Assert.Equal(0f, mesh.Material.BaseColor.Y);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsEmptyMesh()
    {
        var mesh = ObjLoader.Load("# just a comment\n");

        Assert.Equal(0, mesh.VertexCount);
        Assert.Equal(0, mesh.IndexCount);
    }
}
