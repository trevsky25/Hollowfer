# Input System
New Input System only (never legacy Input Manager). Project-owned `InputActions` asset with UI / Player / Dialogue maps; Wren's controller separately uses the StarterAssets asset — the two coexist intentionally. Controller-first is a Steam Deck cert requirement: every interactive element must be gamepad-reachable.
Key assets: `Assets/_Hollowfen/Input/InputActions.inputactions` (C# wrapper auto-generated as `Hollowfen.Input.InputActions`); `Assets/Starter Assets/.../StarterAssets.inputactions` (Wren's PlayerInput).
Action maps: UI (Navigate, Submit, Cancel, TabLeft, TabRight, Delete), Player (Move, Look, Interact, Jump, OpenInventory, OpenFieldGuide, Pause, OpenMap), Dialogue (Advance, Skip, Choice1-4). The forage cutting micro-challenge polls live devices directly for analog precision and haptics.
Conventions: Submit=South(Cross/A), Cancel=East(Circle/B), Steam Deck bindings mirror gamepad.
Biggest gotchas: disable Wren's PlayerInput while modal screens are open (buffered-Jump bug); glyph auto-detection assumes DualSense as primary dev pad.
Status: shipped + verified. Batch-71 extends the existing journal inspection grammar to Wren's animated 3D study: drag/right stick orbit, wheel/triggers zoom, and `R`/right-stick click reset without adding a competing action map.

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
| Inspect / Field Guide specimen / Wren study rotate + zoom | mouse drag / scroll (`R` resets 3D view) | right stick / RT+LT (stick click resets 3D view) |
| Forage cutting challenge | hold `S`, alternate `A` / `D`; `Esc` cancels | hold left stick down to brace, alternate right stick left/right; Circle/B cancels |

## Notes

- **Journal and satchel are separate actions.** `OpenFieldGuide` opens the mushroom journal directly (`J` / D-Pad Up); `OpenInventory` keeps provisions on (`I` / Square). Triangle remains Interact and the DualSense touchpad remains Map.
- **D-Pad Up is gameplay-contextual.** `InventoryInputBridge` ignores the shortcut while a UIManager screen or modal gameplay surface owns input, so D-Pad navigation cannot unexpectedly open the guide inside menus.
- **Direct gameplay opens own their pause state.** `FieldGuideScreen` pauses Wren, unlocks the cursor, and restores the exact prior state on close; main-menu and pause-stack opens retain their existing context.
- **3D journal interaction is surface-scoped.** Pointer drag and wheel input are accepted only by the large model `RawImage`; index cards stay non-interactive. Right-stick orbit and trigger zoom use unscaled time on both the Field Guide detail and Wren living-study page, so they work in Main Menu, Pause, and direct gameplay-open contexts without stealing left-stick journal navigation.
- **Forage cutting is deliberately direct-polled.** `ForageCuttingChallenge` reads `Gamepad.current` each unscaled frame so stick angle, brace pressure, hot-swapped device hints, and motor speeds stay synchronized. A stroke requires left-stick brace ≥ 0.72, right-stick horizontal magnitude ≥ 0.64, vertical error ≤ 0.52, an alternating direction, and at least 0.13s since the previous accepted stroke.
- **Haptics use standard gamepad motors.** A low motor carries brace tension, the high motor carries blade resistance/angle chatter, accepted strokes pulse progressively harder, and the release uses the strongest pulse. Rumble is stopped on focus loss, cancel, destroy, and normal completion.
- **UIManager's pause input fires from the project asset** regardless of scene, since UIManager is DDOL.
- **Glyph detection** (`InspectScreen`, `InventoryScreen`): `Gamepad.current.GetType().Name` + `description.product` matched against PS / Xbox / Switch; PS-style fallback (`△`/`○`). Refreshed every frame so pad swaps update live.
- **The Dialogue action map is currently UNUSED** — `DialogueScreen` polls devices directly (Space/Enter/E, gamepad South/North, mouse left), so rebinding doesn't affect dialogue. Same pattern in `MapScreen` and `InspectScreen` for their in-screen controls (only open/close go through actions). Consolidating onto action maps is a deferred hardening item (TODOS.md).
- Every UI screen needs a default selected element on open + visible focus highlight readable on a Steam Deck screen (see steam-constraints.md).
