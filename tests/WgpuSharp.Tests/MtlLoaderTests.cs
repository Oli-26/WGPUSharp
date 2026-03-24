using WgpuSharp.Mesh;

namespace WgpuSharp.Tests;

public class MtlLoaderTests
{
    [Fact]
    public void Load_BasicMaterial_ParsesColor()
    {
        var mtl = """
            newmtl wood
            Kd 0.6 0.4 0.2
            """;

        var materials = MtlLoader.Load(mtl);

        Assert.Single(materials);
        Assert.True(materials.ContainsKey("wood"));
        var mat = materials["wood"];
        Assert.Equal(0.6f, mat.BaseColor.X, 0.01f);
        Assert.Equal(0.4f, mat.BaseColor.Y, 0.01f);
        Assert.Equal(0.2f, mat.BaseColor.Z, 0.01f);
    }

    [Fact]
    public void Load_MultipleMaterials_ParsesAll()
    {
        var mtl = """
            newmtl red
            Kd 1.0 0.0 0.0

            newmtl blue
            Kd 0.0 0.0 1.0
            """;

        var materials = MtlLoader.Load(mtl);

        Assert.Equal(2, materials.Count);
        Assert.Equal(1f, materials["red"].BaseColor.X);
        Assert.Equal(1f, materials["blue"].BaseColor.Z);
    }

    [Fact]
    public void Load_WithSpecular_CalculatesRoughness()
    {
        var mtl = """
            newmtl shiny
            Kd 0.8 0.8 0.8
            Ns 500
            """;

        var materials = MtlLoader.Load(mtl);
        var mat = materials["shiny"];

        // Ns 500/1000 = 0.5, roughness = 1 - 0.5 = 0.5
        Assert.Equal(0.5f, mat.Roughness, 0.01f);
    }

    [Fact]
    public void Load_WithDissolve_SetsAlpha()
    {
        var mtl = """
            newmtl glass
            Kd 1 1 1
            d 0.5
            """;

        var materials = MtlLoader.Load(mtl);

        Assert.Equal(0.5f, materials["glass"].BaseColor.W, 0.01f);
    }

    [Fact]
    public void Load_Empty_ReturnsEmpty()
    {
        var materials = MtlLoader.Load("# empty\n");
        Assert.Empty(materials);
    }
}
