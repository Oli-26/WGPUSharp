using Microsoft.JSInterop;

namespace WgpuMaui.Interop;

internal sealed class JsBridge
{
    private readonly IJSRuntime _js;

    public JsBridge(IJSRuntime js)
    {
        _js = js;
    }

    // Availability
    public ValueTask<bool> IsWebGpuSupportedAsync(CancellationToken ct = default)
        => _js.InvokeAsync<bool>("WgpuMaui.isWebGpuSupported", ct);

    // Adapter
    public ValueTask<int> RequestAdapterAsync(bool preferHighPerformance = false, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.requestAdapter", ct, preferHighPerformance);

    public ValueTask<Core.GpuAdapterInfo> GetAdapterInfoAsync(int adapterId, CancellationToken ct = default)
        => _js.InvokeAsync<Core.GpuAdapterInfo>("WgpuMaui.getAdapterInfo", ct, adapterId);

    public ValueTask<string[]> GetAdapterFeaturesAsync(int adapterId, CancellationToken ct = default)
        => _js.InvokeAsync<string[]>("WgpuMaui.getAdapterFeatures", ct, adapterId);

    public ValueTask<Core.GpuAdapterLimits> GetAdapterLimitsAsync(int adapterId, CancellationToken ct = default)
        => _js.InvokeAsync<Core.GpuAdapterLimits>("WgpuMaui.getAdapterLimits", ct, adapterId);

    // Device
    public ValueTask<int> RequestDeviceAsync(int adapterId, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.requestDevice", ct, adapterId);

    public ValueTask RegisterDeviceLostCallbackAsync(int deviceId, object dotNetRef, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.registerDeviceLostCallback", ct, deviceId, dotNetRef);

    // Canvas
    public ValueTask<int> ConfigureCanvasAsync(int deviceId, string canvasId, string format, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.configureCanvas", ct, deviceId, canvasId, format);

    public ValueTask<string> GetPreferredCanvasFormatAsync(CancellationToken ct = default)
        => _js.InvokeAsync<string>("WgpuMaui.getPreferredCanvasFormat", ct);

    public ValueTask<CanvasSize> GetCanvasSizeAsync(string canvasId, CancellationToken ct = default)
        => _js.InvokeAsync<CanvasSize>("WgpuMaui.getCanvasSize", ct, canvasId);

    public ValueTask<CanvasSize> GetCanvasDisplaySizeAsync(string canvasId, CancellationToken ct = default)
        => _js.InvokeAsync<CanvasSize>("WgpuMaui.getCanvasDisplaySize", ct, canvasId);

    public ValueTask SetCanvasSizeAsync(string canvasId, int width, int height, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.setCanvasSize", ct, canvasId, width, height);

    public ValueTask<int> GetCurrentTextureAsync(int contextId, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.getCurrentTexture", ct, contextId);

    public ValueTask<CanvasSize> GetTextureSizeAsync(int textureId, CancellationToken ct = default)
        => _js.InvokeAsync<CanvasSize>("WgpuMaui.getTextureSize", ct, textureId);

    public ValueTask<int> CreateTextureViewAsync(int textureId, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createTextureView", ct, textureId);

    // Shader
    public ValueTask<int> CreateShaderModuleAsync(int deviceId, string wgslCode, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createShaderModule", ct, deviceId, wgslCode);

    public ValueTask<Core.ShaderCompilationInfo> GetShaderCompilationInfoAsync(int moduleId, CancellationToken ct = default)
        => _js.InvokeAsync<Core.ShaderCompilationInfo>("WgpuMaui.getShaderCompilationInfo", ct, moduleId);

    // Buffer
    public ValueTask<int> CreateBufferAsync(int deviceId, long size, int usage, bool mappedAtCreation = false, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createBuffer", ct, deviceId, size, usage, mappedAtCreation);

    public ValueTask WriteBufferAsync(int deviceId, int bufferId, string dataBase64, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.writeBuffer", ct, deviceId, bufferId, dataBase64);

    // Texture
    public ValueTask<int> CreateTextureAsync(int deviceId, object descriptor, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createTexture", ct, deviceId, descriptor);

    public ValueTask<int> CreateTextureViewWithDescriptorAsync(int textureId, object? descriptor = null, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createTextureViewWithDescriptor", ct, textureId, descriptor);

    public ValueTask WriteTextureAsync(int deviceId, int textureId, string dataBase64, int width, int height, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.writeTexture", ct, deviceId, textureId, dataBase64, width, height);

    // Texture from image
    public ValueTask<int> CreateTextureFromImageAsync(int deviceId, string imageBase64, bool generateMipmaps = false, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createTextureFromImage", ct, deviceId, imageBase64, generateMipmaps);

    // Sampler
    public ValueTask<int> CreateSamplerAsync(int deviceId, object descriptor, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createSampler", ct, deviceId, descriptor);

    // Bind group
    public ValueTask<int> CreateBindGroupAsync(int deviceId, int pipelineId, int groupIndex, object[] entries, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createBindGroup", ct, deviceId, pipelineId, groupIndex, entries);

    public ValueTask SetBindGroupAsync(int passId, int groupIndex, int bindGroupId, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.setBindGroup", ct, passId, groupIndex, bindGroupId);

    // Render pipeline
    public ValueTask<int> CreateRenderPipelineAsync(int deviceId, object descriptor, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createRenderPipeline", ct, deviceId, descriptor);

    // Command encoder
    public ValueTask<int> CreateCommandEncoderAsync(int deviceId, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createCommandEncoder", ct, deviceId);

    public ValueTask<int> BeginRenderPassAsync(int encoderId, object descriptor, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.beginRenderPass", ct, encoderId, descriptor);

    public ValueTask SetPipelineAsync(int passId, int pipelineId, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.setPipeline", ct, passId, pipelineId);

    public ValueTask SetVertexBufferAsync(int passId, int slot, int bufferId, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.setVertexBuffer", ct, passId, slot, bufferId);

    public ValueTask SetIndexBufferAsync(int passId, int bufferId, string format, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.setIndexBuffer", ct, passId, bufferId, format);

    public ValueTask DrawAsync(int passId, int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.draw", ct, passId, vertexCount, instanceCount, firstVertex, firstInstance);

    public ValueTask DrawIndexedAsync(int passId, int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.drawIndexed", ct, passId, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);

    public ValueTask DrawIndirectAsync(int passId, int bufferId, long indirectOffset, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.drawIndirect", ct, passId, bufferId, indirectOffset);

    public ValueTask DrawIndexedIndirectAsync(int passId, int bufferId, long indirectOffset, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.drawIndexedIndirect", ct, passId, bufferId, indirectOffset);

    public ValueTask EndPassAsync(int passId, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.endPass", ct, passId);

    public ValueTask<int> FinishEncoderAsync(int encoderId, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.finishEncoder", ct, encoderId);

    public ValueTask SubmitAsync(int deviceId, int[] commandBufferIds, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.submit", ct, deviceId, commandBufferIds);

    // Compute pipeline
    public ValueTask<int> CreateComputePipelineAsync(int deviceId, object descriptor, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createComputePipeline", ct, deviceId, descriptor);

    public ValueTask<int> BeginComputePassAsync(int encoderId, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.beginComputePass", ct, encoderId);

    public ValueTask DispatchWorkgroupsAsync(int passId, int x, int y = 1, int z = 1, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.dispatchWorkgroups", ct, passId, x, y, z);

    public ValueTask CopyBufferToBufferAsync(int encoderId, int srcId, long srcOffset, int dstId, long dstOffset, long size, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.copyBufferToBuffer", ct, encoderId, srcId, srcOffset, dstId, dstOffset, size);

    public ValueTask<string> MapReadBufferAsync(int bufferId, CancellationToken ct = default)
        => _js.InvokeAsync<string>("WgpuMaui.mapReadBuffer", ct, bufferId);

    public ValueTask<int> CreateComputeBindGroupAsync(int deviceId, int pipelineId, int groupIndex, object[] entries, CancellationToken ct = default)
        => _js.InvokeAsync<int>("WgpuMaui.createComputeBindGroup", ct, deviceId, pipelineId, groupIndex, entries);

    // GpuLoop
    public ValueTask StartLoopAsync(object dotNetRef, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.startLoop", ct, dotNetRef);

    public ValueTask StopLoopAsync(CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.stopLoop", ct);

    // Batched execution
    public ValueTask<int[]> ExecuteBatchAsync(object commands, object? bufferWrites = null, CancellationToken ct = default)
        => _js.InvokeAsync<int[]>("WgpuMaui.executeBatch", ct, commands, bufferWrites);

    // Input
    public ValueTask InitInputAsync(string canvasId, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.initInput", ct, canvasId);

    public async ValueTask<Core.InputState> GetInputStateAsync(CancellationToken ct = default)
        => await _js.InvokeAsync<Core.InputState>("WgpuMaui.getInputState", ct);

    public ValueTask DisposeInputAsync(CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.disposeInput", ct);

    // Cleanup
    public ValueTask DestroyHandleAsync(int id, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.destroyHandle", ct, id);

    public ValueTask ReleaseHandleAsync(int id, CancellationToken ct = default)
        => _js.InvokeVoidAsync("WgpuMaui.releaseHandle", ct, id);
}

public record struct CanvasSize(int Width, int Height);
