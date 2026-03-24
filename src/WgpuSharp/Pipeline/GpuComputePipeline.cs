using WgpuSharp.Interop;
using WgpuSharp.Resources;

namespace WgpuSharp.Pipeline;

/// <summary>A GPU compute pipeline for GPGPU workloads.</summary>
public sealed class GpuComputePipeline : IAsyncDisposable
{
    private readonly JsBridge? _bridge;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuComputePipeline(int handle)
    {
        Handle = handle;
    }

    internal GpuComputePipeline(JsBridge bridge, int handle)
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

public sealed class ComputePipelineDescriptor
{
    public required ComputeState Compute { get; init; }

    internal object ToJsObject() => new
    {
        moduleId = Compute.Module.Handle,
        entryPoint = Compute.EntryPoint,
    };
}

public sealed class ComputeState
{
    public required GpuShaderModule Module { get; init; }
    public required string EntryPoint { get; init; }
}
