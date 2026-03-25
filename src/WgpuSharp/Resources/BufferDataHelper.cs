using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WgpuSharp.Resources;

/// <summary>
/// Converts unmanaged types to byte arrays for GPU buffer writes.
/// </summary>
internal static class BufferDataHelper
{
    /// <summary>Converts an array of unmanaged structs to a byte array.</summary>
    public static byte[] ToBytes<T>(T[] data) where T : unmanaged
    {
        var bytes = new byte[data.Length * Unsafe.SizeOf<T>()];
        MemoryMarshal.AsBytes(data.AsSpan()).CopyTo(bytes);
        return bytes;
    }

    /// <summary>Converts a single unmanaged struct to a byte array.</summary>
    public static byte[] ToBytes<T>(T value) where T : unmanaged
    {
        var bytes = new byte[Unsafe.SizeOf<T>()];
        MemoryMarshal.Write(bytes, in value);
        return bytes;
    }
}
