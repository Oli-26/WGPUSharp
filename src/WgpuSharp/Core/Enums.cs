namespace WgpuSharp.Core;

/// <summary>Pixel format of a texture.</summary>
public enum TextureFormat
{
    // 8-bit

    /// <summary>8-bit unsigned normalized red channel.</summary>
    R8Unorm,
    /// <summary>8-bit signed normalized red channel.</summary>
    R8Snorm,
    /// <summary>8-bit unsigned integer red channel.</summary>
    R8Uint,
    /// <summary>8-bit signed integer red channel.</summary>
    R8Sint,

    // 16-bit

    /// <summary>16-bit unsigned integer red channel.</summary>
    R16Uint,
    /// <summary>16-bit signed integer red channel.</summary>
    R16Sint,
    /// <summary>16-bit float red channel.</summary>
    R16Float,
    /// <summary>Two 8-bit unsigned normalized channels (red, green).</summary>
    Rg8Unorm,
    /// <summary>Two 8-bit signed normalized channels (red, green).</summary>
    Rg8Snorm,
    /// <summary>Two 8-bit unsigned integer channels (red, green).</summary>
    Rg8Uint,
    /// <summary>Two 8-bit signed integer channels (red, green).</summary>
    Rg8Sint,

    // 32-bit

    /// <summary>32-bit unsigned integer red channel.</summary>
    R32Uint,
    /// <summary>32-bit signed integer red channel.</summary>
    R32Sint,
    /// <summary>32-bit float red channel.</summary>
    R32Float,
    /// <summary>Two 16-bit unsigned integer channels (red, green).</summary>
    Rg16Uint,
    /// <summary>Two 16-bit signed integer channels (red, green).</summary>
    Rg16Sint,
    /// <summary>Two 16-bit float channels (red, green).</summary>
    Rg16Float,
    /// <summary>Four 8-bit unsigned normalized channels (RGBA).</summary>
    Rgba8Unorm,
    /// <summary>Four 8-bit unsigned normalized channels (RGBA) with sRGB encoding.</summary>
    Rgba8UnormSrgb,
    /// <summary>Four 8-bit signed normalized channels (RGBA).</summary>
    Rgba8Snorm,
    /// <summary>Four 8-bit unsigned integer channels (RGBA).</summary>
    Rgba8Uint,
    /// <summary>Four 8-bit signed integer channels (RGBA).</summary>
    Rgba8Sint,
    /// <summary>Four 8-bit unsigned normalized channels in BGRA order.</summary>
    Bgra8Unorm,
    /// <summary>Four 8-bit unsigned normalized channels in BGRA order with sRGB encoding.</summary>
    Bgra8UnormSrgb,

    // 64-bit

    /// <summary>Two 32-bit unsigned integer channels (red, green).</summary>
    Rg32Uint,
    /// <summary>Two 32-bit signed integer channels (red, green).</summary>
    Rg32Sint,
    /// <summary>Two 32-bit float channels (red, green).</summary>
    Rg32Float,
    /// <summary>Four 16-bit unsigned integer channels (RGBA).</summary>
    Rgba16Uint,
    /// <summary>Four 16-bit signed integer channels (RGBA).</summary>
    Rgba16Sint,
    /// <summary>Four 16-bit float channels (RGBA).</summary>
    Rgba16Float,

    // 128-bit

    /// <summary>Four 32-bit unsigned integer channels (RGBA).</summary>
    Rgba32Uint,
    /// <summary>Four 32-bit signed integer channels (RGBA).</summary>
    Rgba32Sint,
    /// <summary>Four 32-bit float channels (RGBA).</summary>
    Rgba32Float,

    // Depth/stencil

    /// <summary>16-bit unsigned normalized depth.</summary>
    Depth16Unorm,
    /// <summary>24-bit depth in an implementation-defined format.</summary>
    Depth24Plus,
    /// <summary>24-bit depth plus 8-bit stencil.</summary>
    Depth24PlusStencil8,
    /// <summary>32-bit float depth.</summary>
    Depth32Float,
}

/// <summary>Operation to perform on a render target at the start of a render pass.</summary>
public enum LoadOp
{
    /// <summary>Clear the render target to a specified value.</summary>
    Clear,
    /// <summary>Preserve the existing contents of the render target.</summary>
    Load,
}

/// <summary>Operation to perform on a render target at the end of a render pass.</summary>
public enum StoreOp
{
    /// <summary>Store the resulting value to the render target.</summary>
    Store,
    /// <summary>Discard the resulting value; contents become undefined.</summary>
    Discard,
}

