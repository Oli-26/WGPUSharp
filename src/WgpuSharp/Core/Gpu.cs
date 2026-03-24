using Microsoft.JSInterop;
using WgpuSharp.Interop;

namespace WgpuSharp.Core;

/// <summary>
/// Entry point for WgpuSharp. Use this to initialize WebGPU and start render loops.
/// </summary>
public static class Gpu
{
    /// <summary>
    /// Checks whether WebGPU is available in the current browser.
    /// Use this to show a fallback message before attempting to initialize.
    /// </summary>
    public static async Task<bool> IsSupportedAsync(IJSRuntime js, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        try
        {
            var bridge = new JsBridge(js);
            return await bridge.IsWebGpuSupportedAsync(ct);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Requests a GPU adapter from the browser. This is the first call to make when using WgpuSharp.
    /// Requires the WgpuSharp.js script to be loaded in index.html.
    /// </summary>
    /// <param name="js">The Blazor JS runtime, typically injected via <c>@inject IJSRuntime JS</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GPU adapter that can be used to request a device.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the WgpuSharp.js bridge script is not loaded.</exception>
    /// <exception cref="GpuException">Thrown when WebGPU is not supported or no adapter is available.</exception>
    public static async Task<GpuAdapter> RequestAdapterAsync(IJSRuntime js, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        var bridge = new JsBridge(js);
        try
        {
            var handle = await bridge.RequestAdapterAsync(ct);
            return new GpuAdapter(bridge, handle);
        }
        catch (JSException ex) when (ex.Message.Contains("WgpuSharp"))
        {
            throw new InvalidOperationException(
                "WgpuSharp JS bridge not found. Add this to your index.html before the Blazor script:\n" +
                "  <script src=\"_content/WgpuSharp/WgpuSharp.js\"></script>",
                ex);
        }
        catch (JSException ex) when (ex.Message.Contains("WebGPU") || ex.Message.Contains("adapter"))
        {
            throw new GpuException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Starts a requestAnimationFrame-based render loop. The callback is invoked every frame
    /// with timing information. Dispose the returned <see cref="GpuLoop"/> to stop.
    /// </summary>
    /// <param name="device">The GPU device to associate with the loop.</param>
    /// <param name="onFrame">Async callback invoked each frame with delta time and FPS info.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<GpuLoop> RunLoopAsync(GpuDevice device, Func<FrameInfo, Task> onFrame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(onFrame);
        return await GpuLoop.StartAsync(device.Bridge, onFrame, ct);
    }
}
