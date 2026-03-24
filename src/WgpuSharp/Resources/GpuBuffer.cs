using WgpuSharp.Interop;

namespace WgpuSharp.Resources;

/// <summary>
/// A GPU buffer for vertex data, index data, uniforms, storage, or read-back.
/// Dispose to free GPU memory.
/// </summary>
public sealed class GpuBuffer : IAsyncDisposable
{
    private readonly JsBridge _bridge;
    private readonly int _deviceHandle;
    internal int Handle { get; }
    private bool _disposed;

    internal GpuBuffer(JsBridge bridge, int deviceHandle, int handle)
    {
        _bridge = bridge;
        _deviceHandle = deviceHandle;
        Handle = handle;
    }

    /// <summary>Writes float data to this buffer.</summary>
    public async Task WriteAsync(float[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        var base64 = Convert.ToBase64String(bytes);
        await _bridge.WriteBufferAsync(_deviceHandle, Handle, base64, ct);
    }

    /// <summary>Writes raw byte data to this buffer.</summary>
    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var base64 = Convert.ToBase64String(data);
        await _bridge.WriteBufferAsync(_deviceHandle, Handle, base64, ct);
    }

    /// <summary>Maps the buffer and reads its contents back to C#. Buffer must have <see cref="BufferUsage.MapRead"/> usage.</summary>
    public async Task<byte[]> ReadAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var base64 = await _bridge.MapReadBufferAsync(Handle, ct);
        return Convert.FromBase64String(base64);
    }

    /// <summary>Maps the buffer and reads its contents as a float array.</summary>
    public async Task<float[]> ReadFloatsAsync(CancellationToken ct = default)
    {
        var bytes = await ReadAsync(ct);
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _bridge.DestroyHandleAsync(Handle);
    }
}

[Flags]
public enum BufferUsage
{
    MapRead = 0x0001,
    MapWrite = 0x0002,
    CopySource = 0x0004,
    CopyDest = 0x0008,
    Index = 0x0010,
    Vertex = 0x0020,
    Uniform = 0x0040,
    Storage = 0x0080,
    Indirect = 0x0100,
    QueryResolve = 0x0200,
}

/// <summary>Describes a GPU buffer to create.</summary>
public sealed class BufferDescriptor
{
    /// <summary>Buffer size in bytes. Must be greater than 0.</summary>
    public required long Size { get; init; }
    /// <summary>How the buffer will be used (combine flags with |).</summary>
    public required BufferUsage Usage { get; init; }
    /// <summary>If true, the buffer starts in a mapped state for initial data upload.</summary>
    public bool MappedAtCreation { get; init; }
}
