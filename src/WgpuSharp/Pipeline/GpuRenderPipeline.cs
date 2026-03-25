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
    /// <summary>Multisample state for MSAA. Null uses the default (1 sample, no MSAA).</summary>
    public MultisampleState? Multisample { get; init; }

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

        object? multisample = Multisample is not null
            ? new { count = Multisample.Count }
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
            multisample,
        };
    }
}

/// <summary>Configures multisample anti-aliasing (MSAA) for a render pipeline.</summary>
public sealed class MultisampleState
{
    /// <summary>Number of samples per pixel. Must be 1 or 4. Default: 4.</summary>
    public int Count { get; init; } = 4;
}

/// <summary>Describes the vertex stage of a render pipeline.</summary>
public sealed class VertexState
{
    /// <summary>The shader module containing the vertex entry point.</summary>
    public required GpuShaderModule Module { get; init; }
    /// <summary>The name of the vertex shader entry point function.</summary>
    public required string EntryPoint { get; init; }
    /// <summary>The vertex buffer layouts describing the vertex data, or null if no vertex buffers are used.</summary>
    public VertexBufferLayout[]? Buffers { get; init; }
}

/// <summary>Describes the fragment stage of a render pipeline.</summary>
public sealed class FragmentState
{
    /// <summary>The shader module containing the fragment entry point.</summary>
    public required GpuShaderModule Module { get; init; }
    /// <summary>The name of the fragment shader entry point function.</summary>
    public required string EntryPoint { get; init; }
    /// <summary>The color target states for each render attachment.</summary>
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

/// <summary>Describes the layout of a single vertex buffer bound to a render pipeline.</summary>
public sealed class VertexBufferLayout
{
    /// <summary>The stride in bytes between consecutive elements in the buffer.</summary>
    public required long ArrayStride { get; init; }
    /// <summary>Whether the buffer steps per vertex or per instance.</summary>
    public VertexStepMode StepMode { get; init; } = VertexStepMode.Vertex;
    /// <summary>The vertex attributes read from this buffer.</summary>
    public required VertexAttribute[] Attributes { get; init; }
}

/// <summary>Describes a single vertex attribute within a vertex buffer layout.</summary>
public sealed class VertexAttribute
{
    /// <summary>The shader location index this attribute maps to.</summary>
    public required int ShaderLocation { get; init; }
    /// <summary>The byte offset of this attribute relative to the start of the element.</summary>
    public required long Offset { get; init; }
    /// <summary>The data format of this vertex attribute.</summary>
    public required VertexFormat Format { get; init; }
}

/// <summary>Describes the depth/stencil state for a render pipeline.</summary>
public sealed class DepthStencilState
{
    /// <summary>The texture format of the depth/stencil attachment.</summary>
    public required TextureFormat Format { get; init; }
    /// <summary>Whether depth values are written to the depth buffer.</summary>
    public bool DepthWriteEnabled { get; init; } = true;
    /// <summary>The comparison function used for the depth test.</summary>
    public CompareFunction DepthCompare { get; init; } = CompareFunction.Less;
}

/// <summary>Specifies how vertices are assembled into primitives.</summary>
public enum PrimitiveTopology
{
    /// <summary>Each vertex is a separate point.</summary>
    PointList,
    /// <summary>Each pair of vertices forms a separate line segment.</summary>
    LineList,
    /// <summary>Vertices form a connected strip of line segments.</summary>
    LineStrip,
    /// <summary>Each group of three vertices forms a separate triangle.</summary>
    TriangleList,
    /// <summary>Vertices form a connected strip of triangles.</summary>
    TriangleStrip,
}

/// <summary>Specifies whether a vertex buffer steps per vertex or per instance.</summary>
public enum VertexStepMode
{
    /// <summary>The buffer advances once per vertex.</summary>
    Vertex,
    /// <summary>The buffer advances once per instance.</summary>
    Instance,
}

/// <summary>Specifies which triangle faces are culled during rasterization.</summary>
public enum CullMode
{
    /// <summary>No faces are culled.</summary>
    None,
    /// <summary>Front-facing triangles are culled.</summary>
    Front,
    /// <summary>Back-facing triangles are culled.</summary>
    Back,
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
