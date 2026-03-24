using WgpuSharp.Mesh;

namespace WgpuSharp.Tests;

public class StlLoaderTests
{
    [Fact]
    public void Load_AsciiStl_ParsesTriangle()
    {
        var stl = """
            solid test
            facet normal 0 0 1
              outer loop
                vertex 0 0 0
                vertex 1 0 0
                vertex 0 1 0
              endloop
            endfacet
            endsolid test
            """;

        var data = System.Text.Encoding.ASCII.GetBytes(stl);
        var mesh = StlLoader.Load(data);

        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(3, mesh.IndexCount);
        Assert.True(mesh.HasNormals);
        Assert.Equal(1f, mesh.Normals[0].Z);
    }

    [Fact]
    public void Load_BinaryStl_ParsesTriangle()
    {
        // Construct a minimal binary STL: 80-byte header + 4-byte count + 50-byte triangle
        var data = new byte[134];
        // Triangle count = 1 at offset 80
        BitConverter.TryWriteBytes(data.AsSpan(80), 1u);

        // Binary STL triangle: normal(12) + v0(12) + v1(12) + v2(12) + attr(2) = 50 bytes
        int off = 84;
        // Normal: (0, 0, 1)
        BitConverter.TryWriteBytes(data.AsSpan(off + 8), 1f);
        // Vertex 0: (0, 0, 0) — already zeros at off+12
        // Vertex 1: (1, 0, 0)
        BitConverter.TryWriteBytes(data.AsSpan(off + 24), 1f); // v1.x at normal(12)+v0(12)
        // Vertex 2: (0, 1, 0)
        BitConverter.TryWriteBytes(data.AsSpan(off + 40), 1f); // v2.y at normal(12)+v0(12)+v1(12)+4

        var mesh = StlLoader.Load(data);

        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(3, mesh.IndexCount);
        Assert.Equal(1f, mesh.Positions[1].X);
        Assert.Equal(1f, mesh.Positions[2].Y);
    }
}
