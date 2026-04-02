using System.Runtime.CompilerServices;
using WgpuSharp.Core;
using WgpuSharp.Interop;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Commands;

/// <summary>
/// Batches an entire frame's GPU commands into a single JS interop call.
/// Use this in your render loop instead of individual async calls for much better performance.
/// </summary>
public sealed class RenderBatch
{
    private readonly CommandBatch _batch = new();
    private readonly GpuDevice _device;

    public RenderBatch(GpuDevice device)
    {
        _device = device;
    }

    /// <summary>Reset the batch for reuse next frame (avoids per-frame allocation).</summary>
    public void Reset() => _batch.Reset();

    /// <summary>
    /// Write data to a buffer (queued, executes with the batch).
    /// </summary>
    public void WriteBuffer(GpuBuffer buffer, byte[] data)
    {
        _batch.WriteBuffer(_device.Handle, buffer.Handle, data);
    }

    /// <summary>
    /// Write float data to a buffer (queued, executes with the batch).
    /// </summary>
    public void WriteBuffer(GpuBuffer buffer, float[] data)
    {
        _batch.WriteBuffer(_device.Handle, buffer.Handle, data, data.Length);
    }

    /// <summary>
    /// Write a portion of float data to a buffer (queued, executes with the batch).
    /// </summary>
    public void WriteBuffer(GpuBuffer buffer, float[] data, int floatCount)
    {
        _batch.WriteBuffer(_device.Handle, buffer.Handle, data, floatCount);
    }

    /// <summary>
    /// Write an array of unmanaged structs to a buffer (queued, executes with the batch).
    /// </summary>
    public void WriteBuffer<T>(GpuBuffer buffer, T[] data) where T : unmanaged
    {
        _batch.WriteBuffer(_device.Handle, buffer.Handle, BufferDataHelper.ToBytes(data));
    }

    /// <summary>
    /// Write a single unmanaged struct to a buffer (queued, executes with the batch).
    /// </summary>
    public void WriteBuffer<T>(GpuBuffer buffer, T value) where T : unmanaged
    {
        _batch.WriteBuffer(_device.Handle, buffer.Handle, BufferDataHelper.ToBytes(value));
    }

    /// <summary>
    /// Get the current texture view from a canvas for rendering.
    /// Returns a batched handle reference.
    /// </summary>
    public BatchedTextureView GetCurrentTextureView(GpuCanvas canvas)
    {
        int texRef = _batch.GetCurrentTexture(canvas.ContextHandle);
        int viewRef = _batch.CreateTextureView(texRef);
        return new BatchedTextureView(viewRef);
    }

    /// <summary>
    /// Begin encoding a render pass within this batch.
    /// Uses <see cref="LoadOp.Clear"/> and <see cref="StoreOp.Store"/> by default.
    /// </summary>
    /// <param name="colorView">The color render target from <see cref="GetCurrentTextureView"/>.</param>
    /// <param name="clearColor">The color to clear to.</param>
    /// <param name="depthView">Optional depth buffer view.</param>
    /// <param name="colorLoadOp">How to initialize the color attachment. Default: Clear.</param>
    /// <param name="colorStoreOp">What to do with the color attachment after the pass. Default: Store.</param>
    /// <param name="depthLoadOp">How to initialize the depth attachment. Default: Clear.</param>
    /// <param name="depthStoreOp">What to do with the depth attachment after the pass. Default: Store.</param>
    public BatchedRenderPass BeginRenderPass(BatchedTextureView colorView, GpuColor clearColor,
        GpuTextureView? depthView = null,
        LoadOp colorLoadOp = LoadOp.Clear, StoreOp colorStoreOp = StoreOp.Store,
        LoadOp depthLoadOp = LoadOp.Clear, StoreOp depthStoreOp = StoreOp.Store)
    {
        int encoderRef = _batch.CreateCommandEncoder(_device.Handle);

        var colorAttachments = BuildColorAttachments(colorView.Ref, clearColor, colorLoadOp, colorStoreOp);
        var depthAttachment = BuildDepthAttachment(depthView, depthLoadOp, depthStoreOp);

        int passRef = _batch.BeginRenderPass(encoderRef, colorAttachments, depthAttachment);
        return new BatchedRenderPass(_batch, _device.Handle, encoderRef, passRef);
    }

