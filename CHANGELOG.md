# Changelog

All notable changes to WgpuSharp will be documented in this file.

## [0.4.0-alpha] — 2026-03-27

### Added — Scene Editor & Game Engine
- **Scene graph** — transform hierarchy with parent/child nodes, dirty-flag caching, world matrix propagation
- **Scene editor** (`/editor`) — three-panel layout with hierarchy, viewport, and inspector
- **Orbit camera** — click-drag to orbit, shift-drag to pan, scroll to zoom, camera bookmarks (Ctrl+1-4)
- **FPS camera** — play mode with WASD movement, mouse look, jumping, gravity, AABB collision, wall sliding
- **Gizmo tools** — translate (W), rotate (E), scale (R) with axis arrows, hover highlighting, drag interaction
- **Multi-select** — shift+click, Ctrl+A, right-drag box select, group move/delete/duplicate/align/distribute
- **Grid snapping** — configurable position snap (0.1-2 units), rotation snap (15-degree increments)
- **Undo/redo** — command pattern covering all operations (Ctrl+Z/Y)
- **13 gameplay tags** — Static, Collectible, Trigger, Enemy, MovingPlatform, Door, Key, Checkpoint, Teleporter, DamageZone, HealthPickup, NPC, AudioSource
- **Per-object scripting** — text-based script editor with 9 commands (Rotate, Bob, FollowPlayer, OnEnter, OnEnterToggle, SetColor, Scale, Orbit, LookAtPlayer) and 14 templates
- **Point lights** — up to 8 dynamic point lights with color, intensity, range; forward rendering with multi-light fragment shader
- **Procedural sky** — gradient shader with zenith/horizon/ground colors, sun disc, atmospheric haze
- **Lit ground plane** — checkerboard pattern receiving point light illumination, alpha edge fade
- **Editor grid** — wireframe XZ grid with major/minor lines and colored axis indicators (R=X, G=Y, B=Z)
- **Scene settings** — configurable sky colors, sun direction/color/intensity, ambient light, fog (color/start/end), level timer
- **Scene serialization** — save/load to JSON with embedded imported mesh data (base64), all node properties preserved
- **Scene templates** — Empty, Sandbox, Platformer, Arena presets with New Scene picker
- **3D model import** — OBJ/GLB/STL via file picker with Web Worker for off-thread reading, progress bar
- **Material presets** — 15 named presets (Brick, Metal, Wood, Glass, etc.) with color application
- **Object properties** — solid toggle (walkthrough), locked toggle (prevent edits), visibility, per-node color
- **Play mode** — FPS walkthrough with score HUD, health bar, minimap, timer countdown, game over, checkpoint respawn
- **Copy/paste** (Ctrl+C/V), **screenshot** export (PNG), **F3 debug stats** overlay
- **Drag-drop hierarchy** — reparent nodes by dragging in the scene panel
- **Spatial audio** — positional Web Audio sources with distance attenuation

### Added — Mesh Processing
- **Mesh simplification** — `MeshSimplifier` with vertex clustering algorithm for automatic LOD generation
- **LOD system** — `SceneNode.LodMeshes` with distance-based swapping (20/40 unit thresholds), auto-generated for imported models
- **OBJ loader rewrite** — span-based zero-allocation parsing, 3-5x faster for large files

### Added — Rendering
- **Environment uniform buffer** — shared across scene/ground/sky shaders for live scene settings
- **Distance fog** — configurable start/end distance with color blending in scene and ground shaders
- **Renderer optimizations** — reused per-frame collections (zero-allocation render loop), cached sky VP, gizmo skip-update, eliminated redundant UpdateTransforms calls

### Fixed
- Gizmo pipeline missing DepthStencilState (WebGPU validation error)
- Pointer lock SecurityError on rapid re-acquisition
- Player never respawning after death (empty respawn block)
- ExitPlayMode not resetting gameplay state (collected items, keys, triggers leaked)
- Division by zero in gizmo ray-line closest-point math
- Redundant ternary in ScriptEngine OnEnter message
- Missing closing `>` on sidebar and inspector div elements
- Gameplay methods running after ExitPlayMode in same frame
- ScreenPointToRay division by zero when canvas size is 0

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
