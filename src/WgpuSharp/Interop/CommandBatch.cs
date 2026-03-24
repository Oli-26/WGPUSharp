namespace WgpuSharp.Interop;

/// <summary>
/// Batches GPU commands into a single JS interop call.
/// Commands that create handles return negative placeholder IDs
/// which are resolved on the JS side during execution.
/// </summary>
internal sealed class CommandBatch
{
    private readonly List<object[]> _commands = new();
    private readonly List<BufferWrite> _bufferWrites = new();
    private int _nextSlot;
    private int _writeKey;

    /// <summary>Returns a negative placeholder ID referencing a result slot.</summary>
    private int AllocSlot()
    {
        int slot = _nextSlot++;
        return -(slot + 1); // negative = reference to result slot
    }

    private int SlotIndex(int placeholderId) => -(placeholderId + 1);

    // Op 0: getCurrentTexture(contextId) -> textureId
    public int GetCurrentTexture(int contextId)
    {
        int ph = AllocSlot();
        _commands.Add([0, SlotIndex(ph), contextId]);
        return ph;
    }

    // Op 1: createTextureView(textureId) -> viewId
    public int CreateTextureView(int textureIdOrRef)
    {
        int ph = AllocSlot();
        _commands.Add([1, SlotIndex(ph), textureIdOrRef]);
        return ph;
    }

    // Op 2: createCommandEncoder(deviceId) -> encoderId
    public int CreateCommandEncoder(int deviceId)
    {
        int ph = AllocSlot();
        _commands.Add([2, SlotIndex(ph), deviceId]);
        return ph;
    }

    // Op 3: beginRenderPass(encoderId, colorAttachments, depthAttachment)
    public int BeginRenderPass(int encoderIdOrRef, object[] colorAttachments, object? depthAttachment)
    {
        int ph = AllocSlot();
        _commands.Add([3, SlotIndex(ph), encoderIdOrRef, colorAttachments, depthAttachment!]);
        return ph;
    }

    // Op 4: setPipeline(passId, pipelineId)
    public void SetPipeline(int passIdOrRef, int pipelineId)
    {
        _commands.Add([4, -1, passIdOrRef, pipelineId]);
    }

    // Op 5: setBindGroup(passId, groupIndex, bindGroupId)
    public void SetBindGroup(int passIdOrRef, int groupIndex, int bindGroupId)
    {
        _commands.Add([5, -1, passIdOrRef, groupIndex, bindGroupId]);
    }

    // Op 6: setVertexBuffer(passId, slot, bufferId)
    public void SetVertexBuffer(int passIdOrRef, int slot, int bufferId)
    {
        _commands.Add([6, -1, passIdOrRef, slot, bufferId]);
    }

    // Op 7: setIndexBuffer(passId, bufferId, format)
    public void SetIndexBuffer(int passIdOrRef, int bufferIdOrRef, string format)
    {
        _commands.Add([7, -1, passIdOrRef, bufferIdOrRef, format]);
    }

    // Op 8: draw(passId, vertexCount, instanceCount, firstVertex, firstInstance)
    public void Draw(int passIdOrRef, int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        _commands.Add([8, -1, passIdOrRef, vertexCount, instanceCount, firstVertex, firstInstance]);
    }

    // Op 9: drawIndexed(passId, indexCount, instanceCount, firstIndex, baseVertex, firstInstance)
    public void DrawIndexed(int passIdOrRef, int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0)
    {
        _commands.Add([9, -1, passIdOrRef, indexCount, instanceCount, firstIndex, baseVertex, firstInstance]);
    }

    // Op 10: endPass(passId)
    public void EndPass(int passIdOrRef)
    {
        _commands.Add([10, -1, passIdOrRef]);
    }

    // Op 11: finishEncoder(encoderId) -> commandBufferId
    public int FinishEncoder(int encoderIdOrRef)
    {
        int ph = AllocSlot();
        _commands.Add([11, SlotIndex(ph), encoderIdOrRef]);
        return ph;
    }

    // Op 12: submit(deviceId, commandBufferIds)
    public void Submit(int deviceId, params int[] commandBufferIdsOrRefs)
    {
        _commands.Add([12, -1, deviceId, commandBufferIdsOrRefs]);
    }

    // Op 13: writeBuffer(deviceId, bufferId, data)
    public void WriteBuffer(int deviceId, int bufferId, byte[] data)
    {
        var key = $"w{_writeKey++}";
        _bufferWrites.Add(new BufferWrite { Key = key, Data = Convert.ToBase64String(data) });
        _commands.Add([13, -1, deviceId, bufferId, key]);
    }

    public void WriteBuffer(int deviceId, int bufferId, float[] data)
    {
        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        WriteBuffer(deviceId, bufferId, bytes);
    }

    // Op 14: beginComputePass(encoderId) -> passId
    public int BeginComputePass(int encoderIdOrRef)
    {
        int ph = AllocSlot();
        _commands.Add([14, SlotIndex(ph), encoderIdOrRef]);
        return ph;
    }

    // Op 15: dispatchWorkgroups(passId, x, y, z)
    public void DispatchWorkgroups(int passIdOrRef, int x, int y = 1, int z = 1)
    {
        _commands.Add([15, -1, passIdOrRef, x, y, z]);
    }

    // Op 16: releaseHandle(id)
    public void ReleaseHandle(int idOrRef)
    {
        _commands.Add([16, -1, idOrRef]);
    }

    public object[] GetCommands() => _commands.ToArray();
    public object[] GetBufferWrites() => _bufferWrites.Select(w => (object)w).ToArray();
    public bool HasBufferWrites => _bufferWrites.Count > 0;
    public int CommandCount => _commands.Count;

    private sealed class BufferWrite
    {
        public required string Key { get; init; }
        public required string Data { get; init; }
    }
}
