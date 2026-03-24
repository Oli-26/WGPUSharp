using Microsoft.JSInterop;
using WgpuSharp.Interop;

namespace WgpuSharp.Core;

/// <summary>Timing information passed to each frame callback.</summary>
public sealed class FrameInfo
{
    /// <summary>The frame number since the loop started (0-based).</summary>
    public int FrameIndex { get; init; }
    /// <summary>Time in seconds since the last frame. Capped at 0.1s to prevent large jumps.</summary>
    public float DeltaTime { get; init; }
    /// <summary>Frames per second, updated every 0.5 seconds.</summary>
    public float Fps { get; init; }
}

/// <summary>
/// A requestAnimationFrame-based render loop. Dispose to stop.
/// Start via <see cref="Gpu.RunLoopAsync"/>.
/// </summary>
public sealed class GpuLoop : IAsyncDisposable
{
    private readonly JsBridge _bridge;
    private readonly DotNetObjectReference<GpuLoop> _dotNetRef;
    private Func<FrameInfo, Task>? _callback;
    private bool _running;

    // FPS tracking
    private float _fpsAccumulator;
    private int _fpsFrameCount;
    private float _currentFps;

    private GpuLoop(JsBridge bridge)
    {
        _bridge = bridge;
        _dotNetRef = DotNetObjectReference.Create(this);
    }

    internal static async Task<GpuLoop> StartAsync(JsBridge bridge, Func<FrameInfo, Task> onFrame, CancellationToken ct = default)
    {
        var loop = new GpuLoop(bridge);
        loop._callback = onFrame;
        loop._running = true;
        await bridge.StartLoopAsync(loop._dotNetRef, ct);
        return loop;
    }

    [JSInvokable]
    public async Task OnFrame(int frameIndex, float deltaTime)
    {
        if (!_running || _callback is null) return;

        // Update FPS every 0.5 seconds
        _fpsAccumulator += deltaTime;
        _fpsFrameCount++;
        if (_fpsAccumulator >= 0.5f)
        {
            _currentFps = _fpsFrameCount / _fpsAccumulator;
            _fpsAccumulator = 0;
            _fpsFrameCount = 0;
        }

        var frame = new FrameInfo
        {
            FrameIndex = frameIndex,
            DeltaTime = deltaTime,
            Fps = _currentFps,
        };

        await _callback(frame);
    }

    public async Task StopAsync()
    {
        _running = false;
        await _bridge.StopLoopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_running)
        {
            await StopAsync();
        }
        _dotNetRef.Dispose();
    }
}
