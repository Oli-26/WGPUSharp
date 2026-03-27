# Scene Editor Development Log

## Loop 1 — Editor Grid + Axis Indicators

**Feature:** Wireframe grid on XZ plane with colored axis lines (R=X, G=Y, B=Z)

**What was built:**
- `EditorGrid.cs` — standalone line renderer with its own WGSL shader and pipeline (`PrimitiveTopology.LineList`)
- Grid: 20x20 units, 1-unit spacing, major lines every 5 units (brighter), alpha-blended
- Axis lines: red X, green Y (vertical), blue Z — full grid extent
- Integrated into `SceneRenderer` via shared uniform buffer and single render pass (grid draws first, scene on top)
- Removed solid floor plane from default scene — grid replaces it as spatial reference
- Added a cylinder to default scene for variety

**Files changed:**
- `src/WgpuSharp/Scene/EditorGrid.cs` (new)
- `src/WgpuSharp/Scene/SceneRenderer.cs` (added Grid property, multi-draw encoder)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (updated default scene)

**Also fixed this session:**
- Pointer lock SecurityError (try-catch in JS bridge)

## Loop 2 — Scene Save/Load (JSON Serialization)

**Feature:** Persist scenes to JSON files and reload them — the editor now has persistence.

**What was built:**
- `SceneSerializer.cs` — serializes/deserializes Scene graph to/from JSON
  - Stores: node names, mesh types, transforms (position/rotation/scale), colors, visibility, hierarchy
  - Compact: omits default values (identity rotation, unit scale, white color)
  - `MeshType` string tag on `SceneNode` maps to primitive type for recreation on load
- `SceneData` / `NodeData` — clean JSON DTOs with camelCase naming
- `Rebuild()` method with mesh resolver function — decouples serialization from GPU resources
- Save button: serializes scene → triggers browser file download as `scene.json`
- Load button: opens file picker → reads JSON → rebuilds entire scene graph
- JS helpers (`WgpuSharpEditor.downloadFile`, `pickAndReadFile`) in index.html

**Files changed:**
- `src/WgpuSharp/Scene/SceneSerializer.cs` (new)
- `src/WgpuSharp/Scene/SceneNode.cs` (added MeshType property)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (Save/Load buttons, ResolveMesh, MeshType tags)
- `src/WgpuSharp.Demo/wwwroot/index.html` (JS file download/upload helpers)

## Loop 3 — Keyboard Shortcuts

**Feature:** Edge-triggered keyboard shortcuts for fast editor workflow.

**Shortcuts implemented:**
| Key | Action |
|---|---|
| Delete / Backspace | Delete selected node |
| Ctrl+D | Duplicate selected node |
| F | Focus camera on selected node (or origin if none) |
| Escape | Deselect all |
| Arrow Left/Right | Nudge selected ±X (or ±Z with Shift) |
| Arrow Up/Down | Nudge selected ±Z (or ±Y with Shift) |

**What was built:**
- Extended JS input system with edge-triggered `keyDownEvents` array (cleared after each poll)
- Added `ctrlKey` / `shiftKey` modifier tracking to InputState
- Smart filtering: keyboard shortcuts don't fire when typing in inspector inputs (checks `e.target.tagName`)
- Prevents browser defaults for editor keys (Delete, Backspace, Ctrl+D, etc.)
- `WasKeyPressed(code)` helper on `InputState` for one-shot event checks
- `FocusOn(worldPosition)` method on `OrbitCamera` — snaps orbit target to a world point
- Nudge system: arrows move selected node by 0.25 units, Shift modifier switches axis
- Toolbar shows shortcut hints

**Files changed:**
- `src/WgpuSharp/wwwroot/WgpuSharp.js` (keyDownEvents, modifier tracking, preventDefault)
- `src/WgpuSharp/Core/Input.cs` (KeyDownEvents, CtrlKey, ShiftKey, WasKeyPressed)
- `src/WgpuSharp/Scene/OrbitCamera.cs` (FocusOn method, removed hardcoded F key)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (ProcessShortcuts, NudgeSelected, toolbar text)

## Loop 4 — Viewport Object Picking (Raycasting)

**Feature:** Click objects in the 3D viewport to select them. The editor finally feels like a real 3D editor.

**What was built:**
- `Picking.cs` — CPU-side raycasting system:
  - `Ray` struct (origin + direction)
  - `AABB` struct (min/max, unit box matching primitive sizes)
  - `ScreenPointToRay()` — unprojects mouse pixel through inverse view-projection matrix
  - `PickNode()` — tests ray against all visible mesh nodes, returns nearest hit
  - `RayIntersectsAABB()` — slab method, transforms ray into local space for correct hit testing with scaled/rotated objects
