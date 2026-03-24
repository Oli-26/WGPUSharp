using WgpuSharp.Interop;

namespace WgpuSharp.Core;

public sealed class GpuQueue
{
    private readonly JsBridge _bridge;
    private readonly int _deviceHandle;

    internal GpuQueue(JsBridge bridge, int deviceHandle)
    {
        _bridge = bridge;
        _deviceHandle = deviceHandle;
    }

    public async Task SubmitAsync(GpuCommandBuffer commandBuffer, CancellationToken ct = default)
    {
        await _bridge.SubmitAsync(_deviceHandle, [commandBuffer.Handle], ct);
    }

    public async Task SubmitAsync(GpuCommandBuffer[] commandBuffers, CancellationToken ct = default)
    {
        var handles = commandBuffers.Select(cb => cb.Handle).ToArray();
        await _bridge.SubmitAsync(_deviceHandle, handles, ct);
    }
}
