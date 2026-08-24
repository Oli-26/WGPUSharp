# mud-wasm

## Scope

Use this skill for the WgpuMaui editor UI implemented with MudBlazor in Blazor/WASM-style Razor components hosted by MAUI Blazor WebView.

## Mandatory naming rule

MAUI and MudBlazor both expose `Color`. To prevent ambiguity, always import MudBlazor's color enum with an alias:

```csharp
using MudColor = MudBlazor.Color;
```

Use `MudColor` for MudBlazor `Color` values. Do not write `using MudBlazor;` and then use an unqualified `Color` when MAUI namespaces are in scope. If a MAUI color is required, keep it explicitly qualified or use a separate intentional alias.

## UI rules

- Prefer MudBlazor components over hand-written HTML/CSS for editor controls when a MudBlazor component provides the required behavior.
- Keep engine/domain classes independent from MudBlazor. UI components consume services/view models; engine types must not reference UI component types.
- Do not place rendering/gameplay logic inside Razor markup. Keep event handlers thin and delegate to services/commands.
- Use `@inject`/DI for services rather than constructing engine services in components.
- Respect Blazor component lifecycle. JS interop that requires rendered DOM/canvas elements belongs after render and must be guarded against repeated initialization.
- Dispose `IJSObjectReference`, `DotNetObjectReference`, subscriptions and timers owned by a component.
- Use `IAsyncDisposable` when JS interop cleanup is asynchronous.
- Avoid long synchronous work in component lifecycle methods.
- Propagate `CancellationToken` from component/service operations that can outlive the component.

## WebAssembly/Blazor constraints

Treat JS interop as an asynchronous boundary. Do not assume a DOM element exists during initialization. Canvas/WebGPU initialization must tolerate rerendering and component disposal.

Avoid excessive JS interop calls in render loops. Batch state updates and keep high-frequency rendering/input data on the engine side where possible.

## MudBlazor editor patterns

- Use `MudPaper`, `MudStack`, `MudGrid`, `MudTabs`, `MudSelect`, `MudTextField`, `MudNumericField`, `MudSlider`, `MudIconButton`, `MudToolBar`, dialogs and other appropriate MudBlazor primitives consistently.
- Keep editor state separate from engine runtime state. Apply changes through explicit commands/services.
- For color controls, use `MudColor` everywhere the MudBlazor enum is expected.
- Do not introduce another UI framework to solve a problem already covered by MudBlazor.
- Prefer strongly typed component parameters and event callbacks.
- Keep localization through the existing localization abstraction rather than embedding user-facing strings in engine code.

## Testing

UI behavior that encodes business/editor rules must be tested. Features must achieve at least **80% line and 80% branch coverage** in affected testable code and cover every business-rule variation. Include lifecycle cases such as first render, rerender, disposal, failed JS initialization, cancellation, and repeated initialization when applicable.

## .NET 10 style

Prefer collection expressions `[]`, primary constructors, `async`/`await`, `CancellationToken` propagation, nullable reference types and deterministic disposal. Do not introduce synchronous blocking to work around Blazor lifecycle or JS interop behavior.
