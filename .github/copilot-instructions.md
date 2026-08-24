# WgpuMaui Copilot Instructions

## Project

WgpuMaui is a .NET 10 MAUI + Blazor WebView WebGPU game-engine/editor foundation. Read the applicable skills under `.github/skills/` before changing engine, scene, interop or editor code.

## Required skills

- `.github/skills/wgpu-maui-engine/SKILL.md` for engine architecture, WebGPU interop and lifecycle.
- `.github/skills/scene-runtime/SKILL.md` for scene loading, assets, cameras, animation and audio.
- `.github/skills/mud-wasm/SKILL.md` for MudBlazor editor UI. **Always use `using MudColor = MudBlazor.Color;` when MudBlazor Color is used in code that also has MAUI Color in scope.**
- `.github/skills/testing-dotnet10/SKILL.md` for tests, coverage and .NET 10 conventions.

## Non-negotiable engineering rules

1. Target .NET 10 and follow current C#/.NET 10 idioms.
2. Prefer `[]` collection expressions where applicable.
3. Use `async`/`await`; do not synchronously block asynchronous work.
4. Propagate `CancellationToken` through asynchronous feature boundaries.
5. Prefer primary constructors when appropriate.
6. Implement `IDisposable`/`IAsyncDisposable` whenever ownership requires deterministic cleanup.
7. Keep C# authoritative for scene/domain/game rules and JavaScript authoritative only for browser/WebGPU capabilities.
8. Do not serialize browser/GPU handles in scene JSON.
9. Every feature must have tests covering all business rules and variations and must preserve **>=80% line and >=80% branch coverage**.
10. Do not add speculative JS functions without a concrete C# consumer and lifecycle/ownership semantics.
11. Avoid per-frame allocations and unnecessary C#↔JS interop calls.
12. On scene replacement or view disposal, cancel pending work and release scene-owned resources.
13. Make scene loading concurrency-safe; stale asynchronous loads must not overwrite a newer scene.
14. Do not couple engine/domain code to MudBlazor components.
15. Before editing, inspect the existing C# implementation and `src/WgpuMaui/wwwroot/WgpuMaui.js` and reuse existing capabilities.
