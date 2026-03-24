using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Commands;

namespace WgpuSharp.Tests;

public class EnumTests
{
    [Theory]
    [InlineData(TextureFormat.Rgba8Unorm, "rgba8unorm")]
    [InlineData(TextureFormat.Bgra8Unorm, "bgra8unorm")]
    [InlineData(TextureFormat.Depth24Plus, "depth24plus")]
    [InlineData(TextureFormat.Depth32Float, "depth32float")]
    [InlineData(TextureFormat.Rgba16Float, "rgba16float")]
    public void TextureFormat_ToJsString_ReturnsCorrectValue(TextureFormat format, string expected)
    {
        Assert.Equal(expected, format.ToJsString());
    }

    [Theory]
    [InlineData(VertexFormat.Float32x2, "float32x2")]
    [InlineData(VertexFormat.Float32x3, "float32x3")]
    [InlineData(VertexFormat.Float32x4, "float32x4")]
    [InlineData(VertexFormat.Uint32, "uint32")]
    [InlineData(VertexFormat.Unorm8x4, "unorm8x4")]
    public void VertexFormat_ToJsString_ReturnsCorrectValue(VertexFormat format, string expected)
    {
        Assert.Equal(expected, format.ToJsString());
    }

    [Theory]
    [InlineData(CompareFunction.Less, "less")]
    [InlineData(CompareFunction.LessEqual, "less-equal")]
    [InlineData(CompareFunction.Always, "always")]
    public void CompareFunction_ToJsString_ReturnsCorrectValue(CompareFunction fn, string expected)
    {
        Assert.Equal(expected, fn.ToJsString());
    }

    [Theory]
    [InlineData(PrimitiveTopology.TriangleList, "triangle-list")]
    [InlineData(PrimitiveTopology.LineStrip, "line-strip")]
    public void PrimitiveTopology_ToJsString_ReturnsCorrectValue(PrimitiveTopology topo, string expected)
    {
        Assert.Equal(expected, topo.ToJsString());
    }

    [Theory]
    [InlineData(CullMode.None, "none")]
    [InlineData(CullMode.Back, "back")]
    [InlineData(CullMode.Front, "front")]
    public void CullMode_ToJsString_ReturnsCorrectValue(CullMode mode, string expected)
    {
        Assert.Equal(expected, mode.ToJsString());
    }

    [Theory]
    [InlineData(LoadOp.Clear, "clear")]
    [InlineData(LoadOp.Load, "load")]
    public void LoadOp_ToJsString_ReturnsCorrectValue(LoadOp op, string expected)
    {
        Assert.Equal(expected, op.ToJsString());
    }

    [Theory]
    [InlineData(FilterMode.Linear, "linear")]
    [InlineData(FilterMode.Nearest, "nearest")]
    public void FilterMode_ToJsString_ReturnsCorrectValue(FilterMode mode, string expected)
    {
        Assert.Equal(expected, mode.ToJsString());
    }

    [Fact]
    public void ParseTextureFormat_CommonFormats_RoundTrips()
    {
        Assert.Equal(TextureFormat.Bgra8Unorm, WebGpuEnumExtensions.ParseTextureFormat("bgra8unorm"));
        Assert.Equal(TextureFormat.Rgba8Unorm, WebGpuEnumExtensions.ParseTextureFormat("rgba8unorm"));
    }
}
