using Microsoft.JSInterop;
using WgpuSharp.Commands;
using WgpuSharp.Interop;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Core;

/// <summary>
/// Information about a GPU device loss event.
/// </summary>
/// <param name="Reason">The reason the device was lost (e.g. "destroyed", "unknown").</param>
/// <param name="Message">A human-readable message describing why the device was lost.</param>
public record DeviceLostInfo(string Reason, string Message);

/// <summary>
/// The main GPU device. Use this to create all GPU resources: buffers, textures,
/// shader modules, pipelines, bind groups, and command encoders.
/// Obtained via <see cref="GpuAdapter.RequestDeviceAsync"/>.
/// </summary>
public sealed class GpuDevice : IAsyncDisposable
{
    internal int Handle { get; }
    internal JsBridge Bridge { get; }
    private DotNetObjectReference<GpuDevice>? _dotNetRef;
    private bool _disposed;

    /// <summary>The command queue for this device. Used to submit command buffers.</summary>
    public GpuQueue Queue { get; }

    /// <summary>
    /// Raised when the GPU device is lost (e.g. due to driver crash, tab backgrounding, or explicit destruction).
    /// Subscribe to this event to show recovery UI or clean up resources.
    /// </summary>
    public event Func<DeviceLostInfo, Task>? DeviceLost;

    internal GpuDevice(JsBridge bridge, int handle)
    {
        Bridge = bridge;
        Handle = handle;
        Queue = new GpuQueue(bridge, handle);
    }

