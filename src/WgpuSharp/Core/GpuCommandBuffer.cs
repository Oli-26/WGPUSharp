namespace WgpuSharp.Core;

/// <summary>A finished command buffer ready to be submitted to a GPU queue.</summary>
public sealed class GpuCommandBuffer
{
    internal int Handle { get; }

    internal GpuCommandBuffer(int handle)
    {
        Handle = handle;
    }
}
