using WgpuSharp.Interop;

namespace WgpuSharp.Resources;

/// <summary>A compiled WGSL shader module. Created via GpuDevice.CreateShaderModuleAsync.</summary>
public sealed class GpuShaderModule : IAsyncDisposable
{
    private readonly JsBridge? _bridge;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuShaderModule(int handle)
    {
        Handle = handle;
    }

    internal GpuShaderModule(JsBridge bridge, int handle)
    {
        _bridge = bridge;
        Handle = handle;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed || _bridge is null) return;
        _disposed = true;
        await _bridge.ReleaseHandleAsync(Handle);
    }
}