- Click detection in JS input system:
  - `clickEvents` array — fires only for non-drag clicks (<5px mouse movement between down/up)
  - `_mouseDownPos` tracking for drag vs click discrimination
- Pointer lock reworked for editor workflow:
  - Single click = pick object (no pointer lock)
  - Drag (>3px movement) = locks pointer for orbit/pan, releases on mouseup
  - Removed old click-to-lock handler — pointer lock is now drag-only
- `ClickEvent` C# type with X, Y, Button
- `ProcessPicking()` in editor — casts ray on left-click, selects nearest hit or deselects on miss

## Loop 5 — Translation Gizmo (Drag-to-Move)

**Feature:** Three colored axis arrows at the selected object. Click-drag an arrow to translate along that axis.

**What was built:**
- `TranslateGizmo.cs` — full gizmo system:
  - **Rendering:** 3 axis arrows (red=X, green=Y, blue=Z) with arrowheads, rendered as LineList with no depth test (always on top)
  - **Scale-invariant:** gizmo stays the same screen size regardless of camera distance
  - **Hover highlighting:** axis brightens when mouse hovers over it
  - **Hit testing:** ray-line closest-point distance test for each axis
  - **Drag mechanics:** projects mouse ray onto the constraint axis in world space, computes delta from drag start
  - `BeginDrag()` / `UpdateDrag()` / `EndDrag()` API
- Pointer lock blocking during gizmo drag:
  - `setPointerLockBlocked()` JS function prevents pointer lock while dragging gizmo
  - Ensures screen-space mouse coordinates remain available during drag
  - Automatically unblocks on drag end
- `mouseDownEvents` added to input system (edge-triggered mousedown, for drag start detection)
- Gizmo integrated into `SceneRenderer` — renders after scene objects in same pass
- Editor wires up: mousedown on axis → begin drag → continuous position update → mouseup ends drag
- Gizmo interaction takes priority over camera orbit and object picking

**Files changed:**
- `src/WgpuSharp/Scene/TranslateGizmo.cs` (new — rendering, hit testing, drag logic)
- `src/WgpuSharp/Scene/SceneRenderer.cs` (Gizmo property, renders gizmo in pass)
- `src/WgpuSharp/wwwroot/WgpuSharp.js` (mouseDownEvents, pointerLockBlocked, setPointerLockBlocked)
- `src/WgpuSharp/Core/Input.cs` (MouseDownEvents on InputState)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (ProcessGizmo, gizmo drag flow)

**Files changed:**
- `src/WgpuSharp/Scene/Picking.cs` (new — Ray, AABB, raycasting)
- `src/WgpuSharp/wwwroot/WgpuSharp.js` (clickEvents, drag-to-lock pointer, release on mouseup)
- `src/WgpuSharp/Core/Input.cs` (ClickEvent type, ClickEvents on InputState)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (ProcessPicking integration)

## Loop 6 — Undo/Redo System

**Feature:** Command-pattern undo/redo stack covering all editor operations. Ctrl+Z to undo, Ctrl+Y / Ctrl+Shift+Z to redo. The editor now has a safety net.

**What was built:**
- `UndoStack.cs` — core undo/redo infrastructure:
  - `IEditorAction` interface with `Execute()` / `Undo()` / `Description`
  - `UndoStack` with `Do()`, `Push()`, `Undo()`, `Redo()`, `Clear()`
  - Max 100 actions in history, redo stack cleared on new action
  - `CanUndo` / `CanRedo` properties for UI button state
  - `UndoDescription` / `RedoDescription` for button tooltips
- Action types:
  - `TransformAction` — records position, rotation, scale before/after. Has `Move()` static helper.
  - `AddNodeAction` — records adding a node (supports parent for reparented adds)
  - `DeleteNodeAction` — records deletion with parent reference for re-insertion on undo
  - `ColorAction` — records color change before/after
- All editor operations wrapped in undo actions:
  - Add primitive (AddNodeAction)
  - Delete node (DeleteNodeAction)
  - Duplicate node (AddNodeAction)
  - Arrow nudge (TransformAction)
  - Gizmo drag (TransformAction via Push on drag end)
  - Inspector position/rotation/scale changes (TransformAction)
  - Inspector color change (ColorAction)