    /// <summary>
    /// Begin a render pass targeting a user-created texture (render-to-texture).
    /// Use this for shadow maps, post-processing, offscreen rendering, etc.
    /// </summary>
    /// <param name="colorView">A texture view to render into (created from a texture with RenderAttachment usage).</param>
    /// <param name="clearColor">The color to clear to.</param>
    /// <param name="depthView">Optional depth buffer view.</param>
    /// <param name="colorLoadOp">How to initialize the color attachment. Default: Clear.</param>
    /// <param name="colorStoreOp">What to do with the color attachment after the pass. Default: Store.</param>
    /// <param name="depthLoadOp">How to initialize the depth attachment. Default: Clear.</param>
    /// <param name="depthStoreOp">What to do with the depth attachment after the pass. Default: Store.</param>
    public BatchedRenderPass BeginRenderPass(GpuTextureView colorView, GpuColor clearColor,
        GpuTextureView? depthView = null,
        LoadOp colorLoadOp = LoadOp.Clear, StoreOp colorStoreOp = StoreOp.Store,
        LoadOp depthLoadOp = LoadOp.Clear, StoreOp depthStoreOp = StoreOp.Store)
    {
        int encoderRef = _batch.CreateCommandEncoder(_device.Handle);

        var colorAttachments = BuildColorAttachments(colorView.Handle, clearColor, colorLoadOp, colorStoreOp);
        var depthAttachment = BuildDepthAttachment(depthView, depthLoadOp, depthStoreOp);

        int passRef = _batch.BeginRenderPass(encoderRef, colorAttachments, depthAttachment);
        return new BatchedRenderPass(_batch, _device.Handle, encoderRef, passRef);
    }

    /// <summary>
    /// Begin encoding a compute pass within this batch.
    /// </summary>
    public BatchedComputePass BeginComputePass()
    {
        int encoderRef = _batch.CreateCommandEncoder(_device.Handle);
        int passRef = _batch.BeginComputePass(encoderRef);
        return new BatchedComputePass(_batch, _device.Handle, encoderRef, passRef);
    }

    /// <summary>
    /// Create a command encoder that supports multiple passes (compute + render)
    /// on the same encoder before submitting.
    /// </summary>
    public BatchedEncoder CreateEncoder()
    {
        int encoderRef = _batch.CreateCommandEncoder(_device.Handle);
        return new BatchedEncoder(_batch, _device.Handle, encoderRef);
    }

    /// <summary>
    /// Begin an MSAA render pass: render into the multisample texture, resolve to the canvas.
    /// </summary>
    /// <param name="msaaView">The multisample texture view to render into (created from a 4x sample texture).</param>
    /// <param name="resolveTarget">The canvas texture view (from <see cref="GetCurrentTextureView"/>) to resolve the MSAA result into.</param>
    /// <param name="clearColor">The color to clear to.</param>
    /// <param name="depthView">Optional depth buffer view (must also be multisample if used).</param>
    /// <param name="colorLoadOp">How to initialize the color attachment. Default: Clear.</param>
    /// <param name="colorStoreOp">What to do with the color attachment after the pass. Default: Store.</param>
    /// <param name="depthLoadOp">How to initialize the depth attachment. Default: Clear.</param>
    /// <param name="depthStoreOp">What to do with the depth attachment after the pass. Default: Store.</param>
    public BatchedRenderPass BeginRenderPass(GpuTextureView msaaView, BatchedTextureView resolveTarget, GpuColor clearColor,
        GpuTextureView? depthView = null,
        LoadOp colorLoadOp = LoadOp.Clear, StoreOp colorStoreOp = StoreOp.Store,
        LoadOp depthLoadOp = LoadOp.Clear, StoreOp depthStoreOp = StoreOp.Store)
    {
        int encoderRef = _batch.CreateCommandEncoder(_device.Handle);

        var colorAttachments = BuildColorAttachments(msaaView.Handle, clearColor, colorLoadOp, colorStoreOp, resolveTarget.Ref);
        var depthAttachment = BuildDepthAttachment(depthView, depthLoadOp, depthStoreOp);

        int passRef = _batch.BeginRenderPass(encoderRef, colorAttachments, depthAttachment);
        return new BatchedRenderPass(_batch, _device.Handle, encoderRef, passRef);
    }