/// <summary>Comparison function used for depth and stencil testing.</summary>
public enum CompareFunction
{
    /// <summary>Comparison always fails.</summary>
    Never,
    /// <summary>Passes if the new value is less than the existing value.</summary>
    Less,
    /// <summary>Passes if the new value is equal to the existing value.</summary>
    Equal,
    /// <summary>Passes if the new value is less than or equal to the existing value.</summary>
    LessEqual,
    /// <summary>Passes if the new value is greater than the existing value.</summary>
    Greater,
    /// <summary>Passes if the new value is not equal to the existing value.</summary>
    NotEqual,
    /// <summary>Passes if the new value is greater than or equal to the existing value.</summary>
    GreaterEqual,
    /// <summary>Comparison always passes.</summary>
    Always,
}

/// <summary>Filtering mode for texture sampling.</summary>
public enum FilterMode
{
    /// <summary>Returns the texel nearest to the sample point.</summary>
    Nearest,
    /// <summary>Linearly interpolates between the nearest texels.</summary>
    Linear,
}

/// <summary>Addressing/wrapping mode for texture coordinates outside [0, 1].</summary>
public enum AddressMode
{
    /// <summary>Clamp texture coordinates to the [0, 1] range.</summary>
    ClampToEdge,
    /// <summary>Repeat texture coordinates by wrapping around.</summary>
    Repeat,
    /// <summary>Repeat texture coordinates, mirroring on each repetition.</summary>
    MirrorRepeat,
}

/// <summary>Data format of a vertex attribute.</summary>
public enum VertexFormat
{
    // 8-bit

    /// <summary>Two 8-bit unsigned integers.</summary>
    Uint8x2,
    /// <summary>Four 8-bit unsigned integers.</summary>
    Uint8x4,
    /// <summary>Two 8-bit signed integers.</summary>
    Sint8x2,
    /// <summary>Four 8-bit signed integers.</summary>
    Sint8x4,
    /// <summary>Two 8-bit unsigned normalized values.</summary>
    Unorm8x2,
    /// <summary>Four 8-bit unsigned normalized values.</summary>
    Unorm8x4,
    /// <summary>Two 8-bit signed normalized values.</summary>
    Snorm8x2,
    /// <summary>Four 8-bit signed normalized values.</summary>
    Snorm8x4,

    // 16-bit

    /// <summary>Two 16-bit unsigned integers.</summary>
    Uint16x2,
    /// <summary>Four 16-bit unsigned integers.</summary>
    Uint16x4,
    /// <summary>Two 16-bit signed integers.</summary>
    Sint16x2,
    /// <summary>Four 16-bit signed integers.</summary>
    Sint16x4,
    /// <summary>Two 16-bit unsigned normalized values.</summary>
    Unorm16x2,
    /// <summary>Four 16-bit unsigned normalized values.</summary>
    Unorm16x4,
    /// <summary>Two 16-bit signed normalized values.</summary>
    Snorm16x2,
    /// <summary>Four 16-bit signed normalized values.</summary>
    Snorm16x4,
    /// <summary>Two 16-bit floats.</summary>
    Float16x2,
    /// <summary>Four 16-bit floats.</summary>
    Float16x4,

    // 32-bit

    /// <summary>One 32-bit float.</summary>
    Float32,
    /// <summary>Two 32-bit floats.</summary>
    Float32x2,
    /// <summary>Three 32-bit floats.</summary>
    Float32x3,
    /// <summary>Four 32-bit floats.</summary>
    Float32x4,
    /// <summary>One 32-bit unsigned integer.</summary>
    Uint32,
    /// <summary>Two 32-bit unsigned integers.</summary>
    Uint32x2,
    /// <summary>Three 32-bit unsigned integers.</summary>
    Uint32x3,
    /// <summary>Four 32-bit unsigned integers.</summary>
    Uint32x4,
    /// <summary>One 32-bit signed integer.</summary>
    Sint32,
    /// <summary>Two 32-bit signed integers.</summary>
    Sint32x2,
    /// <summary>Three 32-bit signed integers.</summary>
    Sint32x3,
    /// <summary>Four 32-bit signed integers.</summary>
    Sint32x4,
}

