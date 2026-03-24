using WgpuSharp.Interop;

namespace WgpuSharp.Core;

/// <summary>
/// Represents a GPU adapter (a physical or virtual GPU). Obtained via <see cref="Gpu.RequestAdapterAsync"/>.
/// Use this to request a <see cref="GpuDevice"/>.
/// </summary>
public sealed class GpuAdapter
{
    internal int Handle { get; }
    internal JsBridge Bridge { get; }

    internal GpuAdapter(JsBridge bridge, int handle)
    {
        Bridge = bridge;
        Handle = handle;
    }

    /// <summary>
    /// Requests a logical GPU device from this adapter. The device is the main object
    /// used to create buffers, textures, pipelines, and command encoders.
    /// </summary>
    public async Task<GpuDevice> RequestDeviceAsync(CancellationToken ct = default)
    {
        var handle = await Bridge.RequestDeviceAsync(Handle, ct);
        return new GpuDevice(Bridge, handle);
    }
}
