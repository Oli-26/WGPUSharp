using WgpuSharp.Core;
using WgpuSharp.Interop;
using WgpuSharp.Resources;
using static WgpuSharp.Core.WebGpuEnumExtensions;

namespace WgpuSharp.Commands;

public sealed class GpuCommandEncoder
{
    private readonly JsBridge _bridge;
    internal int Handle { get; }

    internal GpuCommandEncoder(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        Handle = handle;
    }

    public async Task<GpuRenderPassEncoder> BeginRenderPassAsync(RenderPassDescriptor descriptor, CancellationToken ct = default)
    {
        object? depthStencilAttachment = descriptor.DepthStencilAttachment is not null
            ? new
            {
                viewId = descriptor.DepthStencilAttachment.View.Handle,
                depthClearValue = descriptor.DepthStencilAttachment.DepthClearValue,
                depthLoadOp = descriptor.DepthStencilAttachment.DepthLoadOp.ToJsString(),
                depthStoreOp = descriptor.DepthStencilAttachment.DepthStoreOp.ToJsString(),
            }
            : null;

        var jsDescriptor = new
        {
            colorAttachments = descriptor.ColorAttachments.Select(a => new
            {
                viewId = a.View.Handle,
                clearValue = new { r = a.ClearValue.R, g = a.ClearValue.G, b = a.ClearValue.B, a = a.ClearValue.A },
                loadOp = a.LoadOp.ToJsString(),
                storeOp = a.StoreOp.ToJsString(),
            }).ToArray(),
            depthStencilAttachment,
        };

        var handle = await _bridge.BeginRenderPassAsync(Handle, jsDescriptor, ct);
        return new GpuRenderPassEncoder(_bridge, handle);
    }

    public async Task<GpuComputePassEncoder> BeginComputePassAsync(CancellationToken ct = default)
    {
        var handle = await _bridge.BeginComputePassAsync(Handle, ct);
        return new GpuComputePassEncoder(_bridge, handle);
    }

    public async Task CopyBufferToBufferAsync(GpuBuffer source, long sourceOffset, GpuBuffer destination, long destinationOffset, long size, CancellationToken ct = default)
    {
        await _bridge.CopyBufferToBufferAsync(Handle, source.Handle, sourceOffset, destination.Handle, destinationOffset, size, ct);
    }

    public async Task<GpuCommandBuffer> FinishAsync(CancellationToken ct = default)
    {
        var handle = await _bridge.FinishEncoderAsync(Handle, ct);
        return new GpuCommandBuffer(handle);
    }
}

public sealed class RenderPassDescriptor
{
    public required ColorAttachment[] ColorAttachments { get; init; }
    public DepthStencilAttachment? DepthStencilAttachment { get; init; }
}

public sealed class ColorAttachment
{
    public required GpuTextureView View { get; init; }
    public GpuColor ClearValue { get; init; } = new(0, 0, 0, 1);
    public LoadOp LoadOp { get; init; } = LoadOp.Clear;
    public StoreOp StoreOp { get; init; } = StoreOp.Store;
}

public sealed class DepthStencilAttachment
{
    public required GpuTextureView View { get; init; }
    public float DepthClearValue { get; init; } = 1.0f;
    public LoadOp DepthLoadOp { get; init; } = LoadOp.Clear;
    public StoreOp DepthStoreOp { get; init; } = StoreOp.Store;
}

public readonly record struct GpuColor(double R, double G, double B, double A);
