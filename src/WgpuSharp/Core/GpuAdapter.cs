using WgpuSharp.Interop;

namespace WgpuSharp.Core;

/// <summary>
/// Represents a GPU adapter (a physical or virtual GPU). Obtained via <see cref="Gpu.RequestAdapterAsync"/>.
/// Use this to request a <see cref="GpuDevice"/> or query GPU capabilities.
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

    /// <summary>
    /// Gets information about the GPU hardware (vendor, architecture, description).
    /// </summary>
    public async Task<GpuAdapterInfo> GetInfoAsync(CancellationToken ct = default)
    {
        return await Bridge.GetAdapterInfoAsync(Handle, ct);
    }

    /// <summary>
    /// Gets the list of WebGPU features supported by this adapter
    /// (e.g. "texture-compression-bc", "float32-filterable").
    /// </summary>
    public async Task<string[]> GetFeaturesAsync(CancellationToken ct = default)
    {
        return await Bridge.GetAdapterFeaturesAsync(Handle, ct);
    }

    /// <summary>
    /// Gets the hardware limits of this adapter (max texture size, buffer size, workgroup size, etc.).
    /// </summary>
    public async Task<GpuAdapterLimits> GetLimitsAsync(CancellationToken ct = default)
    {
        return await Bridge.GetAdapterLimitsAsync(Handle, ct);
    }
}
