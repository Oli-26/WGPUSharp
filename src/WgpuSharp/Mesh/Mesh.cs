using System.Numerics;
using WgpuSharp.Core;
using WgpuSharp.Pipeline;
using WgpuSharp.Resources;

namespace WgpuSharp.Mesh;

public sealed class Mesh
{
    public Vector3[] Positions { get; init; } = [];
    public Vector3[] Normals { get; init; } = [];
    public Vector2[] TexCoords { get; init; } = [];
    public uint[] Indices { get; init; } = [];
    public Material Material { get; init; } = Material.Default;

    public bool HasNormals => Normals.Length > 0;
    public bool HasTexCoords => TexCoords.Length > 0;
    public int VertexCount => Positions.Length;
    public int IndexCount => Indices.Length;

    /// <summary>
    /// Interleaves vertex data into a float array: [pos.x, pos.y, pos.z, norm.x, norm.y, norm.z, uv.x, uv.y]
    /// Components are included based on what data is available.
    /// </summary>
    public float[] GetInterleavedVertices()
    {
        int stride = 3; // position always present
        if (HasNormals) stride += 3;
        if (HasTexCoords) stride += 2;

        var data = new float[VertexCount * stride];
        for (int i = 0; i < VertexCount; i++)
        {
            int offset = i * stride;
            data[offset] = Positions[i].X;
            data[offset + 1] = Positions[i].Y;
            data[offset + 2] = Positions[i].Z;

            int next = 3;
            if (HasNormals)
            {
                data[offset + next] = Normals[i].X;
                data[offset + next + 1] = Normals[i].Y;
                data[offset + next + 2] = Normals[i].Z;
                next += 3;
            }
            if (HasTexCoords)
            {
                data[offset + next] = TexCoords[i].X;
                data[offset + next + 1] = TexCoords[i].Y;
            }
        }
        return data;
    }

    /// <summary>
    /// Returns the vertex buffer layout matching the interleaved format.
    /// Location 0 = position (float32x3), 1 = normal (float32x3), 2 = texcoord (float32x2).
    /// </summary>
    public VertexBufferLayout GetVertexBufferLayout()
    {
        int stride = 3;
        if (HasNormals) stride += 3;
        if (HasTexCoords) stride += 2;

        var attrs = new List<VertexAttribute>();
        int location = 0;
        long offset = 0;

        attrs.Add(new VertexAttribute { ShaderLocation = location++, Offset = offset, Format = VertexFormat.Float32x3 });
        offset += 3 * sizeof(float);

        if (HasNormals)
        {
            attrs.Add(new VertexAttribute { ShaderLocation = location++, Offset = offset, Format = VertexFormat.Float32x3 });
            offset += 3 * sizeof(float);
        }
        if (HasTexCoords)
        {
            attrs.Add(new VertexAttribute { ShaderLocation = location++, Offset = offset, Format = VertexFormat.Float32x2 });
            offset += 2 * sizeof(float);
        }

        return new VertexBufferLayout
        {
            ArrayStride = stride * sizeof(float),
            Attributes = attrs.ToArray(),
        };
    }