    internal static object[] BuildColorAttachments(int viewRef, GpuColor clearColor, LoadOp loadOp, StoreOp storeOp, int? resolveTargetRef = null)
    {
        return
        [
            new
            {
                viewId = viewRef,
                clearValue = new { r = clearColor.R, g = clearColor.G, b = clearColor.B, a = clearColor.A },
                loadOp = loadOp.ToJsString(),
                storeOp = storeOp.ToJsString(),
                resolveTargetId = resolveTargetRef,
            }
        ];
    }

    internal static object? BuildDepthAttachment(GpuTextureView? depthView, LoadOp loadOp, StoreOp storeOp)
    {
        if (depthView is null) return null;
        return new
        {
            viewId = depthView.Handle,
            depthClearValue = 1.0f,
            depthLoadOp = loadOp.ToJsString(),
            depthStoreOp = storeOp.ToJsString(),
        };
    }

    /// <summary>
    /// Execute all queued commands in a single JS interop call.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_batch.CommandCount == 0) return;

        var writes = _batch.HasBufferWrites ? _batch.GetBufferWritesList() : null;
        await _device.Bridge.ExecuteBatchAsync(_batch.GetCommands(), writes, ct);
    }
}

/// <summary>A texture view reference within a batch (not yet resolved to a real handle).</summary>
public readonly struct BatchedTextureView
{
    internal int Ref { get; }
    internal BatchedTextureView(int @ref) => Ref = @ref;
}

/// <summary>A render pass within a batch. Commands are queued, not executed immediately.</summary>
public sealed class BatchedRenderPass
{
    private readonly CommandBatch _batch;
    private readonly int _deviceHandle;
    private readonly int _encoderRef;
    private readonly int _passRef;

    internal BatchedRenderPass(CommandBatch batch, int deviceHandle, int encoderRef, int passRef)
    {
        _batch = batch;
        _deviceHandle = deviceHandle;
        _encoderRef = encoderRef;
        _passRef = passRef;
    }

    public void SetPipeline(GpuRenderPipeline pipeline) =>
        _batch.SetPipeline(_passRef, pipeline.Handle);

    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup) =>
        _batch.SetBindGroup(_passRef, groupIndex, bindGroup.Handle);

    public void SetVertexBuffer(int slot, GpuBuffer buffer) =>
        _batch.SetVertexBuffer(_passRef, slot, buffer.Handle);

    public void SetIndexBuffer(GpuBuffer buffer, IndexFormat format = IndexFormat.Uint16) =>
        _batch.SetIndexBuffer(_passRef, buffer.Handle, format.ToJsString());

    public void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0) =>
        _batch.Draw(_passRef, vertexCount, instanceCount, firstVertex, firstInstance);

    public void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0) =>
        _batch.DrawIndexed(_passRef, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);

    /// <summary>Draws primitives using parameters read from a GPU buffer.</summary>
    public void DrawIndirect(GpuBuffer indirectBuffer, long indirectOffset = 0) =>
        _batch.DrawIndirect(_passRef, indirectBuffer.Handle, indirectOffset);

    /// <summary>Draws indexed primitives using parameters read from a GPU buffer.</summary>
    public void DrawIndexedIndirect(GpuBuffer indirectBuffer, long indirectOffset = 0) =>
        _batch.DrawIndexedIndirect(_passRef, indirectBuffer.Handle, indirectOffset);

    /// <summary>End the pass only (use with BatchedEncoder for multi-pass).</summary>
    public void End() => _batch.EndPass(_passRef);

    /// <summary>End the pass and submit the command buffer.</summary>
    public void EndAndSubmit()
    {
        _batch.EndPass(_passRef);
        int cmdBufRef = _batch.FinishEncoder(_encoderRef);
        _batch.Submit(_deviceHandle, cmdBufRef);
    }
}

/// <summary>A compute pass within a batch.</summary>
public sealed class BatchedComputePass
{
    private readonly CommandBatch _batch;
    private readonly int _deviceHandle;
    private readonly int _encoderRef;
    private readonly int _passRef;

    internal BatchedComputePass(CommandBatch batch, int deviceHandle, int encoderRef, int passRef)
    {
        _batch = batch;
        _deviceHandle = deviceHandle;
        _encoderRef = encoderRef;
        _passRef = passRef;
    }

