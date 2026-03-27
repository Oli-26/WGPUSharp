using System.Numerics;
using System.Runtime.InteropServices;
using WgpuSharp.Mesh;

namespace WgpuSharp.Scene;

/// <summary>Game behavior tag for a scene node.</summary>
public enum NodeTag
{
    /// <summary>Default — no special behavior.</summary>
    Static,
    /// <summary>Collectible — rotates in play mode, disappears when touched by player.</summary>
    Collectible,
    /// <summary>Trigger — invisible volume in play mode, fires event on player entry.</summary>
    Trigger,
    /// <summary>Enemy — chases player in play mode, damages on contact.</summary>
    Enemy,
    /// <summary>MovingPlatform — oscillates between position and MoveTarget in play mode.</summary>
    MovingPlatform,
    /// <summary>Door — blocks the player until the matching key is collected.</summary>
    Door,
    /// <summary>Key — collectible that unlocks the matching door (matched by name prefix).</summary>
    Key,
    /// <summary>Checkpoint — invisible volume that saves the player's respawn position on entry.</summary>
    Checkpoint,
    /// <summary>AudioSource — plays a spatialized looping tone in play mode with distance-based falloff.</summary>
    AudioSource,
    /// <summary>NPC — displays a dialog message when the player is nearby.</summary>
    NPC,
    /// <summary>Teleporter — transports the player to the matching teleporter pad (matched by name prefix).</summary>
    Teleporter,
    /// <summary>DamageZone — invisible volume that damages the player while inside.</summary>
    DamageZone,
    /// <summary>HealthPickup — collectible that restores player health on contact.</summary>
    HealthPickup,
}

/// <summary>
/// Point light properties attached to a scene node.
/// Position comes from the node's world transform.
/// </summary>
public sealed class PointLightData
{
    /// <summary>Light color (RGB, 0-1).</summary>
    public Vector3 Color { get; set; } = Vector3.One;
    /// <summary>Light intensity multiplier.</summary>
    public float Intensity { get; set; } = 2f;
    /// <summary>Maximum range of the light.</summary>
    public float Range { get; set; } = 10f;
}

/// <summary>
/// A node in the scene graph. Has a transform, optional mesh, and child nodes.
/// </summary>
public sealed class SceneNode
{
    private static int _nextId;
    private readonly List<SceneNode> _children = [];

    public SceneNode(string name = "Node")
    {
        Id = Interlocked.Increment(ref _nextId);
        Name = name;
    }

    /// <summary>Unique ID for this node.</summary>
    public int Id { get; }

    /// <summary>Display name.</summary>
    public string Name { get; set; }

    /// <summary>Spatial transform relative to parent.</summary>
    public Transform Transform { get; } = new();

    /// <summary>Game behavior tag.</summary>
    public NodeTag Tag { get; set; } = NodeTag.Static;

    /// <summary>Mesh GPU buffers to render, or null if this is a grouping node.</summary>
    public MeshBuffers? MeshBuffers { get; set; }

    /// <summary>
    /// Lower-detail mesh buffers for LOD. Index 0 = medium detail, 1 = low detail.
    /// If null or empty, MeshBuffers is used at all distances.
    /// </summary>
    public MeshBuffers?[]? LodMeshes { get; set; }

    /// <summary>Get the appropriate mesh buffers for the given camera distance.</summary>
    public MeshBuffers? GetLodMesh(float distance)
    {
        if (LodMeshes is null || LodMeshes.Length == 0) return MeshBuffers;
        if (distance > 40f && LodMeshes.Length > 1 && LodMeshes[1] is not null) return LodMeshes[1];
        if (distance > 20f && LodMeshes[0] is not null) return LodMeshes[0];
        return MeshBuffers;
    }

    /// <summary>
    /// Identifier for the mesh type (e.g. "Cube", "Sphere", "Plane", "Cylinder", "Imported").
    /// Used for serialization — GPU mesh buffers can't be serialized, so this tag
    /// allows recreation on load.
    /// </summary>
    public string? MeshType { get; set; }

    /// <summary>Name of the applied material preset, or null for custom/no preset.</summary>
    public string? MaterialPreset { get; set; }

    /// <summary>Raw bytes of an imported mesh file (OBJ/GLB/STL). Stored for serialization.</summary>
    public byte[]? ImportedMeshData { get; set; }

