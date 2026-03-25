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

/// <summary>Describes a compute pipeline including its shader module and entry point.</summary>
public sealed class ComputePipelineDescriptor
{
    /// <summary>The compute stage configuration for this pipeline.</summary>
    public required ComputeState Compute { get; init; }

    internal object ToJsObject() => new
    {
        moduleId = Compute.Module.Handle,
        entryPoint = Compute.EntryPoint,
    };
}

/// <summary>Describes the compute stage of a compute pipeline.</summary>
public sealed class ComputeState
{
    /// <summary>The shader module containing the compute entry point.</summary>
    public required GpuShaderModule Module { get; init; }
    /// <summary>The name of the compute shader entry point function.</summary>
    public required string EntryPoint { get; init; }
}