    public void SetPipeline(GpuComputePipeline pipeline) =>
        _batch.SetPipeline(_passRef, pipeline.Handle);

    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup) =>
        _batch.SetBindGroup(_passRef, groupIndex, bindGroup.Handle);

    public void DispatchWorkgroups(int x, int y = 1, int z = 1) =>
        _batch.DispatchWorkgroups(_passRef, x, y, z);

    /// <summary>End the pass only (use with BatchedEncoder for multi-pass).</summary>
    public void End() => _batch.EndPass(_passRef);

    /// <summary>End the pass and submit the command buffer.</summary>
    public void EndAndSubmit()
    {
        _batch.EndPass(_passRef);
        int cmdBufRef = _batch.FinishEncoder(_encoderRef);
        _batch.Submit(_deviceHandle, cmdBufRef);
    }
}

/// <summary>
/// A command encoder that supports multiple passes before submitting.
/// Use for mixed compute + render workloads on a single encoder.
/// </summary>
public sealed class BatchedEncoder
{
    private readonly CommandBatch _batch;
    private readonly int _deviceHandle;
    private readonly int _encoderRef;

    internal BatchedEncoder(CommandBatch batch, int deviceHandle, int encoderRef)
    {
        _batch = batch;
        _deviceHandle = deviceHandle;
        _encoderRef = encoderRef;
    }

    public BatchedComputePass BeginComputePass()
    {
        int passRef = _batch.BeginComputePass(_encoderRef);
        return new BatchedComputePass(_batch, _deviceHandle, _encoderRef, passRef);
    }

    /// <summary>Begin a render pass on this encoder.</summary>
    public BatchedRenderPass BeginRenderPass(BatchedTextureView colorView, GpuColor clearColor,
        GpuTextureView? depthView = null,
        LoadOp colorLoadOp = LoadOp.Clear, StoreOp colorStoreOp = StoreOp.Store,
        LoadOp depthLoadOp = LoadOp.Clear, StoreOp depthStoreOp = StoreOp.Store)
    {
        var colorAttachments = RenderBatch.BuildColorAttachments(colorView.Ref, clearColor, colorLoadOp, colorStoreOp);
        var depthAttachment = RenderBatch.BuildDepthAttachment(depthView, depthLoadOp, depthStoreOp);

        int passRef = _batch.BeginRenderPass(_encoderRef, colorAttachments, depthAttachment);
        return new BatchedRenderPass(_batch, _deviceHandle, _encoderRef, passRef);
    }

    /// <summary>Begin a render pass targeting a user-created texture (render-to-texture).</summary>
    public BatchedRenderPass BeginRenderPass(GpuTextureView colorView, GpuColor clearColor,
        GpuTextureView? depthView = null,
        LoadOp colorLoadOp = LoadOp.Clear, StoreOp colorStoreOp = StoreOp.Store,
        LoadOp depthLoadOp = LoadOp.Clear, StoreOp depthStoreOp = StoreOp.Store)
    {
        var colorAttachments = RenderBatch.BuildColorAttachments(colorView.Handle, clearColor, colorLoadOp, colorStoreOp);
        var depthAttachment = RenderBatch.BuildDepthAttachment(depthView, depthLoadOp, depthStoreOp);

        int passRef = _batch.BeginRenderPass(_encoderRef, colorAttachments, depthAttachment);
        return new BatchedRenderPass(_batch, _deviceHandle, _encoderRef, passRef);
    }

    /// <summary>Begin an MSAA render pass on this encoder.</summary>
    public BatchedRenderPass BeginRenderPass(GpuTextureView msaaView, BatchedTextureView resolveTarget, GpuColor clearColor,
        GpuTextureView? depthView = null,
        LoadOp colorLoadOp = LoadOp.Clear, StoreOp colorStoreOp = StoreOp.Store,
        LoadOp depthLoadOp = LoadOp.Clear, StoreOp depthStoreOp = StoreOp.Store)
    {
        var colorAttachments = RenderBatch.BuildColorAttachments(msaaView.Handle, clearColor, colorLoadOp, colorStoreOp, resolveTarget.Ref);
        var depthAttachment = RenderBatch.BuildDepthAttachment(depthView, depthLoadOp, depthStoreOp);

        int passRef = _batch.BeginRenderPass(_encoderRef, colorAttachments, depthAttachment);
        return new BatchedRenderPass(_batch, _deviceHandle, _encoderRef, passRef);
    }

    /// <summary>Finish the encoder and submit the command buffer.</summary>
    public void Submit()
    {
        int cmdBufRef = _batch.FinishEncoder(_encoderRef);
        _batch.Submit(_deviceHandle, cmdBufRef);
    }
}
