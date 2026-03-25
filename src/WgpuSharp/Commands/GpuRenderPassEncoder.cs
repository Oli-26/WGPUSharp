using WgpuSharp.Interop;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Commands;

/// <summary>Encodes draw commands within a render pass.</summary>
public sealed class GpuRenderPassEncoder
{
    private readonly JsBridge _bridge;
    private readonly int _handle;

    internal GpuRenderPassEncoder(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        _handle = handle;
    }

    /// <summary>Sets the render pipeline to use for subsequent draw calls.</summary>
    public async Task SetPipelineAsync(GpuRenderPipeline pipeline, CancellationToken ct = default)
    {
        await _bridge.SetPipelineAsync(_handle, pipeline.Handle, ct);
    }

    /// <summary>Binds a vertex buffer to the given slot.</summary>
    public async Task SetVertexBufferAsync(int slot, GpuBuffer buffer, CancellationToken ct = default)
    {
        await _bridge.SetVertexBufferAsync(_handle, slot, buffer.Handle, ct);
    }

    /// <summary>Binds an index buffer with the specified index format.</summary>
    public async Task SetIndexBufferAsync(GpuBuffer buffer, IndexFormat format = IndexFormat.Uint16, CancellationToken ct = default)
    {
        await _bridge.SetIndexBufferAsync(_handle, buffer.Handle, format.ToJsString(), ct);
    }

    /// <summary>Binds a bind group at the given group index.</summary>
    public async Task SetBindGroupAsync(int groupIndex, GpuBindGroup bindGroup, CancellationToken ct = default)
    {
        await _bridge.SetBindGroupAsync(_handle, groupIndex, bindGroup.Handle, ct);
    }

    /// <summary>Draws non-indexed primitives.</summary>
    public async Task DrawAsync(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0, CancellationToken ct = default)
    {
        await _bridge.DrawAsync(_handle, vertexCount, instanceCount, firstVertex, firstInstance, ct);
    }

    /// <summary>Draws indexed primitives.</summary>
    public async Task DrawIndexedAsync(int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0, CancellationToken ct = default)
    {
        await _bridge.DrawIndexedAsync(_handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance, ct);
    }

    /// <summary>Draws primitives using parameters read from a GPU buffer.</summary>
    public async Task DrawIndirectAsync(GpuBuffer indirectBuffer, long indirectOffset = 0, CancellationToken ct = default)
    {
        await _bridge.DrawIndirectAsync(_handle, indirectBuffer.Handle, indirectOffset, ct);
    }

    /// <summary>Draws indexed primitives using parameters read from a GPU buffer.</summary>
    public async Task DrawIndexedIndirectAsync(GpuBuffer indirectBuffer, long indirectOffset = 0, CancellationToken ct = default)
    {
        await _bridge.DrawIndexedIndirectAsync(_handle, indirectBuffer.Handle, indirectOffset, ct);
    }

    /// <summary>Ends the render pass.</summary>
    public async Task EndAsync(CancellationToken ct = default)
    {
        await _bridge.EndPassAsync(_handle, ct);
    }
}

/// <summary>Index buffer element format.</summary>
public enum IndexFormat
{
    /// <summary>16-bit unsigned integer indices.</summary>
    Uint16,
    /// <summary>32-bit unsigned integer indices.</summary>
    Uint32,
}

/// <summary>Extension methods for converting <see cref="IndexFormat"/> to JS strings.</summary>
public static class IndexFormatExtensions
{
    public static string ToJsString(this IndexFormat format) => format switch
    {
        IndexFormat.Uint16 => "uint16",
        IndexFormat.Uint32 => "uint32",
        _ => "uint16",
    };
}
