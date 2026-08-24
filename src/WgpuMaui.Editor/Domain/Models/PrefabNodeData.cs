using System.Numerics;
using WgpuMaui.Scene;

namespace WgpuMaui.Editor.Domain.Models;

public sealed class PrefabNodeData
{
    public string Name { get; set; } = "";

    public string? MeshType { get; set; }

    public string? MaterialPreset { get; set; }

    public NodeTag Tag { get; set; }

    public Vector3 Position { get; set; }

    public Vector3 Scale { get; set; }

    public Quaternion Rotation { get; set; }

    public Vector4 Color { get; set; }

    public string? Script { get; set; }

    public List<PrefabNodeData> Children { get; set; } = [];
}