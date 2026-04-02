using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WgpuSharp.Scene;

/// <summary>
/// Serializes and deserializes a <see cref="Scene"/> to/from JSON.
/// Stores node hierarchy, transforms, mesh types, colors, and visibility.
/// GPU resources (mesh buffers) are not serialized — they are recreated from <see cref="SceneNode.MeshType"/>.
/// </summary>
public static class SceneSerializer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialize a scene to JSON.</summary>
    public static string Serialize(Scene scene, SceneSettings? settings = null, GameSettingsData? gameSettings = null)
    {
        var data = new SceneData
        {
            Version = 1,
            Nodes = scene.Roots.Select(SerializeNode).ToArray(),
            Settings = settings is not null ? SerializeSettings(settings) : null,
            GameSettings = gameSettings,
        };
        return JsonSerializer.Serialize(data, JsonOpts);
    }

    private static SettingsData SerializeSettings(SceneSettings s) => new()
    {
        SkyZenith = Vec3(s.SkyZenith), SkyHorizon = Vec3(s.SkyHorizon), SkyGround = Vec3(s.SkyGround),
        SunDirection = Vec3(s.SunDirection), SunColor = Vec3(s.SunColor), SunIntensity = s.SunIntensity,
        AmbientColor = Vec3(s.AmbientColor),
        FogColor = Vec3(s.FogColor), FogStart = s.FogStart, FogEnd = s.FogEnd,
    };

    public static void RestoreSettings(SceneData data, SceneSettings target)
    {
        if (data.Settings is not { } s) return;
        if (s.SkyZenith is { Length: >= 3 }) target.SkyZenith = new(s.SkyZenith[0], s.SkyZenith[1], s.SkyZenith[2]);
        if (s.SkyHorizon is { Length: >= 3 }) target.SkyHorizon = new(s.SkyHorizon[0], s.SkyHorizon[1], s.SkyHorizon[2]);
        if (s.SkyGround is { Length: >= 3 }) target.SkyGround = new(s.SkyGround[0], s.SkyGround[1], s.SkyGround[2]);
        if (s.SunDirection is { Length: >= 3 }) target.SunDirection = new(s.SunDirection[0], s.SunDirection[1], s.SunDirection[2]);
        if (s.SunColor is { Length: >= 3 }) target.SunColor = new(s.SunColor[0], s.SunColor[1], s.SunColor[2]);
        if (s.SunIntensity.HasValue) target.SunIntensity = s.SunIntensity.Value;
        if (s.AmbientColor is { Length: >= 3 }) target.AmbientColor = new(s.AmbientColor[0], s.AmbientColor[1], s.AmbientColor[2]);
        if (s.FogColor is { Length: >= 3 }) target.FogColor = new(s.FogColor[0], s.FogColor[1], s.FogColor[2]);
        if (s.FogStart.HasValue) target.FogStart = s.FogStart.Value;
        if (s.FogEnd.HasValue) target.FogEnd = s.FogEnd.Value;
    }

    /// <summary>
    /// Deserialize scene nodes from JSON.
    /// Returns a list of root-level node data. Use <see cref="Rebuild"/> to recreate the scene.
    /// </summary>
    public static SceneData Deserialize(string json)
    {
        return JsonSerializer.Deserialize<SceneData>(json, JsonOpts)
               ?? throw new JsonException("Failed to parse scene JSON.");
    }

    /// <summary>
    /// Rebuild a scene from deserialized data and a mesh resolver.
    /// The mesh resolver maps a mesh type string to GPU mesh buffers.
    /// </summary>
    public static void Rebuild(Scene scene, SceneData data, Func<string, byte[]?, string?, Mesh.MeshBuffers?> meshResolver)
    {
        // Clear existing scene
        while (scene.Roots.Count > 0)
            scene.Remove(scene.Roots[0]);
        scene.SelectedNode = null;

        foreach (var nodeData in data.Nodes)
        {
            var node = RebuildNode(nodeData, meshResolver);
            scene.Add(node);
        }
    }

    private static NodeData SerializeNode(SceneNode node)
    {
        return new NodeData
        {
            Name = node.Name,
            MeshType = node.MeshType,
            MaterialPreset = node.MaterialPreset,
            Tag = node.Tag == NodeTag.Static ? null : node.Tag.ToString(),
            Visible = node.Visible ? null : false, // only store if non-default
            Solid = node.Solid ? null : false, // only store if non-default
            Locked = node.Locked ? true : null,
            Script = node.Script,
            Color = node.Color == Vector4.One ? null : new[] { node.Color.X, node.Color.Y, node.Color.Z, node.Color.W },
            Position = IsZero(node.Transform.Position) ? null : Vec3(node.Transform.Position),
            Rotation = IsIdentity(node.Transform.Rotation) ? null : Quat(node.Transform.Rotation),
            Scale = IsOne(node.Transform.Scale) ? null : Vec3(node.Transform.Scale),
            MoveTarget = IsZero(node.MoveTarget) ? null : Vec3(node.MoveTarget),
            ImportedMeshData = node.ImportedMeshData is not null ? Convert.ToBase64String(node.ImportedMeshData) : null,
            ImportedMeshFileName = node.ImportedMeshFileName,
            Light = node.Light is { } l ? new LightNodeData
            {
                Color = new[] { l.Color.X, l.Color.Y, l.Color.Z },
                Intensity = l.Intensity,
                Range = l.Range,
            } : null,
            HasSkeleton = node.AnimationPlayer is not null ? true : null,
            AnimationState = node.AnimationPlayer is { } ap ? new AnimationStateData
            {
                ClipIndex = ap.CurrentClipIndex > 0 ? ap.CurrentClipIndex : null,
                Playing = ap.IsPlaying ? true : null,
                Loop = !ap.Loop ? false : null,
                Speed = ap.Speed != 1f ? ap.Speed : null,
            } : null,
            Children = node.Children.Count > 0 ? node.Children.Select(SerializeNode).ToArray() : null,
        };
    }

    private static SceneNode RebuildNode(NodeData data, Func<string, byte[]?, string?, Mesh.MeshBuffers?> meshResolver)
    {
        var node = new SceneNode(data.Name ?? "Node");

        if (data.ImportedMeshData is not null)
        {
            node.ImportedMeshData = Convert.FromBase64String(data.ImportedMeshData);
            node.ImportedMeshFileName = data.ImportedMeshFileName;
        }

        if (data.MeshType is not null)
        {
            node.MeshType = data.MeshType;
            node.MeshBuffers = meshResolver(data.MeshType, node.ImportedMeshData, node.ImportedMeshFileName);
        }

        if (data.MaterialPreset is not null)
            node.MaterialPreset = data.MaterialPreset;

        if (data.Tag is not null && Enum.TryParse<NodeTag>(data.Tag, out var tag))
            node.Tag = tag;

        if (data.Visible.HasValue)
            node.Visible = data.Visible.Value;

        if (data.Solid.HasValue)
            node.Solid = data.Solid.Value;

        if (data.Locked.HasValue)
            node.Locked = data.Locked.Value;

        node.Script = data.Script;

        if (data.Color is { Length: >= 4 })
            node.Color = new Vector4(data.Color[0], data.Color[1], data.Color[2], data.Color[3]);

        if (data.Position is { Length: >= 3 })
            node.Transform.Position = new Vector3(data.Position[0], data.Position[1], data.Position[2]);

        if (data.Rotation is { Length: >= 4 })
            node.Transform.Rotation = new Quaternion(data.Rotation[0], data.Rotation[1], data.Rotation[2], data.Rotation[3]);

        if (data.Scale is { Length: >= 3 })
            node.Transform.Scale = new Vector3(data.Scale[0], data.Scale[1], data.Scale[2]);

        if (data.MoveTarget is { Length: >= 3 })
            node.MoveTarget = new Vector3(data.MoveTarget[0], data.MoveTarget[1], data.MoveTarget[2]);

        if (data.Light is { } ld)
        {
            node.Light = new PointLightData
            {
                Color = ld.Color is { Length: >= 3 } ? new Vector3(ld.Color[0], ld.Color[1], ld.Color[2]) : Vector3.One,
                Intensity = ld.Intensity,
                Range = ld.Range,
            };
        }

        if (data.Children is not null)
        {
            foreach (var childData in data.Children)
            {
                var child = RebuildNode(childData, meshResolver);
                node.AddChild(child);
            }
        }

        return node;
    }

    private static float[] Vec3(Vector3 v) => [v.X, v.Y, v.Z];
    private static float[] Quat(Quaternion q) => [q.X, q.Y, q.Z, q.W];
    private static bool IsZero(Vector3 v) => v == Vector3.Zero;
    private static bool IsOne(Vector3 v) => v == Vector3.One;
    private static bool IsIdentity(Quaternion q) => q == Quaternion.Identity;
}

