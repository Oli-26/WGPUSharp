using WgpuSharp.Interop;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Commands;

public sealed class GpuRenderPassEncoder
{
    private readonly JsBridge _bridge;
    private readonly int _handle;

    internal GpuRenderPassEncoder(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        _handle = handle;
    }

    public async Task SetPipelineAsync(GpuRenderPipeline pipeline, CancellationToken ct = default)
    {
        await _bridge.SetPipelineAsync(_handle, pipeline.Handle, ct);
    }

    public async Task SetVertexBufferAsync(int slot, GpuBuffer buffer, CancellationToken ct = default)
    {
        await _bridge.SetVertexBufferAsync(_handle, slot, buffer.Handle, ct);
    }

    public async Task SetIndexBufferAsync(GpuBuffer buffer, IndexFormat format = IndexFormat.Uint16, CancellationToken ct = default)
    {
        await _bridge.SetIndexBufferAsync(_handle, buffer.Handle, format.ToJsString(), ct);
    }

    public async Task SetBindGroupAsync(int groupIndex, GpuBindGroup bindGroup, CancellationToken ct = default)
    {
        await _bridge.SetBindGroupAsync(_handle, groupIndex, bindGroup.Handle, ct);
    }

    public async Task DrawAsync(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0, CancellationToken ct = default)
    {
        await _bridge.DrawAsync(_handle, vertexCount, instanceCount, firstVertex, firstInstance, ct);
    }

    public async Task DrawIndexedAsync(int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0, CancellationToken ct = default)
    {
        await _bridge.DrawIndexedAsync(_handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance, ct);
    }

    public async Task EndAsync(CancellationToken ct = default)
    {
        await _bridge.EndPassAsync(_handle, ct);
    }
}

public enum IndexFormat
{
    Uint16,
    Uint32,
}

public static class IndexFormatExtensions
{
    public static string ToJsString(this IndexFormat format) => format switch
    {
        IndexFormat.Uint16 => "uint16",
        IndexFormat.Uint32 => "uint32",
        _ => "uint16",
    };
}
