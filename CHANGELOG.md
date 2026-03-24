# Changelog

All notable changes to WgpuSharp will be documented in this file.

## [0.2.0-alpha] — 2026-03-24

### Added
- **Alpha blending** — `BlendState` on `ColorTargetState` with presets: `AlphaBlend`, `PremultipliedAlpha`, `Additive`
- **Render-to-texture** — `BeginRenderPass` now accepts `GpuTextureView` for offscreen rendering, shadow maps, and post-processing
- **Canvas resize handling** — `GpuCanvas.ResizeToDisplaySizeAsync()` with `Width`, `Height`, `AspectRatio` properties
- **WebGPU availability check** — `Gpu.IsSupportedAsync()` for graceful fallback
- **GPU adapter info** — `GetInfoAsync()`, `GetFeaturesAsync()`, `GetLimitsAsync()` on `GpuAdapter`
- **Structured shader errors** — `ShaderCompilationException` with line numbers, column positions, and `ShaderMessage[]`
- **GPU error handling** — `GpuException` base type, `GpuDevice` wraps JS errors into typed exceptions
- **Auto-imported namespaces** — NuGet consumers get all WgpuSharp types without `@using` directives
- **Unit tests** — 48 tests covering OBJ/STL/MTL loaders, mesh operations, materials, and enum conversions
- **Shader playground** — interactive WGSL editor with 28 templates, draggable snippets, save/load to localStorage, and tutorial

### Fixed
- **Handle leak in non-batched API** — per-frame objects (textures, views, encoders, passes) now use `storeFrame()` in both batched and async paths, preventing unbounded handle map growth
- **Tab-out crash** — render loop now awaits each frame before scheduling the next, preventing callback pile-up when backgrounded; delta time capped at 100ms

### Changed
- All magic strings replaced with C# enums: `TextureFormat`, `VertexFormat`, `LoadOp`, `StoreOp`, `CompareFunction`, `FilterMode`, `AddressMode`, `BlendFactor`, `BlendOperation`
- All GPU resource types implement `IAsyncDisposable` — buffers and textures call `destroy()` on the GPU object
- All public methods validate arguments with `ArgumentNullException`, `ArgumentOutOfRangeException`, `ObjectDisposedException`
- `RenderBatch.BeginRenderPass` accepts `LoadOp`/`StoreOp` parameters instead of hardcoded values
- `GpuCanvas.CreateDepthTextureAsync` uses cached `Width`/`Height` instead of querying DOM
- Dark mode app-wide in demo site

## [0.1.0-alpha] — 2026-03-24

### Added
- Core WebGPU binding: adapter, device, canvas, queue
- Rendering: vertex/index buffers, render pipelines, depth buffer, textures, samplers, materials
- Compute shaders: compute pipelines, storage buffers, dispatch workgroups, GPU read-back
- Mesh loading: OBJ (with MTL), GLB (with PBR materials and embedded textures), STL (binary and ASCII)
- Materials: PBR properties, base color textures, auto box-projected UV generation
- Input handling: keyboard, mouse, pointer lock, scroll wheel
- Instanced rendering via instance vertex buffers
- Batched command execution: `RenderBatch` for single-JS-call-per-frame rendering
- Game loop: `GpuLoop` with `requestAnimationFrame`, delta time, FPS tracking
- Per-frame handle cleanup for batched path
- JS bridge with handle registry
- NuGet package metadata, README, MIT license
- Demo apps: triangle, spinning cube, reaction-diffusion, mesh viewer, fly camera, shader playground
