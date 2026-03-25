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

    [Theory]
    [InlineData(StoreOp.Store, "store")]
    [InlineData(StoreOp.Discard, "discard")]
    public void StoreOp_ToJsString_ReturnsCorrectValue(StoreOp op, string expected)
    {
        Assert.Equal(expected, op.ToJsString());
    }

    [Theory]
    [InlineData(AddressMode.ClampToEdge, "clamp-to-edge")]
    [InlineData(AddressMode.Repeat, "repeat")]
    [InlineData(AddressMode.MirrorRepeat, "mirror-repeat")]
    public void AddressMode_ToJsString_ReturnsCorrectValue(AddressMode mode, string expected)
    {
        Assert.Equal(expected, mode.ToJsString());
    }

    [Theory]
    [InlineData(BlendFactor.Zero, "zero")]
    [InlineData(BlendFactor.One, "one")]
    [InlineData(BlendFactor.SrcAlpha, "src-alpha")]
    [InlineData(BlendFactor.OneMinusSrcAlpha, "one-minus-src-alpha")]
    [InlineData(BlendFactor.DstAlpha, "dst-alpha")]
    [InlineData(BlendFactor.SrcAlphaSaturated, "src-alpha-saturated")]
    public void BlendFactor_ToJsString_ReturnsCorrectValue(BlendFactor factor, string expected)
    {
        Assert.Equal(expected, factor.ToJsString());
    }

    [Theory]
    [InlineData(BlendOperation.Add, "add")]
    [InlineData(BlendOperation.Subtract, "subtract")]
    [InlineData(BlendOperation.ReverseSubtract, "reverse-subtract")]
    [InlineData(BlendOperation.Min, "min")]
    [InlineData(BlendOperation.Max, "max")]
    public void BlendOperation_ToJsString_ReturnsCorrectValue(BlendOperation op, string expected)
    {
        Assert.Equal(expected, op.ToJsString());
    }

    [Theory]
    [InlineData(IndexFormat.Uint16, "uint16")]
    [InlineData(IndexFormat.Uint32, "uint32")]
    public void IndexFormat_ToJsString_ReturnsCorrectValue(IndexFormat format, string expected)
    {
        Assert.Equal(expected, format.ToJsString());
    }

    [Theory]
    [InlineData(VertexStepMode.Vertex, "vertex")]
    [InlineData(VertexStepMode.Instance, "instance")]
    public void VertexStepMode_ToJsString_ReturnsCorrectValue(VertexStepMode mode, string expected)
    {
        Assert.Equal(expected, mode.ToJsString());
    }

    [Fact]
    public void ParseTextureFormat_UnknownFormat_ReturnsBgra8Unorm()
    {
        Assert.Equal(TextureFormat.Bgra8Unorm, WebGpuEnumExtensions.ParseTextureFormat("something-unknown"));
    }

    [Fact]
    public void AllTextureFormats_HaveNonEmptyJsString()
    {
        foreach (TextureFormat fmt in Enum.GetValues<TextureFormat>())
        {
            var js = fmt.ToJsString();
            Assert.False(string.IsNullOrEmpty(js), $"TextureFormat.{fmt} has empty JS string");
        }
    }

    [Fact]
    public void AllVertexFormats_HaveNonEmptyJsString()
    {
        foreach (VertexFormat fmt in Enum.GetValues<VertexFormat>())
        {
            var js = fmt.ToJsString();
            Assert.False(string.IsNullOrEmpty(js), $"VertexFormat.{fmt} has empty JS string");
        }
    }
}
