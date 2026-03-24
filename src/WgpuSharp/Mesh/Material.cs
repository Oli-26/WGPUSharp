using System.Numerics;

namespace WgpuSharp.Mesh;

public sealed class Material
{
    public string Name { get; init; } = "default";
    public Vector4 BaseColor { get; init; } = Vector4.One;
    public float Metallic { get; init; } = 0.0f;
    public float Roughness { get; init; } = 1.0f;
    public Vector3 Emissive { get; init; } = Vector3.Zero;

    /// <summary>
    /// Raw image bytes (PNG/JPG) for the base color texture, if available.
    /// </summary>
    public byte[]? BaseColorTextureData { get; init; }

    /// <summary>
    /// Raw image bytes for the normal map, if available.
    /// </summary>
    public byte[]? NormalTextureData { get; init; }

    /// <summary>
    /// Raw image bytes for the metallic-roughness texture, if available.
    /// </summary>
    public byte[]? MetallicRoughnessTextureData { get; init; }

    public bool HasBaseColorTexture => BaseColorTextureData is not null;

    /// <summary>
    /// Returns the material uniform data as bytes (for GPU upload).
    /// Layout: baseColor (vec4f), emissive (vec3f), metallic (f32), roughness (f32), hasTexture (u32), pad (2x u32)
    /// Total: 48 bytes
    /// </summary>
    public byte[] GetUniformBytes()
    {
        var data = new byte[48];
        BitConverter.TryWriteBytes(data.AsSpan(0), BaseColor.X);
        BitConverter.TryWriteBytes(data.AsSpan(4), BaseColor.Y);
        BitConverter.TryWriteBytes(data.AsSpan(8), BaseColor.Z);
        BitConverter.TryWriteBytes(data.AsSpan(12), BaseColor.W);
        BitConverter.TryWriteBytes(data.AsSpan(16), Emissive.X);
        BitConverter.TryWriteBytes(data.AsSpan(20), Emissive.Y);
        BitConverter.TryWriteBytes(data.AsSpan(24), Emissive.Z);
        BitConverter.TryWriteBytes(data.AsSpan(28), Metallic);
        BitConverter.TryWriteBytes(data.AsSpan(32), Roughness);
        BitConverter.TryWriteBytes(data.AsSpan(36), HasBaseColorTexture ? 1u : 0u);
        // 40-47: padding
        return data;
    }

    public static Material Default => new();
}
