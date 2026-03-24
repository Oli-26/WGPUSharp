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

[Flags]
public enum TextureUsage
{
    CopySource = 0x01,
    CopyDest = 0x02,
    TextureBinding = 0x04,
    StorageBinding = 0x08,
    RenderAttachment = 0x10,
}

/// <summary>Describes a GPU texture to create.</summary>
public sealed class TextureDescriptor
{
    public required int[] Size { get; init; }
    public required TextureFormat Format { get; init; }
    public required TextureUsage Usage { get; init; }
    public int SampleCount { get; init; } = 1;

    internal object ToJsObject() => new
    {
        size = Size,
        format = Format.ToJsString(),
        usage = (int)Usage,
        sampleCount = SampleCount,
    };
}
