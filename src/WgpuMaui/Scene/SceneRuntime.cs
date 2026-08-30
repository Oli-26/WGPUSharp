using WgpuMaui.Mesh;

namespace WgpuMaui.Scene;

/// <summary>Lifecycle state of a scene runtime.</summary>
public enum SceneRuntimeState
{
    /// <summary>No scene has been installed.</summary>
    Empty,
    /// <summary>A scene load is in progress.</summary>
    Loading,
    /// <summary>A scene is installed and ready for the game loop.</summary>
    Ready,
    /// <summary>The runtime has been disposed.</summary>
    Disposed,
}

/// <summary>Source of versioned scene documents. Acquisition remains owned by C#.</summary>
public interface ISceneSource
{
    /// <summary>Reads a scene document identified by a stable source ID.</summary>
    Task<string> ReadAsync(string sourceId, CancellationToken cancellationToken = default);
}

/// <summary>Resolves serialized mesh references into runtime resources.</summary>
public interface ISceneAssetResolver
{
    /// <summary>Resolves one serialized node's mesh reference.</summary>
    Task<MeshBuffers?> ResolveMeshAsync(NodeData node, CancellationToken cancellationToken = default);
}

/// <summary>Optional scene-owned work performed after the graph has been rebuilt.</summary>
public interface ISceneRuntimeConfigurator
{
    /// <summary>Configures runtime components and tracks owned resources in the scope.</summary>
    Task ConfigureAsync(SceneData data, Scene scene, SceneLoadScope scope, CancellationToken cancellationToken = default);
}

/// <summary>Describes a request to load a scene document.</summary>
public sealed record SceneLoadRequest(string SourceId);

/// <summary>Event raised only after a scene has been fully installed.</summary>
public sealed class SceneReadyEventArgs(SceneRuntimeResult result) : EventArgs
{
    /// <summary>The result that has just been installed.</summary>
    public SceneRuntimeResult Result { get; } = result;
}

/// <summary>
/// Owns resources created while loading one scene. Successful loads transfer this scope
/// to the result; failed, cancelled and superseded loads dispose it automatically.
/// </summary>
public sealed class SceneLoadScope : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _resources = [];
    private int _disposed;

    /// <summary>Adds an asynchronously disposable resource owned by this scene.</summary>
    public void Track(IAsyncDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _resources.Add(resource);
    }

    /// <summary>Disposes tracked resources in reverse registration order.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        for (int index = _resources.Count - 1; index >= 0; index--)
            await _resources[index].DisposeAsync();

        _resources.Clear();
    }
}

/// <summary>Installed scene plus its serialized document and owned resources.</summary>
public sealed class SceneRuntimeResult(Scene scene, SceneData data, SceneSettings settings, SceneLoadScope scope) : IAsyncDisposable
{
    /// <summary>The reconstructed scene graph.</summary>
    public Scene Scene { get; } = scene;
    /// <summary>The validated serialized document.</summary>
    public SceneData Data { get; } = data;
    /// <summary>Environment settings reconstructed from the document.</summary>
    public SceneSettings Settings { get; } = settings;
    /// <summary>Resources owned by this installed scene.</summary>
    public SceneLoadScope Scope { get; } = scope;

    /// <summary>Releases resources owned by this scene.</summary>
    public ValueTask DisposeAsync() => Scope.DisposeAsync();
}

/// <summary>Exception raised when a scene document uses an unsupported version.</summary>
public sealed class SceneVersionException(int version, int maximumVersion)
    : InvalidOperationException($"Scene version {version} is not supported. Maximum supported version is {maximumVersion}.")
{
    /// <summary>The version found in the document.</summary>
    public int Version { get; } = version;
    /// <summary>The maximum version accepted by the loader.</summary>
    public int MaximumVersion { get; } = maximumVersion;
}

/// <summary>Exception raised when a serialized asset cannot be resolved.</summary>
public sealed class SceneAssetNotFoundException(string assetId)
    : InvalidOperationException($"Scene asset '{assetId}' could not be resolved.")
{
    /// <summary>The stable asset identifier that could not be resolved.</summary>
    public string AssetId { get; } = assetId;
}

/// <summary>
/// Loads and installs complete scene documents without allowing stale asynchronous work
/// to replace a newer scene.
/// </summary>
public sealed class SceneRuntime : IAsyncDisposable
{
    private readonly ISceneSource _source;
    private readonly ISceneAssetResolver _assets;
    private readonly ISceneRuntimeConfigurator _configurator;
    private readonly object _gate = new();
    private CancellationTokenSource? _loadCancellation;
    private SceneRuntimeResult? _active;
    private long _generation;
    private bool _disposed;

