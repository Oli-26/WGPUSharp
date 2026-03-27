using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>Defines a named material preset with color and PBR-like properties.</summary>
public sealed record MaterialPresetDef(string Name, Vector4 Color, float Metallic, float Roughness);

/// <summary>Built-in material presets for the scene editor.</summary>
public static class MaterialPresets
{
    public static readonly MaterialPresetDef[] All = [
        new("Default", new(0.8f, 0.8f, 0.8f, 1), 0f, 1f),
        new("Brick", new(0.65f, 0.25f, 0.18f, 1), 0f, 0.9f),
        new("Wood", new(0.55f, 0.35f, 0.18f, 1), 0f, 0.85f),
        new("Metal", new(0.75f, 0.75f, 0.78f, 1), 0.9f, 0.3f),
        new("Gold", new(0.95f, 0.78f, 0.25f, 1), 0.95f, 0.2f),
        new("Grass", new(0.25f, 0.55f, 0.15f, 1), 0f, 0.95f),
        new("Stone", new(0.45f, 0.43f, 0.40f, 1), 0f, 0.8f),
        new("Sand", new(0.85f, 0.75f, 0.55f, 1), 0f, 0.95f),
        new("Water", new(0.15f, 0.45f, 0.75f, 1), 0.2f, 0.1f),
        new("Concrete", new(0.6f, 0.58f, 0.55f, 1), 0f, 0.9f),
        new("Plastic Red", new(0.85f, 0.12f, 0.1f, 1), 0.05f, 0.4f),
        new("Plastic Blue", new(0.1f, 0.3f, 0.9f, 1), 0.05f, 0.4f),
        new("Glass", new(0.7f, 0.85f, 0.9f, 1), 0.1f, 0.05f),
        new("Dark", new(0.1f, 0.1f, 0.1f, 1), 0f, 0.9f),
        new("White", new(0.95f, 0.95f, 0.95f, 1), 0f, 0.9f),
    ];

    /// <summary>Find a preset by name, or null if not found.</summary>
    public static MaterialPresetDef? Find(string? name) =>
        name is null ? null : Array.Find(All, p => p.Name == name);
}
