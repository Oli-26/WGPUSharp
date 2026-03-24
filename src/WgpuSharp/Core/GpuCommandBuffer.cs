namespace WgpuSharp.Core;

public sealed class GpuCommandBuffer
{
    internal int Handle { get; }

    internal GpuCommandBuffer(int handle)
    {
        Handle = handle;
    }
}