    /// <summary>Original filename of the imported mesh (for format detection on reload).</summary>
    public string? ImportedMeshFileName { get; set; }

    /// <summary>Material for rendering. Falls back to default white.</summary>
    public Material Material { get; set; } = Material.Default;

    /// <summary>Per-node tint color (multiplied with material base color).</summary>
    public Vector4 Color { get; set; } = Vector4.One;

    /// <summary>Point light data, or null if this node is not a light.</summary>
    public PointLightData? Light { get; set; }

    /// <summary>Whether this node is a point light.</summary>
    public bool IsLight => Light is not null;

    /// <summary>Whether this node and its children are rendered.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Whether this node participates in collision (solid objects block the player).</summary>
    public bool Solid { get; set; } = true;

    /// <summary>Whether this node is locked against editing (cannot be moved, deleted, or nudged).</summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Script text attached to this node. Each line is a command executed in play mode.
    /// Commands: Rotate(x,y,z), Bob(speed,height), FollowPlayer(speed,range),
    /// OnEnter(message), OnEnterToggle(targetName), SetColor(r,g,b), Scale(factor),
    /// Orbit(radius,speed), LookAtPlayer().
    /// </summary>
    public string? Script { get; set; }

    /// <summary>End position for MovingPlatform oscillation (start position is Transform.Position).</summary>
    public Vector3 MoveTarget { get; set; }

    /// <summary>Parent node, or null if this is a root node.</summary>
    public SceneNode? Parent { get; private set; }

    /// <summary>Read-only list of children.</summary>
    public IReadOnlyList<SceneNode> Children => _children;

    /// <summary>Add a child node. Removes from previous parent if any.</summary>
    public void AddChild(SceneNode child)
    {
        if (child == this) throw new ArgumentException("Cannot add a node as its own child.");
        if (IsDescendantOf(child)) throw new ArgumentException("Cannot add an ancestor as a child (cycle).");

        child.Parent?.RemoveChild(child);
        child.Parent = this;
        _children.Add(child);
        child.Transform.MarkDirty();
    }

    /// <summary>Remove a child node.</summary>
    public bool RemoveChild(SceneNode child)
    {
        if (!_children.Remove(child)) return false;
        child.Parent = null;
        child.Transform.MarkDirty();
        return true;
    }

    /// <summary>Remove this node from its parent.</summary>
    public void Detach()
    {
        Parent?.RemoveChild(this);
    }

    /// <summary>
    /// Recursively update world transforms down the tree.
    /// Call once per frame from the root before rendering.
    /// </summary>
    public void UpdateTransforms(Matrix4x4 parentWorld)
    {
        Transform.UpdateWorldMatrix(parentWorld);

        foreach (var child in _children)
        {
            child.UpdateTransforms(Transform.WorldMatrix);
        }
    }

    /// <summary>Update transforms as a root node (identity parent).</summary>
    public void UpdateTransforms()
    {
        Transform.UpdateWorldMatrix();

        foreach (var child in _children)
        {
            child.UpdateTransforms(Transform.WorldMatrix);
        }
    }

    /// <summary>
    /// Iterate all visible nodes with meshes in the subtree (depth-first).
    /// </summary>
    public IEnumerable<SceneNode> GetVisibleMeshNodes()
    {
        if (!Visible) yield break;

        if (MeshBuffers is not null)
            yield return this;

        foreach (var child in _children)
        {
            foreach (var node in child.GetVisibleMeshNodes())
                yield return node;
        }
    }

    /// <summary>Find a descendant by ID (depth-first).</summary>
    public SceneNode? FindById(int id)
    {
        if (Id == id) return this;
        foreach (var child in _children)
        {
            var found = child.FindById(id);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Find a descendant by name (depth-first, first match).</summary>
    public SceneNode? FindByName(string name)
    {
        if (Name == name) return this;
        foreach (var child in _children)
        {
            var found = child.FindByName(name);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Get all nodes in the subtree (depth-first), including this node.</summary>
    public IEnumerable<SceneNode> GetAllNodes()
    {
        yield return this;
        foreach (var child in _children)
        {
            foreach (var node in child.GetAllNodes())
                yield return node;
        }
    }

    private bool IsDescendantOf(SceneNode potentialAncestor)
    {
        var current = Parent;
        while (current is not null)
        {
            if (current == potentialAncestor) return true;
            current = current.Parent;
        }
        return false;
    }
}
