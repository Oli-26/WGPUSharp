# LOD (Level of Detail) in WgpuSharp

WgpuSharp supports automatic level-of-detail for meshes. Objects far from the camera render with fewer triangles, saving GPU time without visible quality loss.

## How It Works

Each `SceneNode` can hold a `LodMeshes` array alongside its main `MeshBuffers`. The renderer picks the right detail level based on camera distance:

| Distance     | Mesh used        |
|-------------|------------------|
| < 20 units  | `MeshBuffers` (full detail) |
| 20–40 units | `LodMeshes[0]` (medium) |
| > 40 units  | `LodMeshes[1]` (low) |

If `LodMeshes` is null, the full mesh is always used. Nodes with the same LOD mesh are still batched together for instanced rendering.

## Automatic LOD for Imported Models

When you import an OBJ/GLB/STL file through the editor's Import button, LOD meshes are generated automatically using vertex clustering. No extra work needed — the import pipeline handles it.

## Using LOD in Code

### For Primitives

The built-in `Primitives` class accepts segment counts. Generate lower-detail variants and assign them:

```csharp
// Full detail
var sphereHigh = await Primitives.Sphere(16, 32).CreateBuffersAsync(device);
// Medium
var sphereMed = await Primitives.Sphere(10, 20).CreateBuffersAsync(device);
// Low
var sphereLow = await Primitives.Sphere(6, 12).CreateBuffersAsync(device);

var node = new SceneNode("MySphere")
{
    MeshBuffers = sphereHigh,
    LodMeshes = [sphereMed, sphereLow],
};
```

### For Imported Meshes

Use `MeshSimplifier.GenerateLODs()` which picks cell sizes based on the mesh's bounding box:

```csharp
var meshes = MeshLoader.Load(bytes, "model.obj");
var mesh = meshes[0];
if (!mesh.HasNormals) mesh = mesh.ComputeFlatNormals();

// Full detail buffers
var fullBuffers = await mesh.CreateBuffersAsync(device);

// Auto-generate LOD variants
var lods = MeshSimplifier.GenerateLODs(mesh);
var lodBuffers = new MeshBuffers?[2];
if (lods[0] != mesh) lodBuffers[0] = await lods[0].CreateBuffersAsync(device);
if (lods[1] != mesh) lodBuffers[1] = await lods[1].CreateBuffersAsync(device);

var node = new SceneNode("ImportedModel")
{
    MeshBuffers = fullBuffers,
    LodMeshes = lodBuffers,
};
```

### Manual Simplification

For finer control, call `MeshSimplifier.Simplify()` directly with a cell size:

```csharp
// cellSize controls aggressiveness: larger = more reduction
var medium = MeshSimplifier.Simplify(mesh, 0.05f); // subtle
var low = MeshSimplifier.Simplify(mesh, 0.15f);    // aggressive
var veryLow = MeshSimplifier.Simplify(mesh, 0.4f); // distant background
```

Cell size is in world units. A mesh spanning 10 units with `cellSize = 0.1` merges vertices within 0.1-unit cubes — roughly 100 cells across the mesh.

### Custom Distance Thresholds

Override `GetLodMesh()` behavior by subclassing or setting LOD meshes based on your own distance logic. The default thresholds (20/40 units) are set in `SceneNode.GetLodMesh()`.

## Performance Impact

A 50K triangle imported model at distance >40 renders at ~12K triangles. For a scene with 20 copies of that model at various distances, total triangle count drops from 1M to ~400K — a significant GPU win.

LOD generation runs once at import time (not per frame) and adds ~100ms for a 50K triangle mesh.