    /// <summary>Creates a runtime with C#-owned scene and asset acquisition services.</summary>
    public SceneRuntime(ISceneSource source, ISceneAssetResolver assets, ISceneRuntimeConfigurator? configurator = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _configurator = configurator ?? NoOpSceneRuntimeConfigurator.Instance;
    }

    /// <summary>Current lifecycle state.</summary>
    public SceneRuntimeState State { get; private set; } = SceneRuntimeState.Empty;
    /// <summary>Currently installed scene, if any.</summary>
    public SceneRuntimeResult? Active { get { lock (_gate) return _active; } }
    /// <summary>Raised after a fully configured scene replaces the previous scene.</summary>
    public event EventHandler<SceneReadyEventArgs>? SceneReady;

    /// <summary>Loads, validates, configures and atomically installs a scene document.</summary>
    public async Task<SceneRuntimeResult> LoadAsync(SceneLoadRequest request, int maximumVersion = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SourceId)) throw new ArgumentException("A scene source is required.", nameof(request));

        CancellationTokenSource loadCancellation;
        long generation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            loadCancellation = _loadCancellation;
            generation = ++_generation;
            State = SceneRuntimeState.Loading;
        }

        var scope = new SceneLoadScope();
        try
        {
            var token = loadCancellation.Token;
            var json = await _source.ReadAsync(request.SourceId, token);
            token.ThrowIfCancellationRequested();
            var data = SceneSerializer.Deserialize(json);
            if (data.Version < 1 || data.Version > maximumVersion)
                throw new SceneVersionException(data.Version, maximumVersion);

            var scene = new Scene();
            var meshCache = await ResolveMeshesAsync(data, token);
            SceneSerializer.Rebuild(scene, data, (meshType, importedData, importedFileName) =>
                meshCache.TryGetValue(MeshKey.CreateFromBytes(meshType, importedData, importedFileName), out var mesh) ? mesh : null);
            var settings = new SceneSettings();
            SceneSerializer.RestoreSettings(data, settings);
            await _configurator.ConfigureAsync(data, scene, scope, token);
            token.ThrowIfCancellationRequested();

            SceneRuntimeResult result;
            SceneRuntimeResult? previous;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (generation != _generation) throw new OperationCanceledException(token);
                previous = _active;
                result = new SceneRuntimeResult(scene, data, settings, scope);
                _active = result;
                State = SceneRuntimeState.Ready;
            }

            await DisposePreviousAsync(previous);
            SceneReady?.Invoke(this, new SceneReadyEventArgs(result));
            return result;
        }
        catch
        {
            await scope.DisposeAsync();
            lock (_gate)
            {
                if (!_disposed && generation == _generation)
                    State = _active is null ? SceneRuntimeState.Empty : SceneRuntimeState.Ready;
            }
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_loadCancellation, loadCancellation))
                    _loadCancellation = null;
            }
            loadCancellation.Dispose();
        }
    }

    /// <summary>Cancels pending loads and releases the active scene.</summary>
    public async ValueTask DisposeAsync()
    {
        SceneRuntimeResult? active;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            active = _active;
            _active = null;
            State = SceneRuntimeState.Disposed;
        }
        if (active is not null) await active.DisposeAsync();
    }

    private async Task<Dictionary<MeshKey, MeshBuffers>> ResolveMeshesAsync(SceneData data, CancellationToken cancellationToken)
    {
        var result = new Dictionary<MeshKey, MeshBuffers>();
        foreach (var node in EnumerateNodes(data.Nodes))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.MeshType is null) continue;
            var key = MeshKey.Create(node.MeshType, node.ImportedMeshData, node.ImportedMeshFileName);
            if (result.ContainsKey(key)) continue;
            var mesh = await _assets.ResolveMeshAsync(node, cancellationToken);
            if (mesh is null) throw new SceneAssetNotFoundException(node.MeshType);
            result.Add(key, mesh);
        }
        return result;
    }

    private static IEnumerable<NodeData> EnumerateNodes(IEnumerable<NodeData> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            if (node.Children is not null)
                foreach (var child in EnumerateNodes(node.Children)) yield return child;
        }
    }

    private static Task DisposePreviousAsync(SceneRuntimeResult? previous)
        => previous is null ? Task.CompletedTask : previous.DisposeAsync().AsTask();

    private readonly record struct MeshKey(string Type, string? Data, string? FileName)
    {
        public static MeshKey Create(string type, string? data, string? fileName)
            => new(type, data, fileName);

        public static MeshKey CreateFromBytes(string type, byte[]? data, string? fileName)
            => new(type, data is null ? null : Convert.ToBase64String(data), fileName);
    }

    private sealed class NoOpSceneRuntimeConfigurator : ISceneRuntimeConfigurator
    {
        public static readonly NoOpSceneRuntimeConfigurator Instance = new();
        public Task ConfigureAsync(SceneData data, Scene scene, SceneLoadScope scope, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}