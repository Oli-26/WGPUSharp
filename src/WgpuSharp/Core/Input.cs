using WgpuSharp.Interop;

namespace WgpuSharp.Core;

/// <summary>
/// Snapshot of keyboard and mouse state for the current frame.
/// Key codes use the browser <c>KeyboardEvent.code</c> format (e.g. "KeyW", "Space", "ShiftLeft").
/// </summary>
public sealed class InputState
{
    /// <summary>Array of currently pressed key codes.</summary>
    public string[] Keys { get; set; } = [];
    /// <summary>Mouse X position relative to the canvas.</summary>
    public float MouseX { get; set; }
    /// <summary>Mouse Y position relative to the canvas.</summary>
    public float MouseY { get; set; }
    /// <summary>Mouse X movement since last frame (only meaningful when pointer is locked).</summary>
    public float MouseDX { get; set; }
    /// <summary>Mouse Y movement since last frame (only meaningful when pointer is locked).</summary>
    public float MouseDY { get; set; }
    /// <summary>Bitmask of pressed mouse buttons (bit 0 = left, bit 1 = middle, bit 2 = right).</summary>
    public int MouseButtons { get; set; }
    /// <summary>Scroll wheel delta accumulated this frame (positive = scroll down).</summary>
    public float WheelDelta { get; set; }
    /// <summary>Whether the mouse pointer is locked to the canvas (for FPS-style controls).</summary>
    public bool PointerLocked { get; set; }

    /// <summary>Returns true if the given key is currently pressed. Use browser key codes like "KeyW", "Space".</summary>
    public bool IsKeyDown(string code) => Array.IndexOf(Keys, code) >= 0;
    /// <summary>Returns true if the given mouse button is pressed (0=left, 1=middle, 2=right).</summary>
    public bool IsMouseButtonDown(int button) => (MouseButtons & (1 << button)) != 0;
    /// <summary>Whether the left mouse button is pressed.</summary>
    public bool LeftMouseDown => IsMouseButtonDown(0);
    /// <summary>Whether the right mouse button is pressed.</summary>
    public bool RightMouseDown => IsMouseButtonDown(2);
}

/// <summary>
/// Handles keyboard and mouse input for a canvas element.
/// Click the canvas to lock the pointer (for FPS-style controls). Press Esc to release.
/// Initialize via <see cref="InitAsync"/> and poll with <see cref="GetStateAsync"/> each frame.
/// </summary>
public sealed class GpuInput : IAsyncDisposable
{
    private readonly JsBridge _bridge;

    private GpuInput(JsBridge bridge)
    {
        _bridge = bridge;
    }

    public static async Task<GpuInput> InitAsync(GpuDevice device, string canvasId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(canvasId);
        await device.Bridge.InitInputAsync(canvasId, ct);
        return new GpuInput(device.Bridge);
    }

    public async Task<InputState> GetStateAsync(CancellationToken ct = default)
    {
        return await _bridge.GetInputStateAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _bridge.DisposeInputAsync();
    }
}
