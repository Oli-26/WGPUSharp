# .NET 10 Testing and Quality Skill

## Mandatory quality gate

For every feature, bug fix, refactor that changes behavior, or new engine subsystem:

- Add or update automated tests.
- Maintain at least **80% line coverage**.
- Maintain at least **80% branch coverage**.
- Test **all business rules and every meaningful variation**, not merely the happy path.
- Test cancellation, disposal and lifecycle behavior for resource-owning asynchronous components.
- Test invalid inputs, boundaries, missing resources, repeated operations and failure recovery where applicable.

Coverage thresholds are minimum gates. Do not weaken a test or exclude code from coverage simply to satisfy the percentage.

## .NET 10 coding standards

- Prefer collection expressions such as `[]` and `[item1, item2]` where the target type is known and readability is preserved.
- Prefer primary constructors for dependency-bearing services and small immutable components when they improve clarity.
- Use `async`/`await` for asynchronous APIs and I/O. Never introduce synchronous waits on tasks.
- Propagate `CancellationToken` through every layer that can meaningfully cancel work. Do not create `new CancellationToken()` or silently replace a caller token without a deliberate boundary reason.
- Use `IAsyncDisposable` for asynchronous cleanup and `IDisposable` for synchronous cleanup. Dispose `DotNetObjectReference`, JS modules, streams, timers, subscriptions, GPU/audio ownership wrappers and cancellation sources when owned.
- Preserve nullable reference type correctness and avoid null-forgiving operators as a substitute for validation.
- Prefer immutable/read-only contracts at boundaries and explicit DTOs for serialized scene data.

## Test design

For each rule, enumerate its state space before writing tests. Example dimensions include:

- valid/invalid;
- empty/non-empty;
- first/repeated call;
- before/after initialization;
- cancellation before/during/after work;
- resource available/missing;
- scene active/inactive/disposed;
- device available/lost;
- audio enabled/disabled and spatial/non-spatial;
- supported/unsupported GPU feature.

Avoid tests that only verify implementation details. Prefer observable engine behavior and deterministic state transitions.

## Interop testing

C# tests should use an abstraction around JS interop so engine rules can be tested without a browser. Integration tests should verify the actual JS contract when infrastructure exists. Contract changes must update both sides together.