    /// <summary>
    /// Uploads vertex and index data to GPU buffers.
    /// </summary>
    public async Task<MeshBuffers> CreateBuffersAsync(GpuDevice device, CancellationToken ct = default)
    {
        var vertexData = GetInterleavedVertices();
        var vertexBuffer = await device.CreateBufferAsync(new BufferDescriptor
        {
            Size = vertexData.Length * sizeof(float),
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

        return new MeshBuffers
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            Layout = GetVertexBufferLayout(),
            VertexCount = VertexCount,
            IndexCount = IndexCount,
        };
    }

    /// <summary>
    /// Computes flat normals from triangle faces. Requires indices.
    /// Creates a new mesh with per-face vertices (no shared vertices).
    /// </summary>
    public Mesh ComputeFlatNormals()
    {
        if (IndexCount == 0 || IndexCount % 3 != 0)
            return this;

        int triCount = IndexCount / 3;
        var newPositions = new Vector3[IndexCount];
        var newNormals = new Vector3[IndexCount];
        var newTexCoords = HasTexCoords ? new Vector2[IndexCount] : [];
        var newIndices = new uint[IndexCount];

        for (int t = 0; t < triCount; t++)
        {
            uint i0 = Indices[t * 3];
            uint i1 = Indices[t * 3 + 1];
            uint i2 = Indices[t * 3 + 2];

            var p0 = Positions[i0];
            var p1 = Positions[i1];
            var p2 = Positions[i2];

            var normal = Vector3.Normalize(Vector3.Cross(p1 - p0, p2 - p0));

            int baseIdx = t * 3;
            newPositions[baseIdx] = p0;
            newPositions[baseIdx + 1] = p1;
            newPositions[baseIdx + 2] = p2;

            newNormals[baseIdx] = normal;
            newNormals[baseIdx + 1] = normal;
            newNormals[baseIdx + 2] = normal;

            if (HasTexCoords)
            {
                newTexCoords[baseIdx] = TexCoords[i0];
                newTexCoords[baseIdx + 1] = TexCoords[i1];
                newTexCoords[baseIdx + 2] = TexCoords[i2];
            }

            newIndices[baseIdx] = (uint)baseIdx;
            newIndices[baseIdx + 1] = (uint)(baseIdx + 1);
            newIndices[baseIdx + 2] = (uint)(baseIdx + 2);
        }

        return new Mesh
        {
            Positions = newPositions,
            Normals = newNormals,
            TexCoords = newTexCoords,
            Indices = newIndices,
            Material = Material,
        };
    }

    /// <summary>
    /// Generates box-projection UVs from vertex positions and normals.
    /// For each vertex, projects onto the plane most aligned with the normal.
    /// Works on any mesh — no existing UVs required.
    /// </summary>
    public Mesh GenerateBoxUVs(float scale = 1.0f)
    {
        // Compute bounding box for normalization
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var p in Positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        var size = max - min;
        float maxDim = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        if (maxDim < 1e-6f) maxDim = 1f;

        var normals = HasNormals ? Normals : Positions.Select(_ => Vector3.UnitY).ToArray();
        var uvs = new Vector2[VertexCount];

        for (int i = 0; i < VertexCount; i++)
        {
            var p = (Positions[i] - min) / maxDim * scale;
            var n = normals[i];
            float ax = MathF.Abs(n.X), ay = MathF.Abs(n.Y), az = MathF.Abs(n.Z);

            // Project onto the dominant axis plane
            if (ax >= ay && ax >= az)
                uvs[i] = new Vector2(p.Z, p.Y);
            else if (ay >= ax && ay >= az)
                uvs[i] = new Vector2(p.X, p.Z);
            else
                uvs[i] = new Vector2(p.X, p.Y);
        }

        return new Mesh
        {
            Positions = Positions,
            Normals = Normals,
            TexCoords = uvs,
            Indices = Indices,
            Material = Material,
        };
    }
}

/// <summary>GPU buffers and layout produced by uploading a mesh to the GPU.</summary>
public sealed class MeshBuffers
{
    /// <summary>The GPU buffer containing interleaved vertex data.</summary>
    public required GpuBuffer VertexBuffer { get; init; }
    /// <summary>The GPU buffer containing index data, or null if the mesh has no indices.</summary>
    public GpuBuffer? IndexBuffer { get; init; }
    /// <summary>The vertex buffer layout describing the interleaved format.</summary>
    public required VertexBufferLayout Layout { get; init; }
    /// <summary>Number of vertices in the mesh.</summary>
    public int VertexCount { get; init; }
    /// <summary>Number of indices in the mesh (0 if non-indexed).</summary>
    public int IndexCount { get; init; }
}
