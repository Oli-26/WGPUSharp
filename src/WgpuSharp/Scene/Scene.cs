using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// Root container for a scene graph. Manages the top-level nodes.
/// </summary>
public sealed class Scene
{
    private readonly List<SceneNode> _roots = [];

    /// <summary>Top-level nodes in the scene.</summary>
    public IReadOnlyList<SceneNode> Roots => _roots;

    /// <summary>The currently selected node (for editor interaction).</summary>
    public SceneNode? SelectedNode { get; set; }

    /// <summary>Add a root-level node to the scene.</summary>
    public void Add(SceneNode node)
    {
        node.Detach();
        _roots.Add(node);
    }

    /// <summary>Remove a root-level node from the scene.</summary>
    public bool Remove(SceneNode node)
    {
        return _roots.Remove(node);
    }

    /// <summary>Remove a node from anywhere in the scene (detaches from parent or root list).</summary>
    public bool RemoveNode(SceneNode node)
    {
        if (node.Parent is not null)
        {
            node.Detach();
            return true;
        }
        return _roots.Remove(node);
    }

    /// <summary>Find a node by ID anywhere in the scene.</summary>
    public SceneNode? FindById(int id)
    {
        foreach (var root in _roots)
        {
            var found = root.FindById(id);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Find a node by name anywhere in the scene.</summary>
    public SceneNode? FindByName(string name)
    {
        foreach (var root in _roots)
        {
            var found = root.FindByName(name);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Get all nodes in the scene (depth-first).</summary>
    public IEnumerable<SceneNode> GetAllNodes()
    {
        foreach (var root in _roots)
        {
            foreach (var node in root.GetAllNodes())
                yield return node;
        }
    }

    /// <summary>Get all visible nodes that have meshes (for rendering).</summary>
    public IEnumerable<SceneNode> GetVisibleMeshNodes()
    {
        foreach (var root in _roots)
        {
            foreach (var node in root.GetVisibleMeshNodes())
                yield return node;
        }
    }

    /// <summary>Get all visible light nodes.</summary>
    public IEnumerable<SceneNode> GetLightNodes()
    {
        foreach (var node in GetAllNodes())
        {
            if (node.Visible && node.IsLight)
                yield return node;
        }
    }

    /// <summary>
    /// Update all world transforms. Call once per frame before rendering.
    /// </summary>
    public void UpdateTransforms()
    {
        foreach (var root in _roots)
        {
            root.UpdateTransforms();
        }
    }
}
