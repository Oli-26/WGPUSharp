using WgpuSharp.Interop;

namespace WgpuSharp.Tests;

public class CommandBatchTests
{
    // Helper to get typed command arrays
    private static object[] Cmd(object[] commands, int index) => (object[])commands[index];

    [Fact]
    public void EmptyBatch_HasZeroCommands()
    {
        var batch = new CommandBatch();
        Assert.Equal(0, batch.CommandCount);
    }

    [Fact]
    public void GetCurrentTexture_ReturnsNegativePlaceholder()
    {
        var batch = new CommandBatch();
        int ph = batch.GetCurrentTexture(42);
        Assert.True(ph < 0);
    }

    [Fact]
    public void CreateTextureView_ChainedFromPlaceholder_ReturnsNextPlaceholder()
    {
        var batch = new CommandBatch();
        int texPh = batch.GetCurrentTexture(1);
        int viewPh = batch.CreateTextureView(texPh);
        Assert.True(viewPh < 0);
        Assert.NotEqual(texPh, viewPh);
    }

    [Fact]
    public void CreateCommandEncoder_ProducesOpcode2()
    {
        var batch = new CommandBatch();
        batch.CreateCommandEncoder(10);
        var commands = batch.GetCommands().ToArray();
        Assert.Single(commands);
        var cmd = Cmd(commands, 0);
        Assert.Equal(2, cmd[0]); // opcode 2
        Assert.Equal(10, cmd[2]); // deviceId
    }

    [Fact]
    public void Draw_ProducesOpcode8_WithCorrectArgs()
    {
        var batch = new CommandBatch();
        batch.Draw(passIdOrRef: -1, vertexCount: 3, instanceCount: 1, firstVertex: 0, firstInstance: 0);
        var cmd = Cmd(batch.GetCommands().ToArray(), 0);
        Assert.Equal(8, cmd[0]);
        Assert.Equal(3, cmd[3]); // vertexCount
    }

    [Fact]
    public void DrawIndexed_ProducesOpcode9()
    {
        var batch = new CommandBatch();
        batch.DrawIndexed(passIdOrRef: -1, indexCount: 36, instanceCount: 2);
        var cmd = Cmd(batch.GetCommands().ToArray(), 0);
        Assert.Equal(9, cmd[0]);
        Assert.Equal(36, cmd[3]); // indexCount
        Assert.Equal(2, cmd[4]); // instanceCount
    }

    [Fact]
    public void DrawIndirect_ProducesOpcode17()
    {
        var batch = new CommandBatch();
        batch.DrawIndirect(passIdOrRef: -1, bufferIdOrRef: 5, indirectOffset: 0);
        var cmd = Cmd(batch.GetCommands().ToArray(), 0);
        Assert.Equal(17, cmd[0]);
        Assert.Equal(5, cmd[3]); // bufferId
    }

    [Fact]
    public void DrawIndexedIndirect_ProducesOpcode18()
    {
        var batch = new CommandBatch();
        batch.DrawIndexedIndirect(passIdOrRef: -1, bufferIdOrRef: 7, indirectOffset: 16);
        var cmd = Cmd(batch.GetCommands().ToArray(), 0);
        Assert.Equal(18, cmd[0]);
        Assert.Equal(7, cmd[3]); // bufferId
        Assert.Equal(16L, cmd[4]); // offset
    }

    [Fact]
    public void WriteBuffer_ByteArray_ProducesBase64InWrites()
    {
        var batch = new CommandBatch();
        byte[] data = [1, 2, 3, 4];
        batch.WriteBuffer(10, 20, data);

        Assert.True(batch.HasBufferWrites);
        var writes = batch.GetBufferWritesList();
        Assert.Single(writes);
    }

    [Fact]
    public void WriteBuffer_FloatArray_ConvertsToBytes()
    {
        var batch = new CommandBatch();
        float[] data = [1.0f, 2.0f];
        batch.WriteBuffer(10, 20, data, data.Length);

        Assert.True(batch.HasBufferWrites);
        Assert.Equal(1, batch.CommandCount);
    }

