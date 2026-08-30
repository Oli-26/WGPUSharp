using WgpuMaui.Mesh;
using WgpuMaui.Scene;
using SceneGraph = WgpuMaui.Scene.Scene;

namespace WgpuMaui.Tests;

public sealed class SceneRuntimeTests
{
    [Fact]
    public async Task LoadAsync_InstallsSceneAndPublishesReady()
    {
        var source = new TestSceneSource(("scene", """{"version":1,"settings":{"fogStart":12},"nodes":[{"name":"Root"}]}"""));
        var runtime = new SceneRuntime(source, new TestAssetResolver());
        SceneRuntimeResult? published = null;
        runtime.SceneReady += (_, args) => published = args.Result;

        var result = await runtime.LoadAsync(new SceneLoadRequest("scene"));

        Assert.Same(result, published);
        Assert.Equal(SceneRuntimeState.Ready, runtime.State);
        Assert.Equal("Root", result.Scene.Roots[0].Name);
        Assert.Equal(12, result.Settings.FogStart);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task LoadAsync_RejectsUnsupportedVersionAndKeepsActiveScene()
    {
        var source = new TestSceneSource(
            ("good", "{\"version\":1,\"nodes\":[{\"name\":\"Good\"}]}"),
            ("bad", "{\"version\":2,\"nodes\":[{\"name\":\"Bad\"}]}"));
        var runtime = new SceneRuntime(source, new TestAssetResolver());
        var active = await runtime.LoadAsync(new SceneLoadRequest("good"));

        await Assert.ThrowsAsync<SceneVersionException>(() => runtime.LoadAsync(new SceneLoadRequest("bad")));

        Assert.Same(active, runtime.Active);
        Assert.Equal(SceneRuntimeState.Ready, runtime.State);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task LoadAsync_ThrowsForMissingMesh()
    {
        var source = new TestSceneSource(("scene", "{\"version\":1,\"nodes\":[{\"name\":\"Mesh\",\"meshType\":\"missing\"}]}") );
        var runtime = new SceneRuntime(source, new TestAssetResolver());

        var exception = await Assert.ThrowsAsync<SceneAssetNotFoundException>(() => runtime.LoadAsync(new SceneLoadRequest("scene")));

        Assert.Equal("missing", exception.AssetId);
        Assert.Null(runtime.Active);
        Assert.Equal(SceneRuntimeState.Empty, runtime.State);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task LoadAsync_RejectsBlankSource()
    {
        var runtime = new SceneRuntime(new TestSceneSource(), new TestAssetResolver());

        await Assert.ThrowsAsync<ArgumentException>(() => runtime.LoadAsync(new SceneLoadRequest(" ")));

        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task LoadAsync_RejectsCallsAfterDispose()
    {
        var runtime = new SceneRuntime(new TestSceneSource(), new TestAssetResolver());
        await runtime.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => runtime.LoadAsync(new SceneLoadRequest("scene")));
    }

    [Fact]
    public async Task NewerLoadSupersedesOlderLoad()
    {
        var source = new TestSceneSource(
            ("old", "{\"version\":1,\"nodes\":[{\"name\":\"Old\"}]}"),
            ("new", "{\"version\":1,\"nodes\":[{\"name\":\"New\"}]}"));
        source.Delay("old", TimeSpan.FromMilliseconds(100));
        var runtime = new SceneRuntime(source, new TestAssetResolver());

        var oldLoad = runtime.LoadAsync(new SceneLoadRequest("old"));
        var newLoad = await runtime.LoadAsync(new SceneLoadRequest("new"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldLoad);
        Assert.Same(newLoad, runtime.Active);
        Assert.Equal("New", runtime.Active!.Scene.Roots[0].Name);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task ReplacingAndDisposingRuntimeDisposesSceneOwnedResources()
    {
        var source = new TestSceneSource(
            ("one", "{\"version\":1,\"nodes\":[]}"),
            ("two", "{\"version\":1,\"nodes\":[]}"));
        var configurator = new TrackingConfigurator();
        var runtime = new SceneRuntime(source, new TestAssetResolver(), configurator);

        await runtime.LoadAsync(new SceneLoadRequest("one"));
        await runtime.LoadAsync(new SceneLoadRequest("two"));
        Assert.Equal(1, configurator.Resources[0].DisposeCount);

        await runtime.DisposeAsync();
        Assert.Equal(1, configurator.Resources[1].DisposeCount);
        Assert.Equal(SceneRuntimeState.Disposed, runtime.State);
    }

    private sealed class TestSceneSource(params (string Id, string Json)[] scenes) : ISceneSource
    {
        private readonly Dictionary<string, string> _scenes = scenes.ToDictionary(item => item.Id, item => item.Json);
        private readonly Dictionary<string, TimeSpan> _delays = [];

        public void Delay(string id, TimeSpan delay) => _delays[id] = delay;

        public async Task<string> ReadAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            if (_delays.TryGetValue(sourceId, out var delay)) await Task.Delay(delay, cancellationToken);
            return _scenes[sourceId];
        }
    }

    private sealed class TestAssetResolver : ISceneAssetResolver
    {
        public Task<MeshBuffers?> ResolveMeshAsync(NodeData node, CancellationToken cancellationToken = default)
            => Task.FromResult<MeshBuffers?>(null);
    }

    private sealed class TrackingConfigurator : ISceneRuntimeConfigurator
    {
        public List<TrackedResource> Resources { get; } = [];

        public Task ConfigureAsync(SceneData data, SceneGraph scene, SceneLoadScope scope, CancellationToken cancellationToken = default)
        {
            var resource = new TrackedResource();
            Resources.Add(resource);
            scope.Track(resource);
            return Task.CompletedTask;
        }
    }

    private sealed class TrackedResource : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}