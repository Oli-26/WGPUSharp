using WgpuSharp.Core;
using WgpuSharp.Interop;
using WgpuSharp.Resources;

namespace WgpuSharp.Pipeline;

/// <summary>A GPU render pipeline defining vertex/fragment shaders, vertex layout, and render state.</summary>
public sealed class GpuRenderPipeline : IAsyncDisposable
{
    private readonly JsBridge? _bridge;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuRenderPipeline(int handle)
    {
        Handle = handle;
    }

    internal GpuRenderPipeline(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        Handle = handle;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed || _bridge is null) return;
        _disposed = true;
        await _bridge.ReleaseHandleAsync(Handle);
    }
}

/// <summary>Describes a render pipeline including vertex/fragment shaders, vertex layout, and render state.</summary>
public sealed class RenderPipelineDescriptor
{
    public required VertexState Vertex { get; init; }
    public required FragmentState Fragment { get; init; }
    public PrimitiveTopology PrimitiveTopology { get; init; } = PrimitiveTopology.TriangleList;
    public CullMode CullMode { get; init; } = CullMode.None;
    public DepthStencilState? DepthStencil { get; init; }

    internal object ToJsObject()
    {
        object? depthStencil = DepthStencil is not null
            ? new
            {
                format = DepthStencil.Format.ToJsString(),
                depthWriteEnabled = DepthStencil.DepthWriteEnabled,
                depthCompare = DepthStencil.DepthCompare.ToJsString(),
            }
            : null;

        return new
        {
            vertexModuleId = Vertex.Module.Handle,
            vertexEntryPoint = Vertex.EntryPoint,
            vertexBuffers = Vertex.Buffers?.Select(b => new
            {
                arrayStride = b.ArrayStride,
                stepMode = b.StepMode.ToJsString(),
                attributes = b.Attributes.Select(a => new
                {
                    shaderLocation = a.ShaderLocation,
                    offset = a.Offset,
                    format = a.Format.ToJsString(),
                }).ToArray(),
            }).ToArray(),
            fragmentModuleId = Fragment.Module.Handle,
            fragmentEntryPoint = Fragment.EntryPoint,
            colorTargets = Fragment.Targets.Select(t =>
            {
                object? blend = t.Blend?.ToJsObject();
                return new
                {
                    format = t.Format.ToJsString(),
                    blend,
                };
            }).ToArray(),
            primitiveTopology = PrimitiveTopology.ToJsString(),
            cullMode = CullMode.ToJsString(),
            depthStencil,
        };
    }
}

public sealed class VertexState
{
    public required GpuShaderModule Module { get; init; }
    public required string EntryPoint { get; init; }
    public VertexBufferLayout[]? Buffers { get; init; }
}

public sealed class FragmentState
{
    public required GpuShaderModule Module { get; init; }
    public required string EntryPoint { get; init; }
    public required ColorTargetState[] Targets { get; init; }
}

/// <summary>Describes one color attachment of a render pipeline.</summary>
public sealed class ColorTargetState
{
    /// <summary>The texture format of this color target.</summary>
    public required TextureFormat Format { get; init; }
    /// <summary>Optional blend state for alpha blending. Null = opaque (no blending).</summary>
    public BlendState? Blend { get; init; }
}

/// <summary>
/// Blend state for a color target. Use <see cref="BlendState.AlphaBlend"/> or
/// <see cref="BlendState.PremultipliedAlpha"/> for common presets.
/// </summary>
public sealed class BlendState
{
    /// <summary>Blend component for the RGB channels.</summary>
    public required BlendComponent Color { get; init; }
    /// <summary>Blend component for the alpha channel.</summary>
    public required BlendComponent Alpha { get; init; }

    /// <summary>Standard alpha blending: srcColor * srcAlpha + dstColor * (1 - srcAlpha).</summary>
    public static BlendState AlphaBlend => new()
    {
        Color = new() { SrcFactor = BlendFactor.SrcAlpha, DstFactor = BlendFactor.OneMinusSrcAlpha, Operation = BlendOperation.Add },
        Alpha = new() { SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha, Operation = BlendOperation.Add },
    };

    /// <summary>Premultiplied alpha blending: srcColor + dstColor * (1 - srcAlpha).</summary>
    public static BlendState PremultipliedAlpha => new()
    {
        Color = new() { SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha, Operation = BlendOperation.Add },
        Alpha = new() { SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha, Operation = BlendOperation.Add },
    };

    /// <summary>Additive blending: srcColor + dstColor.</summary>
    public static BlendState Additive => new()
    {
        Color = new() { SrcFactor = BlendFactor.One, DstFactor = BlendFactor.One, Operation = BlendOperation.Add },
        Alpha = new() { SrcFactor = BlendFactor.One, DstFactor = BlendFactor.One, Operation = BlendOperation.Add },
    };

    internal object ToJsObject() => new
    {
        color = new { srcFactor = Color.SrcFactor.ToJsString(), dstFactor = Color.DstFactor.ToJsString(), operation = Color.Operation.ToJsString() },
        alpha = new { srcFactor = Alpha.SrcFactor.ToJsString(), dstFactor = Alpha.DstFactor.ToJsString(), operation = Alpha.Operation.ToJsString() },
    };
}

/// <summary>Describes how one blend component (color or alpha) is calculated.</summary>
public sealed class BlendComponent
{
    public BlendFactor SrcFactor { get; init; } = BlendFactor.One;
    public BlendFactor DstFactor { get; init; } = BlendFactor.Zero;
    public BlendOperation Operation { get; init; } = BlendOperation.Add;
}

public sealed class VertexBufferLayout
{
    public required long ArrayStride { get; init; }
    public VertexStepMode StepMode { get; init; } = VertexStepMode.Vertex;
    public required VertexAttribute[] Attributes { get; init; }
}

public sealed class VertexAttribute
{
    public required int ShaderLocation { get; init; }
    public required long Offset { get; init; }
    public required VertexFormat Format { get; init; }
}

public sealed class DepthStencilState
{
    public required TextureFormat Format { get; init; }
    public bool DepthWriteEnabled { get; init; } = true;
    public CompareFunction DepthCompare { get; init; } = CompareFunction.Less;
}

public enum PrimitiveTopology
{
    PointList, LineList, LineStrip, TriangleList, TriangleStrip,
}

public enum VertexStepMode
{
    Vertex, Instance,
}

public enum CullMode
{
    None, Front, Back,
}

public static class EnumExtensions
{
    public static string ToJsString(this PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.PointList => "point-list",
        PrimitiveTopology.LineList => "line-list",
        PrimitiveTopology.LineStrip => "line-strip",
        PrimitiveTopology.TriangleList => "triangle-list",
        PrimitiveTopology.TriangleStrip => "triangle-strip",
        _ => "triangle-list",
    };

    public static string ToJsString(this VertexStepMode mode) => mode switch
    {
        VertexStepMode.Vertex => "vertex",
        VertexStepMode.Instance => "instance",
        _ => "vertex",
    };

    public static string ToJsString(this CullMode mode) => mode switch
    {
        CullMode.None => "none",
        CullMode.Front => "front",
        CullMode.Back => "back",
        _ => "none",
    };
}
