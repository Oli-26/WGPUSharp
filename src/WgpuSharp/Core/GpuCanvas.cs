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
    private readonly string _formatString; // raw JS string for configureCanvas
    internal int ContextHandle { get; }
    /// <summary>The preferred texture format for this canvas (typically Bgra8Unorm).</summary>
    public TextureFormat Format { get; }

    private GpuCanvas(JsBridge bridge, GpuDevice device, string canvasId, int contextHandle, string formatString, TextureFormat format)
    {
        _bridge = bridge;
        _device = device;
        _canvasId = canvasId;
        _formatString = formatString;
        ContextHandle = contextHandle;
        Format = format;
    }

    public static async Task<GpuCanvas> ConfigureAsync(GpuDevice device, string canvasElementId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(canvasElementId);
        var formatString = await device.Bridge.GetPreferredCanvasFormatAsync(ct);
        var format = WebGpuEnumExtensions.ParseTextureFormat(formatString);
        var handle = await device.Bridge.ConfigureCanvasAsync(device.Handle, canvasElementId, formatString, ct);
        return new GpuCanvas(device.Bridge, device, canvasElementId, handle, formatString, format);
    }

    public async Task<GpuTextureView> GetCurrentTextureViewAsync(CancellationToken ct = default)
    {
        var textureHandle = await _bridge.GetCurrentTextureAsync(ContextHandle, ct);
        var viewHandle = await _bridge.CreateTextureViewAsync(textureHandle, ct);
        return new GpuTextureView(viewHandle);
    }

    public async Task<(int Width, int Height)> GetSizeAsync(CancellationToken ct = default)
    {
        var size = await _bridge.GetCanvasSizeAsync(_canvasId, ct);
        return (size.Width, size.Height);
    }

    public async Task SetSizeAsync(int width, int height, CancellationToken ct = default)
    {
        await _bridge.SetCanvasSizeAsync(_canvasId, width, height, ct);
        await _bridge.ConfigureCanvasAsync(_device.Handle, _canvasId, _formatString, ct);
    }

    public async Task<GpuTexture> CreateDepthTextureAsync(GpuDevice device, TextureFormat depthFormat = TextureFormat.Depth24Plus, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        var size = await GetSizeAsync(ct);
        return await device.CreateTextureAsync(new TextureDescriptor
        {
            Size = [size.Width, size.Height],
            Format = depthFormat,
            Usage = TextureUsage.RenderAttachment,
        }, ct);
    }
}
