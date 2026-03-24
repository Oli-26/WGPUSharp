# WgpuSharp

A clean, idiomatic C# binding layer for the browser **WebGPU API** via **Blazor WebAssembly**.

> **Status:** Alpha (0.1.0-alpha) — API surface may change.

## What it does

WgpuSharp gives you typed GPU access from C# in the browser. No raw JavaScript, no verbose interop — just async C# that mirrors the WebGPU spec.

```csharp
var adapter = await Gpu.RequestAdapterAsync(JS);
var device  = await adapter.RequestDeviceAsync();
var canvas  = await GpuCanvas.ConfigureAsync(device, "my-canvas");

var shader = await device.CreateShaderModuleAsync(wgslCode);
var pipeline = await device.CreateRenderPipelineAsync(descriptor);
```

## Features

- **Rendering** — vertex/index buffers, render pipelines, depth, textures, materials, PBR shading
- **Compute shaders** — storage buffers, dispatch workgroups, GPU read-back
- **Mesh loading** — OBJ, GLB (glTF 2.0), STL with automatic material extraction
- **Materials** — PBR properties, base color textures, MTL file parsing, auto UV generation
- **Input** — keyboard, mouse, pointer lock, scroll wheel
- **Instanced rendering** — instance buffers with per-instance transforms
- **Batched commands** — entire frame in a single JS interop call for performance
- **Game loop** — `requestAnimationFrame`-based with delta time and FPS tracking
- **Type safety** — all WebGPU values use C# enums, zero magic strings
- **Resource management** — `IAsyncDisposable` on all GPU types, automatic per-frame handle cleanup
- **Shader playground** — interactive WGSL editor with 28 templates and live preview

## Running the demos

```bash
git clone <repo-url>
cd WgpuSharp
dotnet run --project src/WgpuSharp.Demo
```

Then open **http://localhost:5212** in Chrome or Edge.

> On Linux, you may need to enable WebGPU: `chrome://flags/#enable-unsafe-webgpu`

### Demo pages

| Page | What it shows |
|------|---------------|
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

3. Add a canvas and render from a Razor component:
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

The batched API collects an entire frame's GPU commands and executes them in a **single JS interop call**, eliminating per-command async overhead:

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

## Loading meshes

```csharp
var meshes = MeshLoader.Load(fileBytes, "model.glb");
var buffers = await meshes[0].CreateBuffersAsync(device);
```

Supports OBJ (with .mtl), GLB (with embedded textures/materials), and STL. Meshes without normals get automatic flat normals. Meshes without UVs get automatic box-projected UVs when textures are applied.

## Input handling

```csharp
var input = await GpuInput.InitAsync(device, "gpu-canvas");

// In your game loop:
var state = await input.GetStateAsync();
if (state.IsKeyDown("KeyW")) MoveForward();
if (state.PointerLocked) Look(state.MouseDX, state.MouseDY);
```

Click the canvas to lock the pointer for FPS-style controls. Press Escape to release.

## Architecture

```
C# (Blazor WASM)  →  JS Interop Bridge  →  Browser WebGPU API
```

All JS interop is centralised in a single bridge file. C# holds integer handles to GPU objects. The JS side is a thin executor with no logic. Per-frame handles (textures, views, encoders) are automatically cleaned up after each frame.

## Project structure

```
WgpuSharp/
├── src/
│   ├── WgpuSharp/              # The library (NuGet package)
│   │   ├── Core/               # Gpu, GpuAdapter, GpuDevice, GpuCanvas, GpuLoop, Input, Enums
│   │   ├── Commands/           # GpuCommandEncoder, RenderPassEncoder, ComputePassEncoder, RenderBatch
│   │   ├── Resources/          # GpuBuffer, GpuTexture, GpuSampler, GpuShaderModule, GpuBindGroup
│   │   ├── Pipeline/           # GpuRenderPipeline, GpuComputePipeline, descriptors
│   │   ├── Mesh/               # OBJ/GLB/STL loaders, Mesh, Material, MtlLoader
│   │   ├── Interop/            # JsBridge, CommandBatch (internal)
│   │   └── wwwroot/            # WgpuSharp.js (static web asset)
│   └── WgpuSharp.Demo/         # Blazor WASM demo app
│       └── Pages/              # Triangle, Cube, ReactionDiffusion, MeshViewer, FlyCamera, ShaderPlayground
└── tests/
    └── WgpuSharp.Tests/        # 48 unit tests (loaders, enums, mesh ops, materials)
```

## Building and testing

```bash
dotnet build           # Build everything
dotnet test            # Run all 48 tests
dotnet pack src/WgpuSharp/WgpuSharp.csproj -c Release  # Create NuGet package
```

## Browser support

Requires a WebGPU-capable browser:
- **Chrome/Edge 113+** (stable)
- **Firefox Nightly** (behind flag)
- On Linux, you may need to enable `chrome://flags/#enable-unsafe-webgpu`

## License

MIT