    [Fact]
    public void MultipleCommands_PreservesOrder()
    {
        var batch = new CommandBatch();
        int enc = batch.CreateCommandEncoder(1);      // op 2
        int pass = batch.BeginComputePass(enc);        // op 14
        batch.DispatchWorkgroups(pass, 64);             // op 15
        batch.EndPass(pass);                            // op 10
        int buf = batch.FinishEncoder(enc);            // op 11
        batch.Submit(1, buf);                           // op 12

        var commands = batch.GetCommands().ToArray();
        Assert.Equal(6, commands.Length);
        Assert.Equal(2, Cmd(commands, 0)[0]);   // createCommandEncoder
        Assert.Equal(14, Cmd(commands, 1)[0]);  // beginComputePass
        Assert.Equal(15, Cmd(commands, 2)[0]);  // dispatchWorkgroups
        Assert.Equal(10, Cmd(commands, 3)[0]);  // endPass
        Assert.Equal(11, Cmd(commands, 4)[0]);  // finishEncoder
        Assert.Equal(12, Cmd(commands, 5)[0]);  // submit
    }

    [Fact]
    public void PlaceholderIds_AreSequential()
    {
        var batch = new CommandBatch();
        int a = batch.GetCurrentTexture(1);    // slot 0 -> -1
        int b = batch.CreateTextureView(a);     // slot 1 -> -2
        int c = batch.CreateCommandEncoder(1);  // slot 2 -> -3
        Assert.Equal(-1, a);
        Assert.Equal(-2, b);
        Assert.Equal(-3, c);
    }

    [Fact]
    public void SetPipeline_ProducesOpcode4()
    {
        var batch = new CommandBatch();
        batch.SetPipeline(-1, 42);
        var cmd = Cmd(batch.GetCommands().ToArray(), 0);
        Assert.Equal(4, cmd[0]);
        Assert.Equal(42, cmd[3]); // pipelineId
    }

    [Fact]
    public void SetBindGroup_ProducesOpcode5()
    {
        var batch = new CommandBatch();
        batch.SetBindGroup(-1, 0, 99);
        var cmd = Cmd(batch.GetCommands().ToArray(), 0);
        Assert.Equal(5, cmd[0]);
        Assert.Equal(0, cmd[3]);  // groupIndex
        Assert.Equal(99, cmd[4]); // bindGroupId
    }

    [Fact]
    public void SetVertexBuffer_ProducesOpcode6()
    {
        var batch = new CommandBatch();
        batch.SetVertexBuffer(-1, 0, 55);
        Assert.Equal(6, Cmd(batch.GetCommands().ToArray(), 0)[0]);
    }

    [Fact]
    public void SetIndexBuffer_ProducesOpcode7()
    {
        var batch = new CommandBatch();
        batch.SetIndexBuffer(-1, 33, "uint16");
        var cmd = Cmd(batch.GetCommands().ToArray(), 0);
        Assert.Equal(7, cmd[0]);
        Assert.Equal("uint16", cmd[4]);
    }

    [Fact]
    public void ReleaseHandle_ProducesOpcode16()
    {
        var batch = new CommandBatch();
        batch.ReleaseHandle(42);
        Assert.Equal(16, Cmd(batch.GetCommands().ToArray(), 0)[0]);
    }

    [Fact]
    public void BeginRenderPass_ProducesOpcode3()
    {
        var batch = new CommandBatch();
        int enc = batch.CreateCommandEncoder(1);
        batch.BeginRenderPass(enc, [new { viewId = -1 }], null);
        Assert.Equal(3, Cmd(batch.GetCommands().ToArray(), 1)[0]);
    }

    [Fact]
    public void NoBufferWrites_HasBufferWrites_IsFalse()
    {
        var batch = new CommandBatch();
        batch.Draw(-1, 3);
        Assert.False(batch.HasBufferWrites);
    }
}
