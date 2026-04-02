using System.Numerics;
using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Mesh;

/// <summary>
/// A mesh with per-vertex joint indices and weights for skeletal animation.
/// Vertex layout: pos(12) + normal(12) + joints(4) + weights(16) = 44 bytes.
/// </summary>
public sealed class SkinnedMesh
{
    public Vector3[] Positions { get; init; } = [];
    public Vector3[] Normals { get; init; } = [];
    public uint[] Indices { get; init; } = [];

    /// <summary>Per-vertex joint indices (4 bytes per vertex, packed sequentially). Length = VertexCount * 4.</summary>
    public byte[] JointIndices { get; init; } = [];

    /// <summary>Per-vertex joint weights (4 weights per vertex).</summary>
    public Vector4[] Weights { get; init; } = [];

    public int VertexCount => Positions.Length;
    public int IndexCount => Indices.Length;

    private const int Stride = 44; // bytes per vertex

    /// <summary>Interleave vertex data into a byte array for GPU upload.</summary>
    public byte[] GetInterleavedVertices()
    {
        var data = new byte[VertexCount * Stride];
        for (int i = 0; i < VertexCount; i++)
        {
            int off = i * Stride;
            // Position (12 bytes)
            BitConverter.TryWriteBytes(data.AsSpan(off), Positions[i].X);
            BitConverter.TryWriteBytes(data.AsSpan(off + 4), Positions[i].Y);
            BitConverter.TryWriteBytes(data.AsSpan(off + 8), Positions[i].Z);
            // Normal (12 bytes)
            BitConverter.TryWriteBytes(data.AsSpan(off + 12), Normals[i].X);
            BitConverter.TryWriteBytes(data.AsSpan(off + 16), Normals[i].Y);
            BitConverter.TryWriteBytes(data.AsSpan(off + 20), Normals[i].Z);
            // Joint indices (4 bytes)
            data[off + 24] = JointIndices[i * 4];
            data[off + 25] = JointIndices[i * 4 + 1];
            data[off + 26] = JointIndices[i * 4 + 2];
            data[off + 27] = JointIndices[i * 4 + 3];
            // Weights (16 bytes)
            BitConverter.TryWriteBytes(data.AsSpan(off + 28), Weights[i].X);
            BitConverter.TryWriteBytes(data.AsSpan(off + 32), Weights[i].Y);
            BitConverter.TryWriteBytes(data.AsSpan(off + 36), Weights[i].Z);
            BitConverter.TryWriteBytes(data.AsSpan(off + 40), Weights[i].W);
        }
        return data;
    }

    /// <summary>Vertex buffer layout for skinned mesh vertex data (slot 0).</summary>
    public static VertexBufferLayout GetVertexBufferLayout() => new()
    {
        ArrayStride = Stride,
        StepMode = VertexStepMode.Vertex,
        Attributes =
        [
            new VertexAttribute { ShaderLocation = 0, Offset = 0, Format = VertexFormat.Float32x3 },   // position
            new VertexAttribute { ShaderLocation = 1, Offset = 12, Format = VertexFormat.Float32x3 },  // normal
            new VertexAttribute { ShaderLocation = 2, Offset = 24, Format = VertexFormat.Uint8x4 },    // joint indices
            new VertexAttribute { ShaderLocation = 8, Offset = 28, Format = VertexFormat.Float32x4 },  // weights
        ],
    };

    /// <summary>Upload skinned vertex and index data to GPU buffers.</summary>
    public async Task<SkinnedMeshBuffers> CreateBuffersAsync(GpuDevice device, CancellationToken ct = default)
    {
        var vertexData = GetInterleavedVertices();
        var vertexBuffer = await device.CreateBufferAsync(new BufferDescriptor
        {
            Size = vertexData.Length,
            Usage = BufferUsage.Vertex | BufferUsage.CopyDest,
        }, ct);
        await vertexBuffer.WriteAsync(vertexData, ct);

        GpuBuffer? indexBuffer = null;
        if (IndexCount > 0)
        {
            var indexBytes = new byte[Indices.Length * sizeof(uint)];
            Buffer.BlockCopy(Indices, 0, indexBytes, 0, indexBytes.Length);
            indexBuffer = await device.CreateBufferAsync(new BufferDescriptor
            {
                Size = indexBytes.Length,
                Usage = BufferUsage.Index | BufferUsage.CopyDest,
            }, ct);
            await indexBuffer.WriteAsync(indexBytes, ct);
        }

        return new SkinnedMeshBuffers
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            Layout = GetVertexBufferLayout(),
            VertexCount = VertexCount,
            IndexCount = IndexCount,
        };
    }
}

/// <summary>GPU buffers for a skinned mesh.</summary>
public sealed class SkinnedMeshBuffers
{
    public required GpuBuffer VertexBuffer { get; init; }
    public GpuBuffer? IndexBuffer { get; init; }
    public required VertexBufferLayout Layout { get; init; }
    public int VertexCount { get; init; }
    public int IndexCount { get; init; }
}

/// <summary>Per-node GPU resources for skinned mesh rendering.</summary>
public sealed class SkinnedRenderData : IAsyncDisposable
{
    /// <summary>Skinned vertex/index buffers.</summary>
    public required SkinnedMeshBuffers Buffers { get; init; }
    /// <summary>Storage buffer holding joint matrices (jointCount * 64 bytes).</summary>
    public required GpuBuffer JointBuffer { get; init; }
    /// <summary>Bind group for @group(1) binding the joint buffer.</summary>
    public required GpuBindGroup JointBindGroup { get; init; }

    public async ValueTask DisposeAsync()
    {
        await Buffers.VertexBuffer.DisposeAsync();
        if (Buffers.IndexBuffer is not null) await Buffers.IndexBuffer.DisposeAsync();
        await JointBuffer.DisposeAsync();
    }
}
