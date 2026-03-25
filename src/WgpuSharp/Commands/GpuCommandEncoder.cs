using WgpuSharp.Core;
using WgpuSharp.Interop;
using WgpuSharp.Resources;
using static WgpuSharp.Core.WebGpuEnumExtensions;

namespace WgpuSharp.Commands;

/// <summary>Encodes GPU commands (render passes, compute passes, copies) into a command buffer.</summary>
public sealed class GpuCommandEncoder
{
    private readonly JsBridge _bridge;
    internal int Handle { get; }

    internal GpuCommandEncoder(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        Handle = handle;
    }

    /// <summary>Begins a render pass with the given descriptor and returns the pass encoder.</summary>
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
            colorAttachments = descriptor.ColorAttachments.Select(a => (object)new
            {
                viewId = a.View.Handle,
                clearValue = new { r = a.ClearValue.R, g = a.ClearValue.G, b = a.ClearValue.B, a = a.ClearValue.A },
                loadOp = a.LoadOp.ToJsString(),
                storeOp = a.StoreOp.ToJsString(),
                resolveTargetId = a.ResolveTarget?.Handle,
            }).ToArray(),
            depthStencilAttachment,
        };

        var handle = await _bridge.BeginRenderPassAsync(Handle, jsDescriptor, ct);
        return new GpuRenderPassEncoder(_bridge, handle);
    }

    /// <summary>Begins a compute pass and returns the pass encoder.</summary>
    public async Task<GpuComputePassEncoder> BeginComputePassAsync(CancellationToken ct = default)
    {
        var handle = await _bridge.BeginComputePassAsync(Handle, ct);
        return new GpuComputePassEncoder(_bridge, handle);
    }

    /// <summary>Copies data from one GPU buffer to another.</summary>
    public async Task CopyBufferToBufferAsync(GpuBuffer source, long sourceOffset, GpuBuffer destination, long destinationOffset, long size, CancellationToken ct = default)
    {
        await _bridge.CopyBufferToBufferAsync(Handle, source.Handle, sourceOffset, destination.Handle, destinationOffset, size, ct);
    }

    /// <summary>Finishes encoding and returns the completed command buffer.</summary>
    public async Task<GpuCommandBuffer> FinishAsync(CancellationToken ct = default)
    {
        var handle = await _bridge.FinishEncoderAsync(Handle, ct);
        return new GpuCommandBuffer(handle);
    }
}

/// <summary>Describes a render pass, including color and optional depth/stencil attachments.</summary>
public sealed class RenderPassDescriptor
{
    /// <summary>The color attachments for this render pass.</summary>
    public required ColorAttachment[] ColorAttachments { get; init; }
    /// <summary>Optional depth/stencil attachment for this render pass.</summary>
    public DepthStencilAttachment? DepthStencilAttachment { get; init; }
}

/// <summary>A color attachment for a render pass.</summary>
public sealed class ColorAttachment
{
    /// <summary>The texture view to render into.</summary>
    public required GpuTextureView View { get; init; }
    /// <summary>The color to clear the attachment to when load op is Clear.</summary>
    public GpuColor ClearValue { get; init; } = new(0, 0, 0, 1);
    /// <summary>How the attachment is loaded at the start of the pass.</summary>
    public LoadOp LoadOp { get; init; } = LoadOp.Clear;
    /// <summary>How the attachment is stored at the end of the pass.</summary>
    public StoreOp StoreOp { get; init; } = StoreOp.Store;
    /// <summary>For MSAA: the resolve target texture view (the final non-multisampled output). Null for non-MSAA passes.</summary>
    public GpuTextureView? ResolveTarget { get; init; }
}

/// <summary>A depth/stencil attachment for a render pass.</summary>
public sealed class DepthStencilAttachment
{
    /// <summary>The depth/stencil texture view.</summary>
    public required GpuTextureView View { get; init; }
    /// <summary>The value to clear the depth buffer to when load op is Clear.</summary>
    public float DepthClearValue { get; init; } = 1.0f;
    /// <summary>How the depth attachment is loaded at the start of the pass.</summary>
    public LoadOp DepthLoadOp { get; init; } = LoadOp.Clear;
    /// <summary>How the depth attachment is stored at the end of the pass.</summary>
    public StoreOp DepthStoreOp { get; init; } = StoreOp.Store;
}

/// <summary>An RGBA color with double-precision components.</summary>
public readonly record struct GpuColor(double R, double G, double B, double A);
