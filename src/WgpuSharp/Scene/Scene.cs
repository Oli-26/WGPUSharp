using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// Root container for a scene graph. Manages the top-level nodes.
/// </summary>
public sealed class Scene
{
    private readonly List<SceneNode> _roots = [];

    // Per-frame caches — call RefreshCaches() once per frame to populate
    private readonly List<SceneNode> _allNodesCache = new(256);
    private readonly List<SceneNode> _visibleMeshCache = new(256);
    private readonly List<SceneNode> _lightCache = new(16);
    private int _cacheFrame = -1;

    /// <summary>Top-level nodes in the scene.</summary>
    public IReadOnlyList<SceneNode> Roots => _roots;

    /// <summary>Cached count of all nodes (valid after RefreshCaches).</summary>
    public int NodeCount => _allNodesCache.Count;

    /// <summary>Cached count of light nodes (valid after RefreshCaches).</summary>
    public int LightCount => _lightCache.Count;

    /// <summary>The currently selected node (for editor interaction).</summary>
    public SceneNode? SelectedNode { get; set; }

    /// <summary>Add a root-level node to the scene.</summary>
    public void Add(SceneNode node)
    {
        node.Detach();
        _roots.Add(node);
    }

    /// <summary>Insert a root-level node at a specific index.</summary>
    public void Insert(SceneNode node, int index)
    {
        node.Detach();
        index = Math.Clamp(index, 0, _roots.Count);
        _roots.Insert(index, node);
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
    /// Rebuild per-frame node caches. Call once per frame before gameplay/rendering.
    /// Populates AllNodesCached, VisibleMeshNodesCached, and LightNodesCached.
    /// </summary>
    public void RefreshCaches(int frameIndex = 0)
    {
        if (_cacheFrame == frameIndex && frameIndex != 0) return;
        _cacheFrame = frameIndex;

        _allNodesCache.Clear();
        _visibleMeshCache.Clear();
        _lightCache.Clear();

        foreach (var root in _roots)
        {
            foreach (var node in root.GetAllNodes())
            {
                _allNodesCache.Add(node);
                if (node.Visible && (node.MeshBuffers is not null || node.SkinnedRenderData is not null))
                    _visibleMeshCache.Add(node);
                if (node.Visible && node.IsLight)
                    _lightCache.Add(node);
            }
        }
    }

    /// <summary>All nodes cached from last RefreshCaches call. Zero allocation.</summary>
    public List<SceneNode> AllNodesCached => _allNodesCache;

    /// <summary>Visible mesh nodes cached from last RefreshCaches call. Zero allocation.</summary>
    public List<SceneNode> VisibleMeshNodesCached => _visibleMeshCache;

    /// <summary>Visible light nodes cached from last RefreshCaches call. Zero allocation.</summary>
    public List<SceneNode> LightNodesCached => _lightCache;

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