- Keyboard shortcuts: Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z
- UI: Undo/Redo buttons in viewport toolbar with disabled state and tooltips

**Files changed:**
- `src/WgpuSharp/Scene/UndoStack.cs` (new — IEditorAction, UndoStack, all action types)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (undo integration in all mutations)
- `src/WgpuSharp.Demo/wwwroot/css/app.css` (toolbar button styles)

## Loop 7 — Play Mode (FPS Camera)

**Feature:** Press Play to enter the scene as a first-person player. Walk around the world you've built. Press Stop (or Escape) to return to the editor.

**What was built:**
- `FpsCamera.cs` — reusable first-person camera:
  - WASD movement, mouse look (pointer-locked), Space/Shift vertical
  - Scroll to change speed (1-50 units/sec range)
  - `InitFromOrbitCamera()` — spawns player at orbit target with matching look direction, 1.6m eye height
  - `WriteViewProjection()` for renderer compatibility
- `SceneRenderer` dual-camera support:
  - `Render(batch, scene, OrbitCamera, ...)` — editor mode with grid + gizmo
  - `Render(batch, scene, FpsCamera, ...)` — play mode without editor overlays
  - Internal `RenderWithVP()` shared method with `showEditorOverlays` flag
  - Grid and gizmo hidden during play mode
- Editor mode switching:
  - Green **Play** button in toolbar → enters play mode
  - Red **Stop** button (or **Escape** key) → exits play mode
  - Side panels dimmed and non-interactive during play (CSS `pointer-events: none`)
  - Toolbar turns green-tinted with pulsing "PLAYING" label
  - Help text shows FPS controls (WASD, mouse, Space/Shift, scroll)
- Input routing branched: play mode processes FPS camera only, editor mode processes orbit + gizmo + picking + shortcuts

**Files changed:**
- `src/WgpuSharp/Scene/FpsCamera.cs` (new — FPS camera with movement/look)
- `src/WgpuSharp/Scene/SceneRenderer.cs` (dual render overloads, showEditorOverlays)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (play/stop, mode branching, UI)
- `src/WgpuSharp.Demo/wwwroot/css/app.css` (play mode visuals, dimming, button colors)

## Loop 8 — Grid Snapping

**Feature:** All object placement snaps to a configurable grid. Walls align, floors tile, levels look intentional instead of approximate.

**What was built:**
- `SnapPosition()` / `SnapValue()` helpers — round to nearest grid increment
- Snap integrated into every movement path:
  - **Gizmo drag** — snaps continuously as you drag along an axis
  - **Arrow nudge** — steps by snap size instead of fixed 0.25, snaps result to grid
  - **Inspector position input** — typed values snap to grid
  - **New primitive spawn** — placed at snapped position
- Snap toggle:
  - Blue **Snap** button in toolbar (on by default)
  - **G** keyboard shortcut to toggle
  - Dropdown to pick grid size: 0.1, 0.25, 0.5 (default), 1, 2
- Visual feedback: Snap button highlights blue when active, shows current grid size

**Design decisions:**
- Snap is always-on by default (matches Unity/Godot convention for level design)
- Snap applies to the final position, not the delta — so objects always land on grid points
- Rotation and scale are not snapped (position snap is the 80/20 for level design)

**Files changed:**
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (snap state, helpers, integration in all movement paths, toolbar UI, G shortcut)
- `src/WgpuSharp.Demo/wwwroot/css/app.css` (snap button/select styles)
- `src/WgpuSharp/wwwroot/WgpuSharp.js` (added KeyG to preventDefault list)

## Loop 9 — Point Lights as Scene Objects

**Feature:** Place point lights in the scene that illuminate nearby objects with color and falloff. The biggest visual leap — scenes now look like game worlds instead of flat colored blocks.

**What was built:**
- `PointLightData` class on `SceneNode` — color (RGB), intensity, range
- Multi-light forward rendering shader:
  - `LightData` uniform struct at `@group(0) @binding(1)` with count + array of 8 `PointLight` structs
  - Each light: position (from node transform), color, intensity
  - Smooth quadratic attenuation: `intensity / (1 + dist² * 0.2)`
  - Additive with a dimmed directional sun light (ambient + sun + point lights)
  - Light buffer: 272 bytes (16 header + 8 × 32 per light)
- Light as a scene object:
  - "+ Light" button in scene panel (yellow-tinted)
  - Spawns as a small sphere (0.2 scale) at y=2 with warm default settings
  - Selectable, movable with gizmo, deletable — same as any scene node
  - Inspector shows: Light Color picker, Intensity slider (0.5–10), Range slider (1–30)
