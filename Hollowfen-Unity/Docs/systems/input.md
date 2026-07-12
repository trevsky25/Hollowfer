# Input System
New Input System only (never legacy Input Manager). Project-owned `InputActions` asset with UI / Player / Dialogue maps; Wren's controller separately uses the StarterAssets asset — the two coexist intentionally. Controller-first is a Steam Deck cert requirement: every interactive element must be gamepad-reachable.
Key assets: `Assets/_Hollowfen/Input/InputActions.inputactions` (C# wrapper auto-generated as `Hollowfen.Input.InputActions`); `Assets/Starter Assets/.../StarterAssets.inputactions` (Wren's PlayerInput).
Action maps: UI (Navigate, Submit, Cancel, TabLeft, TabRight, Delete), Player (Move, Look, Interact, Jump, OpenInventory, Pause, OpenMap), Dialogue (Advance, Skip, Choice1-4).
Conventions: Submit=South(Cross/A), Cancel=East(Circle/B), Steam Deck bindings mirror gamepad.
Biggest gotchas: disable Wren's PlayerInput while modal screens are open (buffered-Jump bug); glyph auto-detection assumes DualSense as primary dev pad.
Status: shipped + verified. Consolidating the two input assets is deferred until gameplay needs to trigger menu actions.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Gamepad mapping (post-rebind)

| Action | Keyboard | Gamepad |
|---|---|---|
| `Player/Interact` (open Inspect / Forage shortcut / talk to NPC) | `E` | **Triangle / Y** (`buttonNorth`) |
| `Player/OpenInventory` (provisions journal) | `I` | **Square / X** (`buttonWest`) |
| `Player/OpenMap` | `M` | **Select / View / DualSense Touchpad** |
| `Player/Pause` | `Esc` | **Start / Options** |
| `UI/Submit` (activate focused) | `Enter` / `Space` | **Cross / South** |
| `UI/Cancel` (back / leave) | `Esc` | **Circle / East** |
| `UI/TabLeft` / `UI/TabRight` (settings tabs, inventory Inspect Mode) | `Q`/`Tab` | **LB / RB** |
| `UI/Delete` (save slot delete) | `Delete` | **Square / West** |
| Inspect rotate / zoom | mouse drag / scroll | right stick / RT+LT |

## Notes

- **`Player/OpenJournal` was renamed `Player/OpenInventory`** — the inventory IS the journal today. Old references are gone. A future real-journal feature takes a different action slot.
- **UIManager's pause input fires from the project asset** regardless of scene, since UIManager is DDOL.
- **Glyph detection** (`InspectScreen`, `InventoryScreen`): `Gamepad.current.GetType().Name` + `description.product` matched against PS / Xbox / Switch; PS-style fallback (`△`/`○`). Refreshed every frame so pad swaps update live.
- **The Dialogue action map is currently UNUSED** — `DialogueScreen` polls devices directly (Space/Enter/E, gamepad South/North, mouse left), so rebinding doesn't affect dialogue. Same pattern in `MapScreen` and `InspectScreen` for their in-screen controls (only open/close go through actions). Consolidating onto action maps is a deferred hardening item (TODOS.md).
- Every UI screen needs a default selected element on open + visible focus highlight readable on a Steam Deck screen (see steam-constraints.md).
