# Scene Runtime and Asset Loading Skill

## Objective

Guide implementation of a complete scene runtime on top of the existing WgpuMaui renderer without prematurely rewriting the low-level WebGPU layer.

## Current state assessment

The C# side already has a scene graph with roots, hierarchy traversal, cached all-node/visible-mesh/light collections, selection, and world-transform updates. The project package description also identifies cameras, lights, collision/gravity/jumping, mesh loading, LOD, skeletal animation, scripting, tags, undo/redo, serialization and instanced rendering as existing engine/editor capabilities. The JS module is already a low-level WebGPU bridge with handle storage, adapter/device lifecycle, canvas configuration, buffers, textures, samplers, bind groups, render pipelines, command encoders/render passes, input state and a game-loop mechanism.

Do not duplicate those capabilities. First identify the existing C# owner for each feature and add only the missing orchestration/boundary pieces.

## Scene instantiation contract

A Wgpu view should expose an explicit lifecycle such as:

`Created -> CanvasReady -> GpuReady -> SceneLoading -> SceneReady -> Running -> Stopping -> Disposed`

When the view becomes ready, C# should request/load the selected scene document. The loader should:

1. obtain scene JSON from embedded resources, local storage/file, or backend;
2. validate schema/version;
3. deserialize a DTO/manifest rather than directly trusting runtime objects;
4. resolve assets by stable IDs/URIs;
5. create the scene graph and components;
6. create/configure cameras and choose the active camera;
7. configure lights/environment;
8. load meshes/materials/textures/shaders;
9. initialize animations and animation controllers;
10. initialize audio emitters/environment audio;
11. initialize physics/collision components;
12. initialize scripts/behaviors with safe lifecycle ownership;
13. create GPU resources and renderer bindings;
14. publish a scene-ready state;
15. start/update the game loop.

Each stage should have cancellation and meaningful error reporting. If stage N fails, dispose resources created by stages 1..N-1.

## Backend scene loading

Do not couple `WgpuMaui.js` to HTTP or backend APIs. JS owns browser/GPU capabilities; C# owns scene acquisition. Use an injected scene repository/client in C#. This allows embedded, local and remote sources to share the same loader pipeline.

Remote loading should support cancellation, timeouts, version/ETag policies where useful, and asset caching. Never block the UI thread.

## Scene schema evolution

Use a versioned JSON contract. Unknown optional fields should be safely ignored where compatibility permits. Breaking schema versions should fail with a diagnostic that identifies the scene and version. Stable asset IDs should remain independent from runtime GPU handles.

## Cameras

A scene should serialize camera type and parameters (for example perspective/orthographic, FOV/near/far or orthographic size), transform, viewport configuration and active-camera identity. The runtime should update the renderer from the active camera, while the editor may switch active cameras without changing serialized state until explicitly committed.

The active camera should also drive the audio listener unless a scene explicitly overrides the listener.

## Animation

Treat animation as a component/system, not as arbitrary callbacks from the renderer. Serialize clip references, playback state, speed, looping and blending/controller state. Keep animation timing deterministic and tied to the engine frame/update delta. GPU buffer updates should be batched.

## Audio

Model audio as scene components:

- `AudioEnvironment`: ambient beds, mixer/bus configuration, master/environment gain and optional reverb/spatial environment settings.
- `AudioEmitter`: clip ID, loop, autoplay, volume/gain, pitch, spatialization, attenuation distances, playback state and optional bus.
- `AudioListener`: normally follows the active camera.

The JS layer should expose a Web Audio capability API rather than encode scene rules. A C# audio system should translate scene components into JS resources and update positions/orientation from world transforms. Scene unload must stop and release all scene-owned audio nodes.

## Recommended missing JS capabilities

Prioritize additions in this order:

1. explicit renderer/view lifecycle and idempotent initialization;
2. resource ownership/release and device-loss cleanup;
3. GPU error scopes and uncaptured error reporting;
4. efficient bulk buffer/texture upload paths;
5. audio context/listener/emitter/bus API using Web Audio;
6. render-target/depth/compute/indirect/instancing capabilities only where C# already has a consumer;
7. asset decode helpers where browser APIs materially improve performance;
8. diagnostics/frame timing/resource statistics.

Do not add speculative functions merely because WebGPU has an API. Each JS function must have a concrete C# consumer and tests or a demonstrable integration path.

## Audio JS API shape

Prefer a small capability surface such as:

- initialize/resume audio context;
- create/remove audio buffer by stable handle;
- create/destroy emitter;
- set emitter buffer, transform, gain, pitch, loop, attenuation and spatialization;
- play/stop/pause/seek emitter;
- create/configure/destroy mixer buses;
- set listener transform;
- stop all scene-owned audio resources.

Use stable engine IDs mapped to JS handles. Do not serialize JS handles in scene JSON.

## Resource ownership

Every scene load creates an ownership scope. Resources allocated for that scene must be released when the scene is replaced, cancelled or the view is disposed. Shared asset caches require reference counting or another explicit ownership strategy so replacing one scene does not destroy assets still used by another.

## Concurrency

Guard against overlapping scene loads. A newer request must cancel or supersede an older request. Never allow an older asynchronous continuation to install its scene after a newer load has completed.

## Validation checklist

Before implementing a feature, identify:

- existing C# service/model that owns the behavior;
- JS function already providing the low-level capability;
- missing interop operation, if any;
- lifecycle owner and disposal path;
- cancellation path;
- serialization contract;
- tests for all business-rule variations;
- line/branch coverage impact.
