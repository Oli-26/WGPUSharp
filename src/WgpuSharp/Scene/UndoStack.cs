using System.Numerics;

namespace WgpuSharp.Scene;

/// <summary>
/// An action that can be undone and redone.
/// </summary>
public interface IEditorAction
{
    /// <summary>Short description for display (e.g. "Move Cube 1").</summary>
    string Description { get; }
    /// <summary>Apply the action.</summary>
    void Execute();
    /// <summary>Reverse the action.</summary>
    void Undo();
}

/// <summary>
/// Manages undo/redo history for the editor.
/// </summary>
public sealed class UndoStack
{
    private readonly List<IEditorAction> _undoStack = [];
    private readonly List<IEditorAction> _redoStack = [];
    private const int MaxHistory = 100;

    /// <summary>Whether there are actions to undo.</summary>
    public bool CanUndo => _undoStack.Count > 0;
    /// <summary>Whether there are actions to redo.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Number of actions on the undo stack.</summary>
    public int UndoCount => _undoStack.Count;
    /// <summary>Number of actions on the redo stack.</summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>Description of the next undo action, or null.</summary>
    public string? UndoDescription => _undoStack.Count > 0 ? _undoStack[^1].Description : null;
    /// <summary>Description of the next redo action, or null.</summary>
    public string? RedoDescription => _redoStack.Count > 0 ? _redoStack[^1].Description : null;

    /// <summary>Read-only access to the undo history (most recent last).</summary>
    public IReadOnlyList<IEditorAction> UndoHistory => _undoStack;
    /// <summary>Read-only access to the redo history (most recent last).</summary>
    public IReadOnlyList<IEditorAction> RedoHistory => _redoStack;

    /// <summary>
    /// Execute an action and push it onto the undo stack.
    /// Clears the redo stack.
    /// </summary>
    public void Do(IEditorAction action)
    {
        action.Execute();
        _undoStack.Add(action);
        _redoStack.Clear();

        // Trim old history
        if (_undoStack.Count > MaxHistory)
            _undoStack.RemoveAt(0);
    }

    /// <summary>
    /// Push an already-executed action onto the undo stack (for actions that
    /// were applied incrementally, like gizmo drags).
    /// </summary>
    public void Push(IEditorAction action)
    {
        _undoStack.Add(action);
        _redoStack.Clear();

        if (_undoStack.Count > MaxHistory)
            _undoStack.RemoveAt(0);
    }

    /// <summary>Undo the last action.</summary>
    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        action.Undo();
        _redoStack.Add(action);
    }

    /// <summary>Redo the last undone action.</summary>
    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        action.Execute();
        _undoStack.Add(action);
    }

    /// <summary>Clear all history.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

/// <summary>Records a change to a node's transform (position, rotation, scale).</summary>
public sealed class TransformAction : IEditorAction
{
    private readonly SceneNode _node;
    private readonly Vector3 _oldPos, _newPos;
    private readonly Quaternion _oldRot, _newRot;
    private readonly Vector3 _oldScale, _newScale;

    public TransformAction(SceneNode node,
        Vector3 oldPos, Quaternion oldRot, Vector3 oldScale,
        Vector3 newPos, Quaternion newRot, Vector3 newScale)
    {
        _node = node;
        _oldPos = oldPos; _oldRot = oldRot; _oldScale = oldScale;
        _newPos = newPos; _newRot = newRot; _newScale = newScale;
    }

    /// <summary>Create a transform action that only records a position change.</summary>
    public static TransformAction Move(SceneNode node, Vector3 oldPos, Vector3 newPos)
        => new(node, oldPos, node.Transform.Rotation, node.Transform.Scale,
               newPos, node.Transform.Rotation, node.Transform.Scale);

    public string Description => $"Move {_node.Name}";

    public void Execute()
    {
        _node.Transform.Position = _newPos;
        _node.Transform.Rotation = _newRot;
        _node.Transform.Scale = _newScale;
    }

    public void Undo()
    {
        _node.Transform.Position = _oldPos;
        _node.Transform.Rotation = _oldRot;
        _node.Transform.Scale = _oldScale;
    }
}

/// <summary>Records adding a node to the scene.</summary>
public sealed class AddNodeAction : IEditorAction
{
    private readonly Scene _scene;
    private readonly SceneNode _node;
    private readonly SceneNode? _parent;

    public AddNodeAction(Scene scene, SceneNode node, SceneNode? parent = null)
    {
        _scene = scene;
        _node = node;
        _parent = parent;
    }

    public string Description => $"Add {_node.Name}";

    public void Execute()
    {
        if (_parent is not null)
            _parent.AddChild(_node);
        else
            _scene.Add(_node);
    }

    public void Undo()
    {
        if (_parent is not null)
            _parent.RemoveChild(_node);
        else
            _scene.Remove(_node);

        if (_scene.SelectedNode == _node)
            _scene.SelectedNode = null;
    }
}

/// <summary>Records deleting a node from the scene.</summary>
public sealed class DeleteNodeAction : IEditorAction
{
    private readonly Scene _scene;
    private readonly SceneNode _node;
    private readonly SceneNode? _parent;
    private readonly int _indexInParent;

    public DeleteNodeAction(Scene scene, SceneNode node)
    {
        _scene = scene;
        _node = node;
        _parent = node.Parent;
        // Remember position for re-insertion
        if (_parent is not null)
            _indexInParent = _parent.Children.ToList().IndexOf(node);
        else
            _indexInParent = scene.Roots.ToList().IndexOf(node);
    }

    public string Description => $"Delete {_node.Name}";

    public void Execute()
    {
        _scene.RemoveNode(_node);
        if (_scene.SelectedNode == _node)
            _scene.SelectedNode = null;
    }

    public void Undo()
    {
        if (_parent is not null)
            _parent.InsertChild(_node, _indexInParent);
        else
            _scene.Insert(_node, _indexInParent);

        _scene.SelectedNode = _node;
    }
}

/// <summary>Records a change to a node's color.</summary>
public sealed class ColorAction : IEditorAction
{
    private readonly SceneNode _node;
    private readonly Vector4 _oldColor, _newColor;

    public ColorAction(SceneNode node, Vector4 oldColor, Vector4 newColor)
    {
        _node = node;
        _oldColor = oldColor;
        _newColor = newColor;
    }

    public string Description => $"Color {_node.Name}";

    public void Execute() => _node.Color = _newColor;
    public void Undo() => _node.Color = _oldColor;
}
