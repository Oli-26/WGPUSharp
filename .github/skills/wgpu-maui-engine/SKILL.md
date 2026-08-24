# WgpuMaui Game Engine Development Skill

## Scope

Use this skill when implementing or modifying the WgpuMaui .NET 10 MAUI + Blazor WebView game engine, renderer, scene system, editor integration, or JavaScript WebGPU interop.

## Architecture rules

- Keep the engine core platform-oriented and deterministic; keep MAUI lifecycle concerns at the integration boundary.
- Treat C# as the authoritative scene/domain model. JavaScript/WebGPU is the GPU execution and browser-host boundary.
- Do not move game rules into `WgpuMaui.js` merely to avoid an interop call.
- Keep the JS API explicit and handle-based. Every persistent GPU/browser resource must have a defined owner and release path.
- Frame-scoped resources may be released automatically at frame end; persistent resources must not use frame handles.
- Propagate `CancellationToken` through asynchronous scene/resource loading and lifecycle operations.
- Use `async`/`await` for I/O, GPU/browser operations that are asynchronous, and scene/resource loading. Never block with `.Result`, `.Wait()`, or equivalent.
- Prefer primary constructors where they improve dependency declaration and do not obscure lifecycle/resource ownership.
- Prefer collection expressions (`[]`) where supported and readable by .NET 10.
- Implement `IDisposable`/`IAsyncDisposable` whenever a type owns subscriptions, JS object references, GPU resources, timers, streams, DotNetObjectReference instances, or other resources requiring deterministic cleanup.

## Testing gate

Every feature must include tests for business rules and all meaningful variations, including success, failure, boundary, empty/null, cancellation, repeated invocation, and lifecycle paths where applicable.

Minimum coverage gate: **80% line coverage and 80% branch coverage** for the affected testable code. Coverage is a floor, not a substitute for behavior tests. A feature is incomplete when a business-rule variation is untested even if coverage exceeds 80%.

## Scene lifecycle

The target engine lifecycle is:

1. Instantiate the Wgpu view and bind its canvas.
2. Initialize WebGPU adapter/device/context.
3. Notify C# when the renderer is ready.
4. Resolve the scene source in C# (embedded/local/backend JSON).
5. Deserialize and validate the scene manifest.
6. Resolve/load assets asynchronously: meshes, textures, materials, shaders, animations, audio and scripts.
7. Create/configure cameras, lights, renderable nodes, colliders and environment settings.
8. Build GPU resources and bind groups through the JS interop boundary.
9. Attach runtime components and behavior systems.
10. Start the game loop only after the scene is fully ready, unless explicit progressive loading is designed.
11. On scene change/unmount, cancel pending work and dispose resources owned by the scene.

Scene loading must be idempotent or explicitly reject concurrent loads. A stale asynchronous load must never overwrite a newer scene.

## Scene JSON contract

Design the scene document as a versioned, forward-compatible manifest. It should be able to describe:

- scene metadata and version;
- environment/sky/background/fog/post-processing settings;
- node hierarchy, transforms and visibility;
- mesh/material/texture references;
- cameras and active-camera selection;
- lights and shadow configuration;
- animation clips, controllers and initial playback state;
- audio environment and component-level audio emitters;
- physics/collision configuration;
- scripts/components and serialized parameters;
- tags/layers and editor metadata.

Do not serialize transient GPU handles, DOM references, `GPUDevice`, `GPUBuffer`, `GPUTexture`, render passes, or browser objects. Serialize stable asset/resource identifiers and reconstruct runtime resources after deserialization.

## JS/WebGPU boundary

`src/WgpuMaui/wwwroot/WgpuMaui.js` currently exposes the low-level resource/command API, canvas setup, shader compilation diagnostics, input state, render loop, and WebGPU handles. Extend it deliberately rather than creating a second unmanaged rendering abstraction.

Recommended future API groups:

- resource lifecycle: explicit retain/release, resource ownership and device-loss cleanup;
- scene bootstrap: canvas/renderer readiness callback and scene-generation identifier;
- rendering: depth/stencil, compute, indirect drawing, instancing, texture arrays, mip generation, render targets and post-processing;
- assets: binary/URL/blob loading, image decode, streaming and cache coordination;
- animation: GPU/CPU-friendly buffer updates and animation timing hooks;
- audio: Web Audio context initialization, listener state, positional emitters, looping/one-shot playback, gain, pitch, attenuation, spatialization, environment buses and cleanup;
- input: keyboard/mouse/pointer-lock/gamepad abstraction with focus/lifecycle handling;
- diagnostics: GPU error scopes, uncaptured errors, device loss, timestamps where supported, frame statistics and resource counters.

Do not put scene orchestration, gameplay rules, editor state, or business decisions into JS. JS should expose capabilities; C# should compose them into engine behavior.

## Audio architecture guidance

Support two complementary levels:

- **Component/emitter audio**: an audio source attached to a scene node, with clip, loop, gain, pitch, spatialization, min/max distance and playback state.
- **Environment audio**: scene-level ambient beds, reverb/environment settings, mixer/bus routing and global volume.

The runtime should update emitter transforms from scene-node world transforms. The listener should normally follow the active camera. Audio assets should be referenced by stable IDs and loaded asynchronously. Scene unload must stop/release scene-owned sources.

For browser compatibility, audio context creation/resume must respect user-gesture/autoplay restrictions.

## Editor integration

The editor already contains scene editing capabilities. New engine features must expose editor-friendly metadata without coupling engine domain objects to MudBlazor controls. Prefer commands/services/view models for editor operations and keep rendering state separate from UI state.

## Performance

Avoid per-frame allocations in hot paths. Cache scene traversal/render lists, reuse buffers, batch draw calls where practical, and avoid unnecessary C#↔JS round trips. Prefer sending immutable/batched frame data over many tiny interop calls.

## Error handling

GPU/device errors, invalid scene documents, missing assets, unsupported features and cancellation must be distinguishable. Do not silently swallow failures. Device loss must invalidate/rebuild affected resources and notify C#.
