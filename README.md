# WgpuSharp

A C# game engine and scene editor for the browser, built on **WebGPU** and **Blazor WebAssembly**.

> **Status:** 0.5.0-alpha — actively developed, API may change.

## What it does

WgpuSharp gives you two things:

1. **A typed GPU binding layer** — async C# that mirrors the WebGPU spec. No raw JavaScript.
2. **A scene editor and game engine** — build 3D levels, add gameplay, and playtest in the browser.

```csharp
var adapter = await Gpu.RequestAdapterAsync(JS);
var device  = await adapter.RequestDeviceAsync();
var canvas  = await GpuCanvas.ConfigureAsync(device, "my-canvas");

var shader = await device.CreateShaderModuleAsync(wgslCode);
var pipeline = await device.CreateRenderPipelineAsync(descriptor);
```

## Features

### GPU Binding Layer
- **Rendering** — vertex/index buffers, render pipelines, depth, textures, materials, PBR shading
- **Compute shaders** — storage buffers, dispatch workgroups, GPU read-back
- **Mesh loading** — OBJ, GLB (glTF 2.0), STL with automatic material extraction
- **Skeletal animation** — rigged GLB import with joint skinning, keyframe sampling, animation playback
- **Mesh simplification** — vertex clustering LOD generation for imported models
- **Materials** — PBR properties, base color textures, MTL file parsing, auto UV generation
- **Input** — keyboard, mouse, pointer lock, scroll wheel, edge-triggered key events, box select
- **Instanced rendering** — instance buffers with per-instance transforms
- **Batched commands** — entire frame in a single JS interop call for performance
- **Game loop** — `requestAnimationFrame`-based with delta time and FPS tracking
- **Type safety** — all WebGPU values use C# enums, zero magic strings
- **Resource management** — `IAsyncDisposable` on all GPU types, automatic per-frame handle cleanup

### Scene Editor (`/editor`)
- **Scene graph** — transform hierarchy with parent/child nodes, drag-drop reparenting
- **Gizmo tools** — translate (W), rotate (E), scale (R) with grid and rotation snapping
- **Multi-select** — shift+click, Ctrl+A, box select, group operations (align, distribute)
- **Rigged model import** — GLB files with skins and animations auto-detected, animation controls in inspector
- **13 gameplay tags** — Collectible, Enemy, Trigger, Door/Key, Checkpoint, Teleporter, DamageZone, HealthPickup, NPC, MovingPlatform, AudioSource
- **Per-object scripting** — 9 commands (Rotate, Bob, FollowPlayer, OnEnter, etc.) with 14 templates
- **Point lights** — up to 8 dynamic lights with color, intensity, range
- **Procedural sky** — gradient with sun disc, configurable colors
- **Play mode** — FPS camera with collision, gravity, jumping, score/health HUD, minimap, timer
- **Scene save/load** — JSON with embedded imported meshes
- **LOD system** — automatic distance-based detail reduction for imported models
- **Undo/redo**, copy/paste, camera bookmarks, screenshot export, F3 debug stats
- **Editor UI** — hierarchy search/filter, collapsible sections, right-click context menu, node icons, keyboard shortcut help (?), pause in play mode

## Running the demos

```bash
git clone https://github.com/Oli-26/WGPUSharp.git
cd WGPUSharp
dotnet run --project src/WgpuSharp.Demo
```

Then open **http://localhost:5212** in Chrome or Edge.

> On Linux, you may need to enable WebGPU: `chrome://flags/#enable-unsafe-webgpu`

### Demo pages

| Page | What it shows |
|------|---------------|
| `/editor` | **Scene editor** — build levels, add gameplay, playtest |
| `/triangle` | Hello world — vertex buffer, render pipeline, single draw call |
| `/cube` | Index buffers, uniform MVP matrix, depth buffer, batched game loop |
| `/reaction-diffusion` | GPU compute shaders, storage buffers, ping-pong simulation |
| `/mesh-viewer` | Load .obj/.glb/.stl files with PBR materials and textures |
| `/fly` | WASD + mouse look camera, 1,728 instanced cubes, pointer lock |
| `/shader-playground` | Interactive WGSL editor with 28 templates, snippets, save/load |

