using WgpuSharp.Interop;

namespace WgpuSharp.Resources;

/// <summary>A bind group that connects GPU resources (buffers, textures, samplers) to a pipeline.</summary>
public sealed class GpuBindGroup : IAsyncDisposable
{
    private readonly JsBridge? _bridge;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuBindGroup(int handle)
    {
        Handle = handle;
    }

    internal GpuBindGroup(JsBridge bridge, int handle)
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

/// <summary>Describes a single resource binding within a bind group.</summary>
public sealed class BindGroupEntry
{
    /// <summary>The binding index matching the shader layout.</summary>
    public required int Binding { get; init; }
    /// <summary>The buffer to bind, if this entry is a buffer binding.</summary>
    public GpuBuffer? Buffer { get; init; }
    /// <summary>Byte offset into the buffer.</summary>
    public long Offset { get; init; }
    /// <summary>Size in bytes of the buffer binding, or null for the full buffer.</summary>
    public long? Size { get; init; }
    /// <summary>The sampler to bind, if this entry is a sampler binding.</summary>
    public GpuSampler? Sampler { get; init; }
    /// <summary>The texture view to bind, if this entry is a texture binding.</summary>
    public GpuTextureView? TextureView { get; init; }

    internal object ToJsObject() => new
    {
        binding = Binding,
        bufferId = Buffer?.Handle,
        offset = Offset,
        size = Size,
        samplerId = Sampler?.Handle,
        textureViewId = TextureView?.Handle,
    };
}
