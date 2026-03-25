using WgpuSharp.Core;
using WgpuSharp.Interop;

namespace WgpuSharp.Resources;

/// <summary>A texture sampler that controls filtering and address modes.</summary>
public sealed class GpuSampler : IAsyncDisposable
{
    private readonly JsBridge? _bridge;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuSampler(int handle)
    {
        Handle = handle;
    }

    internal GpuSampler(JsBridge bridge, int handle)
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

/// <summary>Describes a texture sampler to create.</summary>
public sealed class SamplerDescriptor
{
    /// <summary>Filtering mode when the texture is magnified.</summary>
    public FilterMode MagFilter { get; init; } = FilterMode.Linear;
    /// <summary>Filtering mode when the texture is minified.</summary>
    public FilterMode MinFilter { get; init; } = FilterMode.Linear;
    /// <summary>Texture address (wrap) mode for the U coordinate.</summary>
    public AddressMode AddressModeU { get; init; } = AddressMode.ClampToEdge;
    /// <summary>Texture address (wrap) mode for the V coordinate.</summary>
    public AddressMode AddressModeV { get; init; } = AddressMode.ClampToEdge;

    internal object ToJsObject() => new
    {
        magFilter = MagFilter.ToJsString(),
        minFilter = MinFilter.ToJsString(),
        addressModeU = AddressModeU.ToJsString(),
        addressModeV = AddressModeV.ToJsString(),
    };
}