## Quick start

1. Add the NuGet package to your Blazor WASM project:
   ```
   dotnet add package WgpuSharp
   ```

2. Add the JS bridge to your `index.html` (before the Blazor script):
   ```html
   <script src="_content/WgpuSharp/WgpuSharp.js"></script>
   ```

3. Render from a Razor component:
   ```razor
   @using WgpuSharp.Core
   @using WgpuSharp.Commands
   @using WgpuSharp.Pipeline
   @using WgpuSharp.Resources
   @inject IJSRuntime JS

   <canvas id="gpu-canvas" width="800" height="600"></canvas>

   @code {
       protected override async Task OnAfterRenderAsync(bool firstRender)
       {
           if (!firstRender) return;

           var adapter = await Gpu.RequestAdapterAsync(JS);
           var device = await adapter.RequestDeviceAsync();
           var canvas = await GpuCanvas.ConfigureAsync(device, "gpu-canvas");

           // Create shaders, buffers, pipelines, and render...
       }
   }
   ```

## Batched rendering (recommended for game loops)

```csharp
var batch = new RenderBatch(device);
batch.WriteBuffer(uniformBuffer, mvpBytes);

var colorView = batch.GetCurrentTextureView(canvas);
var pass = batch.BeginRenderPass(colorView, clearColor, depthView);
pass.SetPipeline(pipeline);
pass.SetVertexBuffer(0, vertexBuffer);
pass.DrawIndexed(indexCount, instanceCount);
pass.EndAndSubmit();

await batch.FlushAsync(); // single JS interop call for the entire frame
```

## Loading meshes with automatic LOD

```csharp
var meshes = MeshLoader.Load(fileBytes, "model.obj");
var mesh = meshes[0];
if (!mesh.HasNormals) mesh = mesh.ComputeFlatNormals();

// Full detail
var buffers = await mesh.CreateBuffersAsync(device);

// Auto-generate LOD variants for distance rendering
var lods = MeshSimplifier.GenerateLODs(mesh);
var lodBuffers = new MeshBuffers?[] {
    lods[0] != mesh ? await lods[0].CreateBuffersAsync(device) : null,
    lods[1] != mesh ? await lods[1].CreateBuffersAsync(device) : null,
};

var node = new SceneNode("Model") {
    MeshBuffers = buffers,
    LodMeshes = lodBuffers, // swaps automatically at 20/40 unit distance
};
```

## Scene graph and gameplay

```csharp
using WgpuSharp.Scene;

var scene = new Scene();

// Add objects
var cube = new SceneNode("Wall") { MeshBuffers = cubeMesh, Solid = true };
cube.Transform.Position = new Vector3(0, 1, -5);
cube.Transform.Scale = new Vector3(4, 2, 0.5f);
scene.Add(cube);

// Add point light
var light = new SceneNode("Lamp") {
    Light = new PointLightData { Color = new Vector3(1, 0.9f, 0.7f), Intensity = 3, Range = 12 }
};
light.Transform.Position = new Vector3(0, 3, 0);
scene.Add(light);

// Add scripted object
var spinner = new SceneNode("Coin") { Tag = NodeTag.Collectible };
spinner.Script = "Rotate(0, 180, 0)\nBob(3, 0.3)";
scene.Add(spinner);

// Serialize to JSON
var json = SceneSerializer.Serialize(scene);
```

## Input handling

```csharp
var input = await GpuInput.InitAsync(device, "gpu-canvas");

// In your game loop:
var state = await input.GetStateAsync();
if (state.IsKeyDown("KeyW")) MoveForward();
if (state.WasKeyPressed("Space")) Jump();  // edge-triggered
if (state.PointerLocked) Look(state.MouseDX, state.MouseDY);
```

## Architecture

```
C# (Blazor WASM)  →  JS Interop Bridge  →  Browser WebGPU API
```

All JS interop is centralised in a single bridge file. C# holds integer handles to GPU objects. The JS side is a thin executor with no logic. Per-frame handles are automatically cleaned up.

