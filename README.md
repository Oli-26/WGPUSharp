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

## Quick start

1. Add the NuGet package to your Blazor WASM project:
   ```
   dotnet add package WgpuSharp
   ```

2. Add the JS bridge to your `index.html`:
   ```html
   <script src="_content/WgpuSharp/WgpuSharp.js"></script>
   ```

3. Add a canvas and render from a Razor component:
   ```razor
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

## Loading meshes

```csharp
var meshes = MeshLoader.Load(fileBytes, "model.glb");
var buffers = await meshes[0].CreateBuffersAsync(device);
```

Supports OBJ (with .mtl), GLB (with embedded textures/materials), and STL.

## Input handling

```csharp
var input = await GpuInput.InitAsync(device, "gpu-canvas");

// In your game loop:
var state = await input.GetStateAsync();
if (state.IsKeyDown("KeyW")) MoveForward();
if (state.PointerLocked) Look(state.MouseDX, state.MouseDY);
```

## Architecture

```
C# (Blazor WASM)  →  JS Interop Bridge  →  Browser WebGPU API
```

All JS interop is centralised in a single bridge file. C# holds integer handles to GPU objects. The JS side is a thin executor with no logic.

## Browser support

Requires a WebGPU-capable browser:
- **Chrome/Edge 113+** (stable)
- **Firefox Nightly** (behind flag)
- On Linux, you may need to enable `chrome://flags/#enable-unsafe-webgpu`

## License

MIT