- Full pipeline integration:
  - `WriteLightData()` collects all visible light nodes each frame
  - Light buffer created and bound alongside view-projection uniform
  - Bind group expanded to 2 bindings
- Serialization: `LightNodeData` in scene JSON (color, intensity, range)
- Duplication copies light properties
- Default scene includes a warm point light at (0, 3, 0)

**Files changed:**
- `src/WgpuSharp/Scene/SceneNode.cs` (PointLightData class, Light property, IsLight)
- `src/WgpuSharp/Scene/Scene.cs` (GetLightNodes)
- `src/WgpuSharp/Scene/SceneRenderer.cs` (light buffer, WriteLightData, new shader with multi-light)
- `src/WgpuSharp/Scene/SceneSerializer.cs` (LightNodeData, serialize/deserialize lights)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (AddLight, light inspector UI, default light, duplicate light)

## Loop 10 — Rotate & Scale Gizmo Modes (W/E/R)

**Feature:** The gizmo now has three modes — Translate (W), Rotate (E), Scale (R) — matching the Unity/Blender convention. Same axis arrows, different drag behavior per mode. Completes the spatial manipulation toolkit.

**What was built:**
- `GizmoMode` enum: Translate, Rotate, Scale
- `TranslateGizmo` extended with mode-aware drag:
  - `UpdateTranslateDrag(ray)` — existing position-along-axis behavior
  - `UpdateRotateDrag(ray)` — projects mouse drag onto axis, converts linear delta to angle (radians), applies as `CreateFromAxisAngle` rotation composed with start rotation
  - `UpdateScaleDrag(ray)` — projects mouse drag onto axis, converts to scale factor (1 + delta), clamps to ≥0.05 to prevent zero/negative, applies per-axis
  - `BeginDrag` overload accepts rotation + scale start state
- Tool mode switching:
  - **W** key = Translate, **E** key = Rotate, **R** key = Scale
  - Purple-highlighted **W/E/R** buttons in toolbar (click or keyboard)
  - Mode persists across selections
- Undo integration:
  - `ProcessGizmo` captures full transform state (pos + rot + scale) at drag start
  - On drag end, pushes a single `TransformAction` recording the complete before/after
  - Works correctly for all three modes
- W/E/R added to JS preventDefault list

**Files changed:**
- `src/WgpuSharp/Scene/TranslateGizmo.cs` (GizmoMode enum, mode-aware drag methods, rotation/scale state)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (mode switching, ProcessGizmo multi-mode, toolbar UI)
- `src/WgpuSharp.Demo/wwwroot/css/app.css` (tool button styles)
- `src/WgpuSharp/wwwroot/WgpuSharp.js` (W/E/R preventDefault)

## Loop 11 — Lit Ground Plane with Checkerboard

**Feature:** A large ground surface at Y=0 with a procedural checkerboard pattern that receives point light illumination. Objects sit ON something now. Play mode feels like walking in a real space.

**What was built:**
- `GroundPlane.cs` — self-contained renderer:
  - 100×100 unit quad at Y=0 (two triangles, 6 vertices)
  - Its own shader pipeline sharing both VP uniform + light buffer
  - Procedural checkerboard: `floor(worldPos.x) + floor(worldPos.z)` alternating dark/light grey
  - Full point light support: same lighting model as scene objects (sun + N point lights with attenuation)
  - Alpha edge fade: `smoothstep(30, 50, distFromCenter)` so it blends into the background
  - Alpha blending enabled on the pipeline
  - Depth writing enabled (objects properly occlude behind ground)
- Integrated into SceneRenderer:
  - Renders after grid, before scene objects (correct depth ordering)
  - Visible in both editor AND play mode (you walk on it!)
  - `Ground` property on SceneRenderer
- **Floor** toggle button in toolbar (blue when active, reuses snap-on style)
- `ToggleGround()` / `GroundEnabled()` helpers in editor

**Visual impact:**
- Point lights now cast visible pools of color on the floor
- Objects appear grounded (not floating in void)
- Play mode: you walk on a real surface with checkerboard reference pattern
- Edge fade makes it blend naturally into the dark background

**Files changed:**
- `src/WgpuSharp/Scene/GroundPlane.cs` (new — lit checker quad with edge fade)
- `src/WgpuSharp/Scene/SceneRenderer.cs` (Ground property, init, render, dispose)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (Floor toggle, helpers)

