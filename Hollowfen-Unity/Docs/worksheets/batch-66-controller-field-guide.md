# Batch 66 — Controller Field Guide shortcut

**Date:** 2026-07-14 · **Status:** DONE (play-verified)
**Directive:** Give PS5 players a dedicated, discoverable way to open Wren's mushroom journal during gameplay without stealing an existing controller action.

## Plan

- [x] Audit the project action map and gameplay/menu conflicts
- [x] Add a dedicated Field Guide action and safe gameplay open flow
- [x] Update onboarding, Settings, and input documentation
- [x] Play-mode verification and final project gates

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| PS5 shortcut | D-Pad Up | Triangle is Interact, touchpad is Map, Options is Pause, and Square remains the satchel. D-Pad Up is conflict-free during gameplay and conventional for journals/quest logs. |
| Keyboard split | `J` = Mushroom Field Guide; `I` = Satchel | Gives the refined journal a true direct shortcut while bringing the provisions key back in line with the existing input documentation. |
| Menu collision prevention | Only open from unobstructed gameplay | D-Pad Up is also UI navigation. The bridge rejects the shortcut whenever a UIManager screen, paused modal, inspect screen, or satchel already owns input. |
| Direct-open behavior | Field Guide owns pause/cursor state only when opened from live gameplay | Main-menu and Pause-stack uses keep their existing lifecycle; gameplay gets a safe one-button journal with clean state restoration. |

## Verification evidence

- Unity regenerated `InputActions.cs` from the project asset; the typed wrapper exposes `OpenFieldGuide` with `<Keyboard>/j` and `<Gamepad>/dpad/up`, while `OpenInventory` now exposes `<Keyboard>/i` and Square.
- Real main-menu boot followed by a gameplay scene load preserved the DDOL `UIManager`; a virtual gamepad D-Pad Up press produced exactly one `OpenFieldGuide.performed` callback.
- Open state: top/only screen `field-guide`, `Time.timeScale=0`, `PlayerInteractor.Suspended=true`, Wren's `PlayerInput.enabled=false`.
- D-Pad Up while the guide was open left the stack at one screen and did not start another transition, confirming the UI-navigation collision guard.
- Close state: no screen, `Time.timeScale=1`, `PlayerInteractor.Suspended=false`, Wren's `PlayerInput.enabled=true`.
- Unity console: 0 errors (50 pre-existing world/asset warnings in the loaded gameplay scene).
- `lint_hollowfen.py`: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- `run_integrity.py`: `ERRORS=0 WARNINGS=0`; 21 mushrooms, 30 story cards, and 3 dedicated journal models covered.

## Docs updated

- `Docs/systems/input.md` — action map, controller/keyboard table, context gating, and gameplay pause lifecycle.

## Unfinished / handoff

Nothing unfinished. No save data or scene state was changed by verification; the virtual test gamepad was removed before leaving Play Mode.

## Feedback to Trevor

The previously displayed controls called the satchel a journal and documented `I` while the asset actually used `J`; separating the two actions removes that ambiguity and makes the mushroom guide a first-class gameplay surface.
