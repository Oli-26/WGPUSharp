using WgpuSharp.Interop;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Commands;

public sealed class GpuComputePassEncoder
{
    private readonly JsBridge _bridge;
    private readonly int _handle;

    internal GpuComputePassEncoder(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        _handle = handle;
    }

    public async Task SetPipelineAsync(GpuComputePipeline pipeline, CancellationToken ct = default)
    {
        await _bridge.SetPipelineAsync(_handle, pipeline.Handle, ct);
    }

    public async Task SetBindGroupAsync(int groupIndex, GpuBindGroup bindGroup, CancellationToken ct = default)
    {
        await _bridge.SetBindGroupAsync(_handle, groupIndex, bindGroup.Handle, ct);
    }

    public async Task DispatchWorkgroupsAsync(int x, int y = 1, int z = 1, CancellationToken ct = default)
    {
        await _bridge.DispatchWorkgroupsAsync(_handle, x, y, z, ct);
    }

    public async Task EndAsync(CancellationToken ct = default)
    {
        await _bridge.EndPassAsync(_handle, ct);
    }
}