## Loop 12 — Multi-Select with Group Operations

**Feature:** Select multiple objects and operate on them as a group. The biggest workflow multiplier for building actual game levels.

**What was built:**
- `HashSet<int> _selectedIds` — set-based selection model alongside `Scene.SelectedNode` (primary for inspector)
- Selection helpers: `SelectSingle()`, `ToggleSelect()`, `SelectAll()`, `ClearSelection()`, `GetSelectedNodes()`
- Input integration:
  - **Click** = select single (clears others) — in both hierarchy and viewport
  - **Shift+click** = add/remove from selection — in both hierarchy and viewport
  - **Ctrl+A** = select all nodes
  - **Escape** = clear selection
- Group operations:
  - **Delete** (key or button) deletes all selected, each as an undo-able action
  - **Ctrl+D** / Duplicate duplicates all selected, new copies become the new selection
  - **Arrow nudge** moves all selected nodes by the same delta
  - **Gizmo translate** moves all selected by same offset (captures start pos per-node, applies uniform delta)
  - **Undo** records individual TransformActions per-node for correct multi-undo
- Rendering: `SceneRenderer.Render()` now accepts `HashSet<int>` — all selected nodes get orange highlight
- Hierarchy: all selected nodes highlighted with blue border
- Inspector: shows "N objects selected" with group Delete/Duplicate buttons when multi-selected; shows single-node inspector when one selected
- Gizmo rotation/scale still operates on primary selection (translate is the key multi-select operation)

**Files changed:**
- `src/WgpuSharp/Scene/SceneRenderer.cs` (HashSet<int> selection API, multi-highlight)
- `src/WgpuSharp/wwwroot/WgpuSharp.js` (Ctrl+A preventDefault)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (selection model, group ops, shift+click, Ctrl+A, inspector multi-view)

## Loop 13 — 3D Model Import (OBJ/GLB/STL)

**Feature:** Import any 3D model file into the scene. The editor goes from 4 primitive shapes to any model you can find on the internet or export from Blender.

**What was built:**
- **Import button** (blue "Import" in scene panel) — opens file picker for .obj, .glb, .stl
- Import pipeline:
  1. JS `pickAndReadFileBase64()` reads file as binary base64 with filename
  2. C# decodes base64 → `byte[]`
  3. `MeshLoader.Load(bytes, fileName)` dispatches to existing OBJ/GLB/STL parsers
  4. Auto-computes flat normals if mesh has none
  5. `CreateBuffersAsync` uploads to GPU
  6. Creates SceneNode with `MeshType = "Imported"`
- Persistence: raw file bytes stored on `SceneNode.ImportedMeshData` + `ImportedMeshFileName`
  - Serialized as base64 string in scene JSON
  - On scene load: bytes decoded → re-parsed → GPU buffers recreated
  - Fully self-contained — scene file has everything needed to reconstruct imported meshes
- `SceneSerializer.Rebuild` updated: mesh resolver now receives `(meshType, importedData, fileName)` to handle both primitives and imports
- Duplicate copies imported mesh data (same GPU buffers shared, bytes stored for serialization)
- `pickAndReadFileBase64()` JS helper: reads any file type as `filename|base64` string

**Files changed:**
- `src/WgpuSharp/Scene/SceneNode.cs` (ImportedMeshData, ImportedMeshFileName properties)
- `src/WgpuSharp/Scene/SceneSerializer.cs` (serialize/deserialize imported data as base64, updated resolver signature)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (ImportMesh, ResolveMeshFull, duplicate copies import data)
- `src/WgpuSharp.Demo/wwwroot/index.html` (pickAndReadFileBase64 JS helper)

## Loop 14 — Player Collision & Gravity

**Feature:** Play mode now has real physics. Gravity pulls you down, you stand on objects, bump into walls, and jump. This is what makes it a game engine, not a viewer.

**What was built:**
- Complete FpsCamera physics rewrite:
  - **Gravity**: 15 m/s² downward when not grounded
  - **Ground plane collision**: always-present floor at Y=0
  - **AABB collision vs scene objects**: computes world AABB from each node's transform (8 unit-cube corners transformed → axis-aligned bounds)
  - **Minimum penetration resolution**: finds shallowest overlap axis (X/Y/Z), pushes player out along it
  - **Wall sliding**: horizontal velocity zeroed on collision axis, movement continues along the other axis
  - **Landing on objects**: player stands on top of any AABB whose top face they collide with
  - **Ceiling bonk**: upward velocity zeroed when hitting an object from below
  - **Ground detection**: checks for solid surface below feet (ground plane or object tops)
  - **Jump**: Space key applies 6 m/s upward velocity when grounded
  - **Horizontal walking**: WASD uses horizontal forward (no pitch component) so you walk level
