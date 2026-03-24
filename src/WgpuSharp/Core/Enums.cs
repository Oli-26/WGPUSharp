namespace WgpuSharp.Core;

public enum TextureFormat
{
    // 8-bit
    R8Unorm, R8Snorm, R8Uint, R8Sint,
    // 16-bit
    R16Uint, R16Sint, R16Float,
    Rg8Unorm, Rg8Snorm, Rg8Uint, Rg8Sint,
    // 32-bit
    R32Uint, R32Sint, R32Float,
    Rg16Uint, Rg16Sint, Rg16Float,
    Rgba8Unorm, Rgba8UnormSrgb, Rgba8Snorm, Rgba8Uint, Rgba8Sint,
    Bgra8Unorm, Bgra8UnormSrgb,
    // 64-bit
    Rg32Uint, Rg32Sint, Rg32Float,
    Rgba16Uint, Rgba16Sint, Rgba16Float,
    // 128-bit
    Rgba32Uint, Rgba32Sint, Rgba32Float,
    // Depth/stencil
    Depth16Unorm, Depth24Plus, Depth24PlusStencil8, Depth32Float,
}

public enum LoadOp
{
    Clear,
    Load,
}

public enum StoreOp
{
    Store,
    Discard,
}

public enum CompareFunction
{
    Never,
    Less,
    Equal,
    LessEqual,
    Greater,
    NotEqual,
    GreaterEqual,
    Always,
}

public enum FilterMode
{
    Nearest,
    Linear,
}

public enum AddressMode
{
    ClampToEdge,
    Repeat,
    MirrorRepeat,
}

public enum VertexFormat
{
    // 8-bit
    Uint8x2, Uint8x4, Sint8x2, Sint8x4,
    Unorm8x2, Unorm8x4, Snorm8x2, Snorm8x4,
    // 16-bit
    Uint16x2, Uint16x4, Sint16x2, Sint16x4,
    Unorm16x2, Unorm16x4, Snorm16x2, Snorm16x4,
    Float16x2, Float16x4,
    // 32-bit
    Float32, Float32x2, Float32x3, Float32x4,
    Uint32, Uint32x2, Uint32x3, Uint32x4,
    Sint32, Sint32x2, Sint32x3, Sint32x4,
}

public enum BlendFactor
{
    Zero, One,
    Src, OneMinusSrc,
    SrcAlpha, OneMinusSrcAlpha,
    Dst, OneMinusDst,
    DstAlpha, OneMinusDstAlpha,
    SrcAlphaSaturated,
    Constant, OneMinusConstant,
}

public enum BlendOperation
{
    Add, Subtract, ReverseSubtract, Min, Max,
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
