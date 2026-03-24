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

public sealed class BindGroupEntry
{
    public required int Binding { get; init; }
    public GpuBuffer? Buffer { get; init; }
    public long Offset { get; init; }
    public long? Size { get; init; }
    public GpuSampler? Sampler { get; init; }
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