- `ComputeWorldAABB(Matrix4x4)` — transforms unit cube corners to world space, computes axis-aligned bounds
- `AABBOverlap()` — fast AABB intersection test
- Light nodes excluded from collision (no bumping into invisible light spheres)
- Player capsule approximated as box: 0.6m wide × 1.8m tall × 0.6m deep
- Default scene now includes a raised platform (3×0.5×3 green slab) to demonstrate jumping onto objects
- Play mode hints updated: "Space=jump"

**Files changed:**
- `src/WgpuSharp/Scene/FpsCamera.cs` (full rewrite — velocity physics, collision, gravity, jump)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (pass scene to ProcessInput, platform in default scene, updated hints)

## Loop 15 — Procedural Sky

**Feature:** A gradient sky rendered behind everything — blue zenith, warm horizon, sun disc, atmospheric haze. Transforms both editor and play mode from "black void" to "outdoor world."

**What was built:**
- `Sky.cs` — fullscreen sky renderer:
  - **No vertex buffer**: generates a fullscreen triangle from vertex IDs (0,1,2)
  - **Inverse VP reconstruction**: passes `invViewProj` uniform to fragment shader, unprojects clip-space coords to world-space view direction
  - **Gradient**: `dir.y` drives color blend — deep blue zenith → warm horizon → dark ground below
  - **Sun disc**: sharp core at `dot > 0.997`, soft glow ring at `dot > 0.99`, positioned at (0.4, 0.3, -0.5)
  - **Atmospheric haze**: exponential falloff near horizon for depth
  - **Depth**: renders at z=1.0 (far plane) with `DepthCompare.LessEqual` + no depth write — always behind everything
- Integrated into SceneRenderer:
  - Renders FIRST in the pass (before grid, ground, scene objects)
  - `Sky` property, created in InitAsync, disposed in DisposeAsync
  - Stores `_currentVP` matrix for inverse-projection
  - Both orbit camera and FPS camera compute VP for sky
  - Visible in editor AND play mode
- Clear color updated to match sky ground color for seamless fallback

**Files changed:**
- `src/WgpuSharp/Scene/Sky.cs` (new — fullscreen triangle, gradient shader, sun disc)
- `src/WgpuSharp/Scene/SceneRenderer.cs` (Sky property, VP storage, sky rendering in pass, clear color)

## Loop 16 — Collectible Tag & Gameplay

**Feature:** The first actual game mechanic. Tag objects as "Collectible" in the inspector, hit Play, and collect them by walking into them. Score HUD, spinning animation, win state. This is what makes it a game engine.

**What was built:**
- `NodeTag` enum: `Static` (default), `Collectible`
- Tag dropdown in inspector for every scene node
- Play-mode collectible behavior:
  - **Auto-rotate**: collectibles spin around Y axis (2 rad/s)
  - **Bob animation**: gentle sine-wave vertical motion (±0.15 units)
  - **Proximity collection**: when player is within ~1 unit, object disappears and score increments
  - **Score HUD**: golden "N / Total" counter centered at top of viewport
  - **Win state**: flashing green "ALL COLLECTED!" when score equals total
- Play-mode state management:
  - `_score`, `_totalCollectibles`, `_playTime`, `_collectedIds`
  - **Transform snapshot** on Play: saves every node's position, rotation, and visibility
  - **Full restore on Stop**: all transforms and visibility reset to pre-play state
  - Collectibles reappear and stop spinning when you exit play mode
- Tag serialized/deserialized in scene JSON (omitted when Static)
- Duplicate copies tag
- Default scene includes **5 colored gem spheres** arranged in a circle at radius 5, tagged Collectible

**Files changed:**
- `src/WgpuSharp/Scene/SceneNode.cs` (NodeTag enum, Tag property)
- `src/WgpuSharp/Scene/SceneSerializer.cs` (Tag serialization/deserialization, NodeData.Tag)
- `src/WgpuSharp.Demo/Pages/SceneEditor.razor` (Tag inspector, EnterPlayMode snapshot, ExitPlayMode restore, UpdateCollectibles, HUD, default gems)
- `src/WgpuSharp.Demo/wwwroot/css/app.css` (HUD styles — score, win flash)
