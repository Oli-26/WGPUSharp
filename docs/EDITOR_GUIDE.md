# WgpuSharp Scene Editor Guide

A lightweight 3D game engine and scene editor built with WebGPU and Blazor WASM. Build levels, add gameplay logic, and playtest — all in the browser.

Open the editor at `/editor` after starting the demo app with `dotnet run`.

---

## Table of Contents

1. [Editor Layout](#editor-layout)
2. [Camera Controls](#camera-controls)
3. [Keyboard Shortcuts](#keyboard-shortcuts)
4. [Selection](#selection)
5. [Gizmo Tools](#gizmo-tools)
6. [Adding Objects](#adding-objects)
7. [Inspector Properties](#inspector-properties)
8. [Object Tags](#object-tags)
9. [Scripting](#scripting)
10. [Scene Settings](#scene-settings)
11. [Play Mode](#play-mode)
12. [Scene Templates](#scene-templates)
13. [Saving, Loading, and Exporting](#saving-loading-and-exporting)
14. [Tips and Workflow](#tips-and-workflow)

---

## Editor Layout

```
+------------------+----------------------------+------------------+
|  Scene Hierarchy |       3D Viewport          |    Inspector     |
|                  |                            |                  |
|  Lists all nodes |  Click to select objects   |  Properties of   |
|  Drag to reorder |  Drag to orbit camera      |  selected node   |
|  + Add buttons   |  Toolbar at top            |  or Scene Settings|
|  Save/Load/New   |                            |  when nothing    |
|  Template picker  |                            |  selected        |
+------------------+----------------------------+------------------+
```

### Toolbar (top of viewport)

| Element | Description |
|---|---|
| **Undo / Redo** | Buttons + Ctrl+Z/Y. Hover to see history. |
| **Move / Rotate / Scale** | Active gizmo tool (purple = selected) |
| **Local / World** | Toggle gizmo space (L key) |
| **Snap** | Grid snap toggle + size dropdown. Indicator shown in viewport. |
| **Floor** | Toggle the ground plane |
| **Play** | Enter play mode |
| **?=help** | Click for full keyboard shortcut reference |

---

## Camera Controls

### Editor Mode (Orbit Camera)

| Action | Control |
|---|---|
| **Orbit** | Left-click drag on viewport |
| **Pan** | Shift + left-click drag, or middle-click drag |
| **Zoom** | Scroll wheel |
| **Focus on selection** | F key |
| **Camera bookmarks** | Ctrl+1 through Ctrl+4 to save, 1-4 to recall |

### Play Mode (FPS Camera)

| Action | Control |
|---|---|
| **Walk** | WASD |
| **Look** | Mouse (click canvas to capture) |
| **Jump** | Space |
| **Change speed** | Scroll wheel |
| **Exit play mode** | Escape |

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| **W** | Move tool (translate gizmo) |
| **E** | Rotate tool |
| **R** | Scale tool |
| **G** | Toggle grid snap |
| **L** | Toggle local/world gizmo space |
| **F** | Focus camera on selected object |
| **?** (Shift+/) | Show keyboard shortcut help dialog |
| **F3** | Toggle debug stats overlay (FPS, node count, position) |
| **Delete / Backspace** | Delete selected |
| **Ctrl+D** | Duplicate selected |
| **Ctrl+C** | Copy selected to clipboard |
| **Ctrl+V** | Paste from clipboard (offset by 1 unit) |
| **Ctrl+A** | Select all |
| **Ctrl+Z** | Undo |
| **Ctrl+Y / Ctrl+Shift+Z** | Redo |
| **Ctrl+1-4** | Save camera bookmark |
| **1-4** | Recall camera bookmark |
| **Escape** | Deselect all (editor) / Exit play mode |
| **Arrow keys** | Nudge selected by snap size (Shift = alternate axis) |

---

## Selection

- **Click** an object in the viewport or hierarchy to select it
- **Shift+click** to add/remove from multi-selection
- **Double-click** a node in the hierarchy to rename it inline
- **Right-click** a node in the hierarchy for a context menu (cut, copy, paste, duplicate, rename, delete)
- **Right-click drag** a rectangle in the viewport to box-select all enclosed objects
- **Ctrl+A** selects every object in the scene
- **Escape** clears the selection

When multiple objects are selected:
- The inspector shows **"N objects selected"** with group Delete/Duplicate buttons
- **Align** buttons (Left, Right, Top, Bottom, Front, Back) align objects along an axis
- **Distribute** buttons (X, Y, Z) evenly space objects along an axis
- The gizmo moves all selected objects by the same offset

---

## Gizmo Tools

The colored arrows at the selected object manipulate it visually. Switch tools with **W/E/R** keys or the toolbar buttons.

| Tool | Key | Drag behavior |
|---|---|---|
| **Move** | W | Translate along axis. Red=X, Green=Y, Blue=Z |
| **Rotate** | E | Rotate around axis |
| **Scale** | R | Scale along axis |

### Snapping

When **Snap** is enabled (blue button or G key):
- **Position** snaps to the grid increment (adjustable: 0.1, 0.25, 0.5, 1, 2)
- **Rotation** snaps to 15-degree increments
- Arrow key nudge uses the snap size as the step

---

## Adding Objects

### Primitives (Scene panel buttons)

| Button | Object |
|---|---|
| **+ Cube** | Unit cube |
| **+ Sphere** | UV sphere |
| **+ Plane** | Flat quad on XZ plane |
| **+ Cylinder** | Cylinder along Y axis |
| **+ Light** | Point light source (small sphere icon) |
| **Import** | Load a .OBJ / .GLB / .STL model from disk |

### Importing 3D Models

Click **Import**, pick a file. A progress bar shows the import stages (reading, parsing, computing normals, uploading to GPU). The file reading runs in a Web Worker so the UI stays responsive.

Imported models are embedded in the scene file when saved (as base64), so the `.json` is fully self-contained.

### Importing Rigged/Animated Models

GLB files with skeletal animation data (skins, joints, animations) are automatically detected. When a rigged GLB is imported:

- The skeleton hierarchy and inverse bind matrices are extracted
- Joint indices and weights are loaded per vertex
- All animation clips are parsed (translation, rotation, scale keyframes)
- A dedicated skinned rendering pipeline handles bone blending on the GPU
- The **Animation** section appears in the inspector with playback controls

Supported rigs: up to 128 joints, 4 influences per vertex. Standard glTF 2.0 skinning with LINEAR or STEP interpolation.

---

## Inspector Properties

The inspector is organized into collapsible sections (Properties, Transform, Material, Light, Animation, Script). Click a section header to expand or collapse it.

### Single Object Selected

| Property | Description |
|---|---|
| **Name** | Display name. Used for Door/Key/Teleporter matching. |
| **Visible** | Toggle rendering on/off |
| **Solid** | Whether the player collides with this in play mode |
| **Locked** | Prevent moving, deleting, or nudging this object |
| **Tag** | Game behavior tag (see below) |
| **Material** | Preset dropdown (Brick, Metal, Wood, Glass, etc.) or Custom |
| **Position** | World position (X, Y, Z) |
| **Rotation** | Euler angles in degrees |
| **Scale** | Size multiplier per axis |
| **Color** | Tint color picker |
| **Script** | Per-object script with template dropdown (see Scripting) |

### Conditional Properties

| When... | Extra fields shown |
|---|---|
| Node is a **Light** | Light Color, Intensity slider, Range slider |
| Node is a **rigged model** | Animation section: clip selector, play/pause/stop, speed, loop, time scrubber, joint count |
| Tag is **MovingPlatform** | Move Target (X, Y, Z) — the endpoint for oscillation |

### Animation Controls (rigged models only)

| Control | Description |
|---|---|
| **Clip** | Select which animation clip to play |
| **Play / Pause / Stop** | Playback controls. Stop resets to frame 0. |
| **Speed** | Playback speed multiplier (0x to 3x) |
| **Loop** | Whether the animation repeats |
| **Time** | Scrub to a specific time in the clip |
| **Joints** | Shows the number of bones in the skeleton |

Animations play in both editor and play mode. The animation state (clip, speed, loop) is saved with the scene.

---

## Object Tags

Tags define game behavior in play mode. Set them via the **Tag** dropdown in the inspector.

### Environment Tags

| Tag | Behavior in Play Mode |
|---|---|
| **Static** | Default. No special behavior. Solid by default. |
| **MovingPlatform** | Oscillates between its position and Move Target. Player can ride it. |
| **DamageZone** | Drains 5 HP every 0.3 seconds while the player is inside. Use for lava, spikes. |

### Collectible Tags

| Tag | Behavior in Play Mode |
|---|---|
| **Collectible** | Spins and bobs. Disappears when touched. Increments score counter. |
| **Key** | Disappears when touched. Unlocks matching Door. Match by name prefix: "Red Key" → "Red Door". |
| **HealthPickup** | Restores 25 HP on touch. Disappears after use. |

### Interactive Tags

| Tag | Behavior in Play Mode |
|---|---|
| **Trigger** | Invisible. Shows "Entered: [name]" message when player walks in. |
| **Checkpoint** | Invisible. Saves player respawn position on entry. Shows "Checkpoint: [name]". |
| **Teleporter** | Walk in to teleport to matching pad. Match by name prefix: "Blue Pad A" ↔ "Blue Pad B". |
| **Door** | Solid barrier. Becomes invisible (opens) when matching Key is collected. |
| **NPC** | Shows node name as dialog text when player is within 3 units. |

### Combat Tags

| Tag | Behavior in Play Mode |
|---|---|
| **Enemy** | Chases player within 12 units at 2.5 u/s. Deals 10 damage on contact (1s cooldown). |

### Audio Tags

| Tag | Behavior in Play Mode |
|---|---|
| **AudioSource** | Plays a positional tone with distance-based falloff tied to the player camera. |

---

## Scripting

Every object has a **Script** field in the inspector. Scripts are simple text — one command per line, executed every frame in play mode. Lines starting with `//` are comments.

### Quick Start

Select an object, pick a template from the dropdown above the script editor, or write commands manually.

### Available Commands

```
Rotate(x, y, z)             // Rotate degrees/sec per axis
Bob(speed, height)           // Oscillate up and down
FollowPlayer(speed, range)   // Move toward player within range
OnEnter("message")           // Show message when player enters volume
OnEnterToggle("NodeName")    // Toggle named object's visibility on enter
SetColor(r, g, b)            // Override color (0-1 range)
Scale(speed)                 // Pulse scale over time
Orbit(radius, speed)         // Circle around start position
LookAtPlayer()               // Face the player each frame
```

### Script Templates

The dropdown above the script editor provides 14 ready-made templates:

| Template | Commands | Use case |
|---|---|---|
| **Spinning Pickup** | Rotate + Bob | Coins, gems, power-ups |
| **Patrolling Enemy** | FollowPlayer + LookAtPlayer | Standard enemy |
| **Aggressive Enemy** | Fast FollowPlayer + LookAtPlayer | Boss-type enemy |
| **Floating Sentry** | Orbit + LookAtPlayer | Flying patrol |
| **Pulsing Light** | Scale + Bob | Glowing orbs |
| **Orbiting Light** | Orbit | Circling light source |
| **Swinging Obstacle** | Rotate (Z axis) | Pendulum trap |
| **Bounce Pad** | Scale + Bob | Jump pad visual |
| **Trigger Message** | OnEnter | Welcome signs, hints |
| **Trigger Door Open** | OnEnterToggle | Pressure plates |
| **NPC Idle** | Slow Rotate | Characters |
| **Decoration Spin** | Gentle Rotate | Display items |
| **Hazard Warning** | Scale + SetColor | Danger indicators |
| **Moving Wall** | Bob (slow, large) | Crushing walls |

Templates **append** to the existing script, so you can stack multiple behaviors.

### Example: Complete Puzzle

1. Create a cube, name it "Gate", set Tag to Static, make it Solid
2. Create a small sphere, name it "Button", give it the script: `OnEnterToggle("Gate")`
3. Hit Play — walk onto the button and the gate disappears

### Example: Orbiting Enemy with Dialog

```
Orbit(5, 1)
LookAtPlayer()
FollowPlayer(2, 8)
```

---

## Scene Settings

Click empty space (or press Escape) to deselect all objects. The inspector shows global scene settings:

### Sky

| Setting | Description |
|---|---|
| **Sky Zenith** | Color at the top of the sky (default: deep blue) |
| **Sky Horizon** | Color at the horizon (default: warm orange) |
| **Sky Ground** | Color below the horizon (default: dark) |

### Lighting

| Setting | Description |
|---|---|
| **Sun Color** | Directional light color |
| **Sun Intensity** | Directional light strength (0–2) |
| **Ambient Color** | Base lighting applied everywhere |

### Atmosphere

| Setting | Description |
|---|---|
| **Fog Color** | Color objects fade toward at distance |
| **Fog Start** | Distance where fog begins (5–100) |
| **Fog End** | Distance where fog is fully opaque (10–200) |

### Gameplay

| Setting | Description |
|---|---|
| **Level Timer** | Enable a countdown timer |
| **Time Limit** | Seconds before time runs out (10–300). Reaching 0 = game over. |

---

## Play Mode

Click the green **Play** button to enter play mode. The editor panels dim and the viewport becomes a first-person game. Use the **Pause** button to freeze gameplay while the camera still renders.

### HUD Elements

| Element | When shown |
|---|---|
| **Score** (gold, top center) | When collectibles exist in the scene |
| **Health bar** (bottom center) | When enemies exist |
| **Timer** (top right, monospace) | When level timer is enabled |
| **Minimap** (bottom right) | Always in play mode. Green=you, Red=enemies, Yellow=collectibles, Cyan=keys |
| **Trigger messages** (top center) | When entering triggers, checkpoints, collecting keys |
| **"GAME OVER"** (center) | When health reaches 0 |
| **"ALL COLLECTED!"** | When all collectibles are gathered |

### Physics

- **Gravity** pulls you down at 15 m/s²
- **Ground plane** at Y=0 always catches you
- **Solid objects** block movement (wall sliding)
- **Jump** launches at 6 m/s upward
- **Respawn** at last checkpoint (or origin) when falling below Y=-10 or after death

### State Reset

All play-mode changes are **fully reset when you stop**:
- Collected items reappear
- Moved enemies return to starting positions
- Opened doors re-close
- Health, score, keys, triggers all reset
- Scripts restart from scratch

---

## Scene Templates

At the bottom of the Scene panel, pick a template from the dropdown and click **New Scene**:

| Template | Contents |
|---|---|
| **Empty** | Just a light. Blank canvas for building from scratch. |
| **Sandbox** | Floor, 3 cubes, sphere. Freeform experimentation. |
| **Platformer** | Ascending platforms, narrow bridge, 5 gems, enemy guard, win zone, mood lights. |
| **Arena** | Walled arena with cover blocks, 3 enemies, health pickups, colored lighting. |

**Warning:** New Scene clears the current scene and undo history.

---

## Saving, Loading, and Exporting

| Button | Action |
|---|---|
| **Save** | Downloads scene as `scene.json` |
| **Load** | Opens file picker to load a saved `.json` scene |
| **Screenshot** | Captures the viewport as `screenshot.png` |

### Scene File Format

Scenes are stored as JSON. All data is self-contained:
- Node hierarchy, transforms, colors, tags, scripts
- Material presets, light properties, move targets
- Imported 3D model data (embedded as base64)
- Locked/solid/visible state

---

## Tips and Workflow

### Building Levels
- Enable **Snap** (G key) for precise placement — walls and floors align perfectly
- Use snap size **1.0** for room-scale building, **0.25** for fine detail
- **Lock** finished geometry to prevent accidental moves
- **Shift+click** to multi-select, then use **Align** buttons to line things up

### Organizing
- **Filter** nodes with the search box at the top of the hierarchy
- **Collapse/expand** parent nodes by clicking the arrow toggle
- **Drag nodes** in the hierarchy to reparent them (create groups)
- Drop on *"(drop here for root)"* to move back to top level
- Name objects descriptively — Door/Key/Teleporter matching depends on names
- **Node icons** show type at a glance: # = Cube, @ = Sphere, o = Light, etc.
- **Lock indicator** (amber L) appears on locked nodes in the hierarchy

### Testing
- **Play** frequently — the round-trip is instant, and all changes reset on Stop
- Use **F3** to see node counts, FPS, and player position
- Use **Ctrl+1-4** to bookmark camera angles for quick navigation

### Performance
- Point lights are limited to **8 per scene** (forward rendering)
- Large imported models may cause frame drops — keep triangle count reasonable
- The **Floor** button hides the ground plane if it's not needed

### Scripting Tips
- Scripts execute **every frame** — use `dt`-scaled values (Rotate degrees are per-second)
- **Stack templates** by selecting multiple from the dropdown
- Use `OnEnterToggle` for doors, bridges, platforms that appear/disappear
- Combine `FollowPlayer` + `LookAtPlayer` for enemies that feel alive
- `Orbit` + `Bob` creates interesting patrol patterns

### Making a Game
1. Start with the **Platformer** or **Arena** template
2. Modify the layout (move platforms, add walls)
3. Place **Collectibles** as objectives
4. Add **Enemies** with `Patrolling Enemy` scripts
5. Place **Checkpoints** before difficult sections
6. Add a **Trigger** at the end with `OnEnter("You Win!")`
7. Enable the **Level Timer** for challenge
8. Hit **Play** and test!