/// <summary>Factor used in blend equations for source or destination components.</summary>
public enum BlendFactor
{
    /// <summary>Factor is zero (0, 0, 0, 0).</summary>
    Zero,
    /// <summary>Factor is one (1, 1, 1, 1).</summary>
    One,
    /// <summary>Factor is the source color.</summary>
    Src,
    /// <summary>Factor is one minus the source color.</summary>
    OneMinusSrc,
    /// <summary>Factor is the source alpha.</summary>
    SrcAlpha,
    /// <summary>Factor is one minus the source alpha.</summary>
    OneMinusSrcAlpha,
    /// <summary>Factor is the destination color.</summary>
    Dst,
    /// <summary>Factor is one minus the destination color.</summary>
    OneMinusDst,
    /// <summary>Factor is the destination alpha.</summary>
    DstAlpha,
    /// <summary>Factor is one minus the destination alpha.</summary>
    OneMinusDstAlpha,
    /// <summary>Factor is the minimum of source alpha and one minus destination alpha.</summary>
    SrcAlphaSaturated,
    /// <summary>Factor is a constant blend color.</summary>
    Constant,
    /// <summary>Factor is one minus the constant blend color.</summary>
    OneMinusConstant,
}

/// <summary>Operation that combines source and destination blend components.</summary>
public enum BlendOperation
{
    /// <summary>Source + destination.</summary>
    Add,
    /// <summary>Source - destination.</summary>
    Subtract,
    /// <summary>Destination - source.</summary>
    ReverseSubtract,
    /// <summary>Minimum of source and destination.</summary>
    Min,
    /// <summary>Maximum of source and destination.</summary>
    Max,
}

public static class WebGpuEnumExtensions
{
    public static string ToJsString(this TextureFormat format) => format switch
    {
        TextureFormat.R8Unorm => "r8unorm",
        TextureFormat.R8Snorm => "r8snorm",
        TextureFormat.R8Uint => "r8uint",
        TextureFormat.R8Sint => "r8sint",
        TextureFormat.R16Uint => "r16uint",
        TextureFormat.R16Sint => "r16sint",
        TextureFormat.R16Float => "r16float",
        TextureFormat.Rg8Unorm => "rg8unorm",
        TextureFormat.Rg8Snorm => "rg8snorm",
        TextureFormat.Rg8Uint => "rg8uint",
        TextureFormat.Rg8Sint => "rg8sint",
        TextureFormat.R32Uint => "r32uint",
        TextureFormat.R32Sint => "r32sint",
        TextureFormat.R32Float => "r32float",
        TextureFormat.Rg16Uint => "rg16uint",
        TextureFormat.Rg16Sint => "rg16sint",
        TextureFormat.Rg16Float => "rg16float",
        TextureFormat.Rgba8Unorm => "rgba8unorm",
        TextureFormat.Rgba8UnormSrgb => "rgba8unorm-srgb",
        TextureFormat.Rgba8Snorm => "rgba8snorm",
        TextureFormat.Rgba8Uint => "rgba8uint",
        TextureFormat.Rgba8Sint => "rgba8sint",
        TextureFormat.Bgra8Unorm => "bgra8unorm",
        TextureFormat.Bgra8UnormSrgb => "bgra8unorm-srgb",
        TextureFormat.Rg32Uint => "rg32uint",
        TextureFormat.Rg32Sint => "rg32sint",
        TextureFormat.Rg32Float => "rg32float",
        TextureFormat.Rgba16Uint => "rgba16uint",
        TextureFormat.Rgba16Sint => "rgba16sint",
        TextureFormat.Rgba16Float => "rgba16float",
        TextureFormat.Rgba32Uint => "rgba32uint",
        TextureFormat.Rgba32Sint => "rgba32sint",
        TextureFormat.Rgba32Float => "rgba32float",
        TextureFormat.Depth16Unorm => "depth16unorm",
        TextureFormat.Depth24Plus => "depth24plus",
        TextureFormat.Depth24PlusStencil8 => "depth24plus-stencil8",
        TextureFormat.Depth32Float => "depth32float",
        _ => "rgba8unorm",
    };

    public static TextureFormat ParseTextureFormat(string s) => s switch
    {
        "bgra8unorm" => TextureFormat.Bgra8Unorm,
        "rgba8unorm" => TextureFormat.Rgba8Unorm,
        "rgba8unorm-srgb" => TextureFormat.Rgba8UnormSrgb,
        "bgra8unorm-srgb" => TextureFormat.Bgra8UnormSrgb,
        "rgba16float" => TextureFormat.Rgba16Float,
        _ => TextureFormat.Bgra8Unorm,
    };

