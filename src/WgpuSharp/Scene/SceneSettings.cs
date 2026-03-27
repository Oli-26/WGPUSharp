using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// Scene-level environment settings: sky colors, ambient light, sun direction/color.
/// Passed as uniforms to shaders each frame so they can be changed at runtime.
/// </summary>
public sealed class SceneSettings
{
    /// <summary>Sky zenith (top) color.</summary>
    public Vector3 SkyZenith { get; set; } = new(0.15f, 0.25f, 0.55f);
    /// <summary>Sky horizon color.</summary>
    public Vector3 SkyHorizon { get; set; } = new(0.65f, 0.50f, 0.40f);
    /// <summary>Sky ground (below horizon) color.</summary>
    public Vector3 SkyGround { get; set; } = new(0.08f, 0.08f, 0.10f);

    /// <summary>Sun direction (will be normalized in shader).</summary>
    public Vector3 SunDirection { get; set; } = new(0.4f, 0.8f, 0.3f);
    /// <summary>Sun color.</summary>
    public Vector3 SunColor { get; set; } = new(0.9f, 0.85f, 0.8f);
    /// <summary>Sun intensity (0-2).</summary>
    public float SunIntensity { get; set; } = 0.5f;

    /// <summary>Ambient light color.</summary>
    public Vector3 AmbientColor { get; set; } = new(0.12f, 0.12f, 0.14f);

    /// <summary>Fog color (objects blend toward this at distance).</summary>
    public Vector3 FogColor { get; set; } = new(0.08f, 0.08f, 0.10f);
    /// <summary>Distance at which fog begins.</summary>
    public float FogStart { get; set; } = 30f;
    /// <summary>Distance at which fog is fully opaque.</summary>
    public float FogEnd { get; set; } = 80f;

    /// <summary>
    /// Pack scene environment data into a byte array for the environment uniform buffer.
    /// Layout (std140-compatible, 128 bytes):
    ///   0: sunDirection(vec3f) + sunIntensity(f32)   = 16 bytes
    ///  16: sunColor(vec3f) + pad                      = 16 bytes
    ///  32: ambientColor(vec3f) + pad                  = 16 bytes
    ///  48: skyZenith(vec3f) + pad                     = 16 bytes
    ///  64: skyHorizon(vec3f) + pad                    = 16 bytes
    ///  80: skyGround(vec3f) + pad                     = 16 bytes
    ///  96: fogColor(vec3f) + fogStart(f32)            = 16 bytes
    /// 112: fogEnd(f32) + pad(3xf32)                   = 16 bytes
    /// Total: 128 bytes
    /// </summary>
    private readonly byte[] _buffer = new byte[128];

    /// <summary>Write settings into a reusable buffer. Returns the same array each call (no allocation).</summary>
    public byte[] ToBytes()
    {
        WriteVec3F(_buffer, 0, SunDirection); WriteF32(_buffer, 12, SunIntensity);
        WriteVec3F(_buffer, 16, SunColor);
        WriteVec3F(_buffer, 32, AmbientColor);
        WriteVec3F(_buffer, 48, SkyZenith);
        WriteVec3F(_buffer, 64, SkyHorizon);
        WriteVec3F(_buffer, 80, SkyGround);
        WriteVec3F(_buffer, 96, FogColor); WriteF32(_buffer, 108, FogStart);
        WriteF32(_buffer, 112, FogEnd);
        return _buffer;
    }

    private static void WriteVec3F(byte[] dest, int offset, Vector3 v)
    {
        BitConverter.TryWriteBytes(dest.AsSpan(offset), v.X);
        BitConverter.TryWriteBytes(dest.AsSpan(offset + 4), v.Y);
        BitConverter.TryWriteBytes(dest.AsSpan(offset + 8), v.Z);
    }

    private static void WriteF32(byte[] dest, int offset, float v)
    {
        BitConverter.TryWriteBytes(dest.AsSpan(offset), v);
    }
}