/// <summary>Top-level scene file format.</summary>
public sealed class SceneData
{
    public int Version { get; set; } = 1;
    public NodeData[] Nodes { get; set; } = [];
    public SettingsData? Settings { get; set; }
    public GameSettingsData? GameSettings { get; set; }
}

/// <summary>Serialized gameplay settings (player speed, health, win condition, etc.).</summary>
public sealed class GameSettingsData
{
    public float? PlayerSpeed { get; set; }
    public float? JumpHeight { get; set; }
    public float? Gravity { get; set; }
    public int? MaxPlayerHealth { get; set; }
    public bool? TimerEnabled { get; set; }
    public float? TimerMax { get; set; }
    public string? WinCondition { get; set; }
    public string? StartMessage { get; set; }
}

/// <summary>Serialized scene environment settings.</summary>
public sealed class SettingsData
{
    public float[]? SkyZenith { get; set; }
    public float[]? SkyHorizon { get; set; }
    public float[]? SkyGround { get; set; }
    public float[]? SunDirection { get; set; }
    public float[]? SunColor { get; set; }
    public float? SunIntensity { get; set; }
    public float[]? AmbientColor { get; set; }
    public float[]? FogColor { get; set; }
    public float? FogStart { get; set; }
    public float? FogEnd { get; set; }
}

/// <summary>Serialized node data.</summary>
public sealed class NodeData
{
    public string? Name { get; set; }
    public string? MeshType { get; set; }
    public string? MaterialPreset { get; set; }
    public string? Tag { get; set; }
    public bool? Visible { get; set; }
    public bool? Solid { get; set; }
    public bool? Locked { get; set; }
    public string? Script { get; set; }
    public float[]? Color { get; set; }
    public float[]? Position { get; set; }
    public float[]? Rotation { get; set; }
    public float[]? Scale { get; set; }
    public float[]? MoveTarget { get; set; }
    public string? ImportedMeshData { get; set; }
    public string? ImportedMeshFileName { get; set; }
    public LightNodeData? Light { get; set; }
    public bool? HasSkeleton { get; set; }
    public AnimationStateData? AnimationState { get; set; }
    public NodeData[]? Children { get; set; }
}

/// <summary>Serialized animation playback state.</summary>
public sealed class AnimationStateData
{
    public int? ClipIndex { get; set; }
    public bool? Playing { get; set; }
    public bool? Loop { get; set; }
    public float? Speed { get; set; }
}

/// <summary>Serialized point light data.</summary>
public sealed class LightNodeData
{
    public float[]? Color { get; set; }
    public float Intensity { get; set; } = 2f;
    public float Range { get; set; } = 10f;
}
