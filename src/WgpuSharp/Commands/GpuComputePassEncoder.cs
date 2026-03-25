using WgpuSharp.Interop;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Commands;

/// <summary>Encodes dispatch commands within a compute pass.</summary>
public sealed class GpuComputePassEncoder
{
    private readonly JsBridge _bridge;
    private readonly int _handle;

    internal GpuComputePassEncoder(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        _handle = handle;
    }

    /// <summary>Sets the compute pipeline to use for subsequent dispatch calls.</summary>
    public async Task SetPipelineAsync(GpuComputePipeline pipeline, CancellationToken ct = default)
    {
        await _bridge.SetPipelineAsync(_handle, pipeline.Handle, ct);
    }

    /// <summary>Binds a bind group at the given group index.</summary>
    public async Task SetBindGroupAsync(int groupIndex, GpuBindGroup bindGroup, CancellationToken ct = default)
    {
        await _bridge.SetBindGroupAsync(_handle, groupIndex, bindGroup.Handle, ct);
    }

    /// <summary>Dispatches compute work with the specified workgroup counts.</summary>
    public async Task DispatchWorkgroupsAsync(int x, int y = 1, int z = 1, CancellationToken ct = default)
    {
        await _bridge.DispatchWorkgroupsAsync(_handle, x, y, z, ct);
    }

    /// <summary>Ends the compute pass.</summary>
    public async Task EndAsync(CancellationToken ct = default)
    {
        await _bridge.EndPassAsync(_handle, ct);
    }
}