    internal async Task RegisterDeviceLostCallbackAsync(CancellationToken ct = default)
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        await Bridge.RegisterDeviceLostCallbackAsync(Handle, _dotNetRef, ct);
    }

    /// <summary>Called from JS when the GPU device is lost. Do not call directly.</summary>
    [JSInvokable]
    public async Task OnDeviceLost(string reason, string message)
    {
        var handler = DeviceLost;
        if (handler is not null)
            await handler(new DeviceLostInfo(reason, message));
    }

    /// <summary>Disposes the device and releases the JS callback reference.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _dotNetRef?.Dispose();
        await Bridge.DestroyHandleAsync(Handle);
    }

    /// <summary>
    /// Compiles a WGSL shader into a GPU shader module.
    /// Throws <see cref="ShaderCompilationException"/> with line numbers if the shader has errors.
    /// </summary>
    /// <param name="wgslCode">The WGSL shader source code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ShaderCompilationException">Thrown when the shader contains compilation errors.</exception>
    public async Task<GpuShaderModule> CreateShaderModuleAsync(string wgslCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wgslCode);
        var handle = await Bridge.CreateShaderModuleAsync(Handle, wgslCode, ct);
        var module = new GpuShaderModule(Bridge, handle);

        // Check for compilation errors
        var info = await Bridge.GetShaderCompilationInfoAsync(handle, ct);
        var errors = info.Messages.Where(m => m.Type == "error").ToArray();
        if (errors.Length > 0)
        {
            var summary = $"Shader compilation failed with {errors.Length} error(s): {errors[0].Message}";
            if (errors[0].LineNum > 0)
                summary = $"Shader error at line {errors[0].LineNum}: {errors[0].Message}";
            throw new ShaderCompilationException(summary, info.Messages);
        }

        return module;
    }

    /// <summary>Creates a GPU buffer for vertex data, index data, uniforms, or storage.</summary>
    public async Task<GpuBuffer> CreateBufferAsync(BufferDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Size <= 0)
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Buffer size must be greater than 0.");
        var handle = await Bridge.CreateBufferAsync(Handle, descriptor.Size, (int)descriptor.Usage, descriptor.MappedAtCreation, ct);
        return new GpuBuffer(Bridge, Handle, handle);
    }

    /// <summary>Creates a GPU texture for sampling, rendering, or storage.</summary>
    public async Task<GpuTexture> CreateTextureAsync(TextureDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Size is not { Length: >= 1 })
            throw new ArgumentException("Texture size must have at least 1 dimension.", nameof(descriptor));
        var handle = await Bridge.CreateTextureAsync(Handle, descriptor.ToJsObject(), ct);
        return new GpuTexture(Bridge, handle);
    }

    /// <summary>Creates a texture sampler with filtering and address modes.</summary>
    public async Task<GpuSampler> CreateSamplerAsync(SamplerDescriptor? descriptor = null, CancellationToken ct = default)
    {
        var desc = descriptor ?? new SamplerDescriptor();
        var handle = await Bridge.CreateSamplerAsync(Handle, desc.ToJsObject(), ct);
        return new GpuSampler(Bridge, handle);
    }

    /// <summary>Creates a bind group that binds resources (buffers, textures, samplers) to a render pipeline.</summary>
    /// <param name="pipeline">The pipeline whose layout defines the bind group structure.</param>
    /// <param name="groupIndex">The <c>@group(N)</c> index in the shader.</param>
    /// <param name="entries">The resource bindings.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<GpuBindGroup> CreateBindGroupAsync(GpuRenderPipeline pipeline, int groupIndex, BindGroupEntry[] entries, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0)
            throw new ArgumentException("At least one bind group entry is required.", nameof(entries));
        var jsEntries = entries.Select(e => e.ToJsObject()).ToArray();
        var handle = await Bridge.CreateBindGroupAsync(Handle, pipeline.Handle, groupIndex, jsEntries, ct);
        return new GpuBindGroup(Bridge, handle);
    }

    /// <summary>Creates a bind group that binds resources to a compute pipeline.</summary>
    public async Task<GpuBindGroup> CreateBindGroupAsync(GpuComputePipeline pipeline, int groupIndex, BindGroupEntry[] entries, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0)
            throw new ArgumentException("At least one bind group entry is required.", nameof(entries));
        var jsEntries = entries.Select(e => e.ToJsObject()).ToArray();
        var handle = await Bridge.CreateComputeBindGroupAsync(Handle, pipeline.Handle, groupIndex, jsEntries, ct);
        return new GpuBindGroup(Bridge, handle);
    }

    /// <summary>Creates a render pipeline from vertex/fragment shaders and pipeline configuration.</summary>
    /// <exception cref="GpuException">Thrown when pipeline creation fails (e.g. incompatible shader/layout).</exception>
    public async Task<GpuRenderPipeline> CreateRenderPipelineAsync(RenderPipelineDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(descriptor.Vertex);
        ArgumentNullException.ThrowIfNull(descriptor.Fragment);
        try
        {
            var jsDescriptor = descriptor.ToJsObject();
            var handle = await Bridge.CreateRenderPipelineAsync(Handle, jsDescriptor, ct);
            return new GpuRenderPipeline(Bridge, handle);
        }
        catch (Microsoft.JSInterop.JSException ex)
        {
            throw new GpuException($"Failed to create render pipeline: {ex.Message}", ex);
        }
    }

    /// <summary>Creates a command encoder for recording GPU commands. For game loops, prefer <see cref="RenderBatch"/> instead.</summary>
    public async Task<GpuCommandEncoder> CreateCommandEncoderAsync(CancellationToken ct = default)
    {
        var handle = await Bridge.CreateCommandEncoderAsync(Handle, ct);
        return new GpuCommandEncoder(Bridge, handle);
    }

    /// <summary>Creates a compute pipeline for GPGPU workloads.</summary>
    /// <exception cref="GpuException">Thrown when pipeline creation fails.</exception>
    public async Task<GpuComputePipeline> CreateComputePipelineAsync(ComputePipelineDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(descriptor.Compute);
        try
        {
            var jsDescriptor = descriptor.ToJsObject();
            var handle = await Bridge.CreateComputePipelineAsync(Handle, jsDescriptor, ct);
            return new GpuComputePipeline(Bridge, handle);
        }
        catch (Microsoft.JSInterop.JSException ex)
        {
            throw new GpuException($"Failed to create compute pipeline: {ex.Message}", ex);
        }
    }

    /// <summary>Writes raw RGBA pixel data to a texture.</summary>
    public async Task WriteTextureAsync(GpuTexture texture, byte[] data, int width, int height, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(data);
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than 0.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than 0.");
        var base64 = Convert.ToBase64String(data);
        await Bridge.WriteTextureAsync(Handle, texture.Handle, base64, width, height, ct);
    }

    /// <summary>Creates a GPU texture from encoded image data (PNG, JPG). The browser decodes the image.</summary>
    public async Task<GpuTexture> CreateTextureFromImageAsync(byte[] imageData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        if (imageData.Length == 0)
            throw new ArgumentException("Image data cannot be empty.", nameof(imageData));
        var base64 = Convert.ToBase64String(imageData);
        var handle = await Bridge.CreateTextureFromImageAsync(Handle, base64, false, ct);
        return new GpuTexture(Bridge, handle);
    }
}
