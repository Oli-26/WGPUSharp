using System.Numerics;
using WgpuSharp.Mesh;

namespace WgpuSharp.Tests;

public class MaterialTests
{
    [Fact]
    public void Default_HasWhiteBaseColor()
    {
        var mat = Material.Default;
        Assert.Equal(Vector4.One, mat.BaseColor);
    }

    [Fact]
    public void GetUniformBytes_Returns48Bytes()
    {
        var mat = new Material
        {
            BaseColor = new Vector4(1, 0, 0, 1),
            Metallic = 0.5f,
            Roughness = 0.8f,
        };

        var bytes = mat.GetUniformBytes();

        Assert.Equal(48, bytes.Length);
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 0));  // baseColor.x
        Assert.Equal(0f, BitConverter.ToSingle(bytes, 4));  // baseColor.y
        Assert.Equal(0.5f, BitConverter.ToSingle(bytes, 28)); // metallic
        Assert.Equal(0.8f, BitConverter.ToSingle(bytes, 32)); // roughness
    }

    [Fact]
    public void HasBaseColorTexture_TrueWhenDataPresent()
    {
        var mat = new Material { BaseColorTextureData = new byte[] { 1 } };
        Assert.True(mat.HasBaseColorTexture);
    }

    [Fact]
    public void HasBaseColorTexture_FalseWhenNull()
    {
        Assert.False(Material.Default.HasBaseColorTexture);
    }

    [Fact]
    public void GetUniformBytes_HasTexture_SetsFlag()
    {
        var mat = new Material { BaseColorTextureData = new byte[] { 1 } };
        var bytes = mat.GetUniformBytes();
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 36)); // hasTexture
    }
}
