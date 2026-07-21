# Input + gameplay camera
New Input System only (never legacy Input Manager). Project-owned `InputActions` asset with UI / Player / Dialogue maps; Wren's controller and follow camera separately use Starter Assets — the two coexist intentionally. Controller-first is a Steam Deck cert requirement: every interactive element must be gamepad-reachable.
Key assets: `Assets/_Hollowfen/Input/InputActions.inputactions` (C# wrapper auto-generated as `Hollowfen.Input.InputActions`); `Assets/Starter Assets/.../StarterAssets.inputactions` (Wren's PlayerInput).
Action maps: UI (Navigate, Submit, Cancel, TabLeft, TabRight, Delete), Player (Move, Look, Interact, Jump, OpenInventory, OpenFieldGuide, Pause, OpenMap), Dialogue (Advance, Skip, Choice1-4). The forage cutting micro-challenge polls live devices directly for analog precision and haptics.
Conventions: Submit=South(Cross/A), Cancel=East(Circle/B), Steam Deck bindings mirror gamepad.
Biggest gotchas: modal ownership must flow through `NarrativePresentationSession` so nested screens cannot re-enable Wren or global shortcuts early; brand glyphs are physical-face hints, not Steam Input binding-origin glyphs. Gameplay camera noise runs after collision avoidance, so its positional amplitude must remain smaller than the camera collision buffer.
Status: centralized presentation ownership, connection-first brand-aware prompts, transition-safe PlayStation narration hints, dialogue/narration action-map controls, and an interior-safe follow camera are Play Mode verified. Batch 123 retired the last project legacy-input waiver by removing the inactive location debug overlay; lint is now clean with zero waivers.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Gamepad mapping (post-rebind)

| Action | Keyboard | Gamepad |
|---|---|---|
| `Player/Interact` (open Inspect / Forage shortcut / talk to NPC) | `E` | **Triangle / Y** (`buttonNorth`) |
| `Player/OpenInventory` (satchel / provisions) | `I` | **Square / X** (`buttonWest`) |
| `Player/OpenFieldGuide` (mushroom journal) | `J` | **D-Pad Up** |
| `Player/OpenMap` | `M` | **Select / View / DualSense Touchpad** |
| `Player/Pause` | `Esc` | **Start / Options** |
| `UI/Submit` (activate focused) | `Enter` / `Space` | **Cross / South** |
| `UI/Cancel` (back / leave) | `Esc` | **Circle / East** |
| `UI/TabLeft` / `UI/TabRight` (settings tabs, inventory Inspect Mode) | `Q`/`Tab` | **LB / RB** |
| `UI/Delete` (save slot delete) | `Delete` | **Square / West** |
| Inspect / Field Guide specimen / Wren study / identification lens rotate + zoom | mouse drag / scroll (`R` resets 3D view; lens also supports middle-drag pan) | right stick / RT+LT (stick click resets 3D view) |
| Forage cutting challenge | hold `S`, alternate `A` / `D`; `Esc` cancels | hold left stick down to brace, alternate right stick left/right; Circle/B cancels |
| Dialogue advance / skip | `Space`/`Enter` (`E` alias) / `Esc` | South (North alias) / East |
| Dialogue choices 1–4 | `1`–`4`, or W/S + confirm | D-Pad up/right/down/left, or left stick + South |
| Narration continue / skip | `Space`/`Enter` (`E` alias) / `Esc` | South / East |

## Notes

- **No legacy-input debug exception remains.** Batch 123 removed the inactive `LocationDebugHUD`
  scene root and script rather than migrate its discovery-mutating `F` shortcut. Location state can
  be inspected through the native Pipeline/Coplay tooling without compiling a debug input path into
  Player assemblies.
- **The gameplay follow camera is interior-safe.** `PlayerFollowCamera.prefab` retains Cinemachine obstacle avoidance on Default-layer architecture, but its radius is 0.35 m and continuous Perlin amplitude is only 0.08. The former 0.50 amplitude ran after collision solving and could push the final camera pose through thin walls; the 0.10 m near clip adds clearance at tight corners.
- **Journal and satchel are separate actions.** `OpenFieldGuide` opens the mushroom journal directly (`J` / D-Pad Up); `OpenInventory` keeps provisions on (`I` / Square). Triangle remains Interact and the DualSense touchpad remains Map.
- **D-Pad Up is gameplay-contextual.** `InventoryInputBridge` ignores the shortcut while a UIManager screen or modal gameplay surface owns input, so D-Pad navigation cannot unexpectedly open the guide inside menus.
- **Direct gameplay opens own presentation leases.** Field Guide, Inventory, Map, Purse, Inspect, Cultivation, Requests, Dialogue, and Pause acquire centralized policies. Main-menu-only instances use `AcquireIfGameplay`, so menu presentation does not fabricate gameplay state.
- **Global shortcuts are one-at-a-time.** Bridges and `UIManager` consult `NarrativePresentationSession.BlocksGameplayShortcuts`; a close key remains blocked through its release frame so Escape cannot close one surface and open Pause behind it.
- **3D journal interaction is surface-scoped.** Pointer drag and wheel input are accepted only by the large model `RawImage`; index cards stay non-interactive. Right-stick orbit and trigger zoom use unscaled time on both the Field Guide detail and Wren living-study page, so they work in Main Menu, Pause, and direct gameplay-open contexts without stealing left-stick journal navigation.
- **The first-identification observation lens is optional and modal.** Selecting the live silhouette moves focus to one controller-safe Return button; right stick/trigger input continues to drive the unnamed preview, Circle/Esc returns to the candidate book, and all four navigation directions remain trapped inside the lens. Page-turn and final-verification haptics are short standard-motor pulses and are explicitly stopped on close, focus loss, destroy, or reveal completion.
- **Forage cutting is deliberately direct-polled.** `ForageCuttingChallenge` reads `Gamepad.current` each unscaled frame so stick angle, brace pressure, hot-swapped device hints, and motor speeds stay synchronized. A stroke requires left-stick brace ≥ 0.72, right-stick horizontal magnitude ≥ 0.64, vertical error ≤ 0.52, an alternating direction, and at least 0.13s since the previous accepted stroke.
- **Haptics use standard gamepad motors.** A low motor carries brace tension, the high motor carries blade resistance/angle chatter, accepted strokes pulse progressively harder, and the release uses the strongest pulse. Rumble is stopped on focus loss, cancel, destroy, and normal completion.
- **UIManager's pause input fires from the project asset** regardless of scene, since UIManager is DDOL.
- **Glyph detection is connection-first.** If an enabled gamepad is connected, Intro Guide, Inspect, Inventory, Interaction Prompt, Coin HUD, forage cutting, dialogue, and narration show controller prompts; keyboard prompts appear only when no gamepad is available. The current/semantically submitted pad determines PlayStation, Xbox, or Switch branding, with any remaining connected pad as a hot-plug fallback. A cinematic scene transition pins the selected gamepad until narration is visible, preventing connection/current-device churn during loading.
- **The Dialogue action map is live for dialogue and narration.** Dialogue also preserves E/North/mouse aliases that are intentionally outside the asset. Map, Inspect model controls, Wren study, Mushroom detail, and forage analog sampling still poll devices directly, so a future rebinding system must centralize those paths before claiming full remap support.
- Every UI screen needs a default selected element on open + visible focus highlight readable on a Steam Deck screen (see steam-constraints.md).
