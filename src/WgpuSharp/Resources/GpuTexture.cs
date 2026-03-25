using WgpuSharp.Core;
using WgpuSharp.Interop;

namespace WgpuSharp.Resources;

/// <summary>A GPU texture. Dispose to free GPU memory.</summary>
public sealed class GpuTexture : IAsyncDisposable
{
    private readonly JsBridge _bridge;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuTexture(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        Handle = handle;
    }

    public async Task<GpuTextureView> CreateViewAsync(CancellationToken ct = default)
    {
        var handle = await _bridge.CreateTextureViewWithDescriptorAsync(Handle, null, ct);
        return new GpuTextureView(_bridge, handle);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _bridge.DestroyHandleAsync(Handle);
    }
}

/// <summary>A view into a GPU texture, used as a render target or shader resource.</summary>
public sealed class GpuTextureView : IAsyncDisposable
{
    private readonly JsBridge? _bridge;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuTextureView(int handle)
    {
        Handle = handle;
    }

    internal GpuTextureView(JsBridge bridge, int handle)
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

/// <summary>Flags indicating how a texture may be used.</summary>
[Flags]
public enum TextureUsage
{
    /// <summary>Texture can be used as the source of a copy operation.</summary>
    CopySource = 0x01,
    /// <summary>Texture can be used as the destination of a copy or write operation.</summary>
    CopyDest = 0x02,
    /// <summary>Texture can be used as a sampled texture in a shader.</summary>
    TextureBinding = 0x04,
    /// <summary>Texture can be used as a storage texture in a shader.</summary>
    StorageBinding = 0x08,
    /// <summary>Texture can be used as a render attachment (color or depth/stencil).</summary>
    RenderAttachment = 0x10,
}

/// <summary>Describes a GPU texture to create.</summary>
public sealed class TextureDescriptor
{
    /// <summary>Texture dimensions as [width, height] or [width, height, depth].</summary>
    public required int[] Size { get; init; }
    /// <summary>The texel format of the texture.</summary>
    public required TextureFormat Format { get; init; }
    /// <summary>How the texture will be used (combine flags with |).</summary>
    public required TextureUsage Usage { get; init; }
    /// <summary>Number of samples per texel (1 for non-MSAA, 4 for typical MSAA).</summary>
    public int SampleCount { get; init; } = 1;

    internal object ToJsObject() => new
    {
        size = Size,
        format = Format.ToJsString(),
        usage = (int)Usage,
        sampleCount = SampleCount,
    };
}