    public static string ToJsString(this LoadOp op) => op switch
    {
        LoadOp.Clear => "clear",
        LoadOp.Load => "load",
        _ => "clear",
    };

    public static string ToJsString(this StoreOp op) => op switch
    {
        StoreOp.Store => "store",
        StoreOp.Discard => "discard",
        _ => "store",
    };

    public static string ToJsString(this CompareFunction fn) => fn switch
    {
        CompareFunction.Never => "never",
        CompareFunction.Less => "less",
        CompareFunction.Equal => "equal",
        CompareFunction.LessEqual => "less-equal",
        CompareFunction.Greater => "greater",
        CompareFunction.NotEqual => "not-equal",
        CompareFunction.GreaterEqual => "greater-equal",
        CompareFunction.Always => "always",
        _ => "less",
    };

    public static string ToJsString(this FilterMode mode) => mode switch
    {
        FilterMode.Nearest => "nearest",
        FilterMode.Linear => "linear",
        _ => "linear",
    };

    public static string ToJsString(this AddressMode mode) => mode switch
    {
        AddressMode.ClampToEdge => "clamp-to-edge",
        AddressMode.Repeat => "repeat",
        AddressMode.MirrorRepeat => "mirror-repeat",
        _ => "clamp-to-edge",
    };

    public static string ToJsString(this VertexFormat fmt) => fmt switch
    {
        VertexFormat.Uint8x2 => "uint8x2",
        VertexFormat.Uint8x4 => "uint8x4",
        VertexFormat.Sint8x2 => "sint8x2",
        VertexFormat.Sint8x4 => "sint8x4",
        VertexFormat.Unorm8x2 => "unorm8x2",
        VertexFormat.Unorm8x4 => "unorm8x4",
        VertexFormat.Snorm8x2 => "snorm8x2",
        VertexFormat.Snorm8x4 => "snorm8x4",
        VertexFormat.Uint16x2 => "uint16x2",
        VertexFormat.Uint16x4 => "uint16x4",
        VertexFormat.Sint16x2 => "sint16x2",
        VertexFormat.Sint16x4 => "sint16x4",
        VertexFormat.Unorm16x2 => "unorm16x2",
        VertexFormat.Unorm16x4 => "unorm16x4",
        VertexFormat.Snorm16x2 => "snorm16x2",
        VertexFormat.Snorm16x4 => "snorm16x4",
        VertexFormat.Float16x2 => "float16x2",
        VertexFormat.Float16x4 => "float16x4",
        VertexFormat.Float32 => "float32",
        VertexFormat.Float32x2 => "float32x2",
        VertexFormat.Float32x3 => "float32x3",
        VertexFormat.Float32x4 => "float32x4",
        VertexFormat.Uint32 => "uint32",
        VertexFormat.Uint32x2 => "uint32x2",
        VertexFormat.Uint32x3 => "uint32x3",
        VertexFormat.Uint32x4 => "uint32x4",
        VertexFormat.Sint32 => "sint32",
        VertexFormat.Sint32x2 => "sint32x2",
        VertexFormat.Sint32x3 => "sint32x3",
        VertexFormat.Sint32x4 => "sint32x4",
        _ => "float32x4",
    };

    public static string ToJsString(this BlendFactor f) => f switch
    {
        BlendFactor.Zero => "zero",
        BlendFactor.One => "one",
        BlendFactor.Src => "src",
        BlendFactor.OneMinusSrc => "one-minus-src",
        BlendFactor.SrcAlpha => "src-alpha",
        BlendFactor.OneMinusSrcAlpha => "one-minus-src-alpha",
        BlendFactor.Dst => "dst",
        BlendFactor.OneMinusDst => "one-minus-dst",
        BlendFactor.DstAlpha => "dst-alpha",
        BlendFactor.OneMinusDstAlpha => "one-minus-dst-alpha",
        BlendFactor.SrcAlphaSaturated => "src-alpha-saturated",
        BlendFactor.Constant => "constant",
        BlendFactor.OneMinusConstant => "one-minus-constant",
        _ => "one",
    };

    public static string ToJsString(this BlendOperation op) => op switch
    {
        BlendOperation.Add => "add",
        BlendOperation.Subtract => "subtract",
        BlendOperation.ReverseSubtract => "reverse-subtract",
        BlendOperation.Min => "min",
        BlendOperation.Max => "max",
        _ => "add",
    };
}