## Project structure

```
WgpuSharp/
├── src/
│   ├── WgpuSharp/              # The library (NuGet package)
│   │   ├── Core/               # Gpu, GpuAdapter, GpuDevice, GpuCanvas, GpuLoop, Input, Enums
│   │   ├── Commands/           # RenderBatch, CommandEncoder, RenderPassEncoder
│   │   ├── Resources/          # GpuBuffer, GpuTexture, GpuSampler, GpuBindGroup
│   │   ├── Pipeline/           # GpuRenderPipeline, GpuComputePipeline, descriptors
│   │   ├── Mesh/               # OBJ/GLB/STL loaders, Mesh, Material, MeshSimplifier
│   │   ├── Scene/              # SceneGraph, SceneNode, Transform, SceneRenderer, cameras,
│   │   │                       # gizmos, picking, sky, grid, ground, lights, undo, scripting
│   │   ├── Interop/            # JsBridge, CommandBatch (internal)
│   │   └── wwwroot/            # WgpuSharp.js (static web asset)
│   └── WgpuSharp.Demo/         # Blazor WASM demo app
│       └── Pages/              # Editor, Triangle, Cube, FlyCamera, ShaderPlayground, etc.
├── tests/
│   └── WgpuSharp.Tests/        # 116 unit tests
└── docs/
    ├── EDITOR_GUIDE.md          # Scene editor reference
    └── LOD_GUIDE.md             # Level of detail guide
```

## Building a scene

1. Start the demo app:
   ```bash
   dotnet run --project src/WgpuSharp.Demo
   ```
2. Open **http://localhost:5212/editor** in Chrome or Edge.
3. Add objects (cubes, spheres, lights, imported models), set tags and scripts in the inspector.
4. Hit **Play** to playtest, **Escape** to return to the editor.
5. Click **Save** in the scene panel to download your scene as `scene.json`.
6. Copy the saved file into `src/WgpuSharp.Demo/wwwroot/scenes/`.
7. Load it directly by opening **http://localhost:5212/editor?scene=scene.json**.

Scenes placed in `wwwroot/scenes/` are bundled into all builds automatically. The `?scene=` parameter loads a scene on startup instead of the default template.

Pick a template (Platformer, Arena, Sandbox) from the bottom of the scene panel to start with a pre-built layout. See [docs/EDITOR_GUIDE.md](docs/EDITOR_GUIDE.md) for the full reference.

### Running the editor as a desktop app

The editor can run as a standalone Electron app with built-in build buttons for packaging your scenes:

```bash
cd electron
npm install
npm run setup   # publishes the Blazor app into electron/www
npm start       # launches the editor
```

From inside the Electron editor, use the **Package** dialog to build web, desktop, or APK targets directly — no terminal needed.

> The `electron/` directory is gitignored. It's a local dev tool, not part of the distributed project.

## Packaging for deployment

The `packaging/build.sh` script produces deployable builds:

```bash
./packaging/build.sh web        # Static site in dist/web — deploy to any host
./packaging/build.sh electron   # Desktop app via Electron
./packaging/build.sh apk        # Android APK via Capacitor
./packaging/build.sh all        # All three targets
```

Add `--aot` for ahead-of-time compilation (slower build, faster runtime). Preview a web build locally with `npx serve dist/web`.

**Prerequisites:** .NET SDK for all targets; Node.js/npm for Electron and APK; Android SDK for APK.

## Building and testing

```bash
dotnet build           # Build everything
dotnet test            # Run all 116 tests
dotnet pack src/WgpuSharp/WgpuSharp.csproj -c Release  # Create NuGet package
```

## Browser support

Requires a WebGPU-capable browser:
- **Chrome/Edge 113+** (stable)
- **Firefox Nightly** (behind flag)
- On Linux, you may need to enable `chrome://flags/#enable-unsafe-webgpu`

## Documentation

- [Editor Guide](docs/EDITOR_GUIDE.md) — full reference for the scene editor
- [LOD Guide](docs/LOD_GUIDE.md) — level of detail system usage

## License

MIT
