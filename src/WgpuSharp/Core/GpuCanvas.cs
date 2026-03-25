using WgpuSharp.Commands;
using WgpuSharp.Interop;
using WgpuSharp.Resources;

namespace WgpuSharp.Core;

/// <summary>
/// Wraps a canvas element's WebGPU context. Provides the render target for each frame.
/// Create via <see cref="ConfigureAsync"/>.
/// </summary>
public sealed class GpuCanvas
{
    private readonly JsBridge _bridge;
    private readonly GpuDevice _device;
    private readonly string _canvasId;
    private readonly string _formatString;
    internal int ContextHandle { get; }

    /// <summary>The preferred texture format for this canvas (typically Bgra8Unorm).</summary>
    public TextureFormat Format { get; }

    /// <summary>Current canvas width in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Current canvas height in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>Current aspect ratio (width / height).</summary>
    public float AspectRatio => Height > 0 ? (float)Width / Height : 1f;

    private GpuCanvas(JsBridge bridge, GpuDevice device, string canvasId, int contextHandle, string formatString, TextureFormat format, int width, int height)
    {
        _bridge = bridge;
        _device = device;
        _canvasId = canvasId;
        _formatString = formatString;
        ContextHandle = contextHandle;
        Format = format;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Configures a canvas element for WebGPU rendering.
    /// </summary>
    /// <param name="device">The GPU device.</param>
    /// <param name="canvasElementId">The HTML id of the canvas element.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<GpuCanvas> ConfigureAsync(GpuDevice device, string canvasElementId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(canvasElementId);
        var formatString = await device.Bridge.GetPreferredCanvasFormatAsync(ct);
        var format = WebGpuEnumExtensions.ParseTextureFormat(formatString);
        var handle = await device.Bridge.ConfigureCanvasAsync(device.Handle, canvasElementId, formatString, ct);
        var size = await device.Bridge.GetCanvasSizeAsync(canvasElementId, ct);
        return new GpuCanvas(device.Bridge, device, canvasElementId, handle, formatString, format, size.Width, size.Height);
    }

    /// <summary>Gets the current frame's texture view for rendering.</summary>
    public async Task<GpuTextureView> GetCurrentTextureViewAsync(CancellationToken ct = default)
    {
        var textureHandle = await _bridge.GetCurrentTextureAsync(ContextHandle, ct);
        var viewHandle = await _bridge.CreateTextureViewAsync(textureHandle, ct);
        return new GpuTextureView(viewHandle);
    }

    /// <summary>Queries the current canvas size from the DOM.</summary>
    public async Task<(int Width, int Height)> GetSizeAsync(CancellationToken ct = default)
    {
        var size = await _bridge.GetCanvasSizeAsync(_canvasId, ct);
        Width = size.Width;
        Height = size.Height;
        return (size.Width, size.Height);
    }

    /// <summary>Resizes the canvas element and reconfigures the WebGPU context.</summary>
    public async Task SetSizeAsync(int width, int height, CancellationToken ct = default)
    {
        await _bridge.SetCanvasSizeAsync(_canvasId, width, height, ct);
        await _bridge.ConfigureCanvasAsync(_device.Handle, _canvasId, _formatString, ct);
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Resizes the canvas to match its CSS display size (for responsive layouts).
    /// Returns true if the size changed and depth textures need recreation.
    /// Call this at the start of each frame for responsive rendering.
    /// </summary>
    public async Task<bool> ResizeToDisplaySizeAsync(CancellationToken ct = default)
    {
        var displaySize = await _bridge.GetCanvasDisplaySizeAsync(_canvasId, ct);
        if (displaySize.Width == Width && displaySize.Height == Height)
            return false;

        if (displaySize.Width <= 0 || displaySize.Height <= 0)
            return false;

        await SetSizeAsync(displaySize.Width, displaySize.Height, ct);
        return true;
    }

    /// <summary>
    /// Creates a multisample texture matching the current canvas size and format.
    /// Use with <see cref="Commands.RenderBatch.BeginRenderPass(GpuTextureView, BatchedTextureView, Commands.GpuColor, GpuTextureView?, LoadOp, StoreOp, LoadOp, StoreOp)"/>
    /// for MSAA rendering.
    /// </summary>
    /// <param name="device">The GPU device.</param>
    /// <param name="sampleCount">Number of samples per pixel. Typically 4.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<GpuTexture> CreateMsaaTextureAsync(GpuDevice device, int sampleCount = 4, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        return await device.CreateTextureAsync(new TextureDescriptor
        {
            Size = [Width, Height],
            Format = Format,
            Usage = TextureUsage.RenderAttachment,
            SampleCount = sampleCount,
        }, ct);
    }

    /// <summary>Creates a multisample depth texture matching the current canvas size.</summary>
    /// <param name="device">The GPU device.</param>
    /// <param name="sampleCount">Number of samples per pixel. Must match the MSAA color texture.</param>
    /// <param name="depthFormat">Depth texture format. Default: Depth24Plus.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<GpuTexture> CreateMsaaDepthTextureAsync(GpuDevice device, int sampleCount = 4, TextureFormat depthFormat = TextureFormat.Depth24Plus, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        return await device.CreateTextureAsync(new TextureDescriptor
        {
            Size = [Width, Height],
            Format = depthFormat,
            Usage = TextureUsage.RenderAttachment,
            SampleCount = sampleCount,
        }, ct);
    }

    /// <summary>Creates a depth texture matching the current canvas size.</summary>
    /// <param name="device">The GPU device.</param>
    /// <param name="depthFormat">Depth texture format. Default: Depth24Plus.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<GpuTexture> CreateDepthTextureAsync(GpuDevice device, TextureFormat depthFormat = TextureFormat.Depth24Plus, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        return await device.CreateTextureAsync(new TextureDescriptor
        {
            Size = [Width, Height],
            Format = depthFormat,
            Usage = TextureUsage.RenderAttachment,
        }, ct);
    }
}
