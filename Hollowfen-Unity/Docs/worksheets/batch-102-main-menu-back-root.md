# Batch 102 — Main-menu Back root protection

**Date:** 2026-07-18 · **Status:** verified

## Goal

Prevent Circle/Escape on the main menu from popping the only UI screen and stranding the player on the scene's green background. Back at the application root should use the existing localized Quit confirmation, and direct generic Back calls must leave the root intact.

## Plan

- [x] Trace the Cancel action through `UIManager`, `UIScreen`, and `MainMenuScreen`.
- [x] Identify the empty-stack failure and protect application root screens.
- [x] Route the main menu's Back action through the existing Quit confirmation.
- [x] Compile and verify direct Back, Circle-equivalent Back, modal cancellation, and normal menu navigation in Unity Play Mode.
- [x] Run integrity/lint checks and finalize the UI documentation.

## Root cause

`MainMenuScreen` inherited `UIScreen.OnBack()`, which called `UIManager.Back()`. Because `main-menu` was the only stack entry, the generic pop deactivated it and left the stack empty. The underlying main-menu scene remained rendered, but there was no active screen or UI input map to recover from it.

## Implementation

- Added `UIScreen.IsRootScreen`, defaulting to false.
- Marked `MainMenuScreen` as the application root.
- Made `UIManager.Back()` refuse direct root-screen pops while preserving explicit `CloseAll` and scene-replacement flows.
- Overrode main-menu Back to open the same localized Quit confirmation as the authored Quit button.

## Verification evidence

- Unity refreshed and compiled with zero Console errors or warnings.
- Main-menu Play Mode started with `top=main-menu`, `open=True`, and `root=True`.
- A direct `UIManager.Back()` left `top=main-menu`, `open=True`, and `transitioning=False`.
- The exact Circle/Escape route (`MainMenuScreen.OnBack`) opened `top=confirm-modal` while leaving the underlying main menu active.
- Canceling the modal restored `top=main-menu`, `open=True`, and focus to `Btn_NewGame` (Forage).
- Normal `main-menu -> settings -> Back` navigation returned to `main-menu` with the stack still open.
- After the menu entrance animation settled, `ProductionUIVerifier` reported `PASS · 0 critical · 0 advisory`.
- `lint_hollowfen.py`: `ERRORS=0 WARNINGS=0` (one existing documented waiver).
- `run_integrity.py`: `ERRORS=0 WARNINGS=0` across 26 quests, 75 dialogues, 11 NPCs, 14 locations, 21 mushrooms, 30 story cards, two story moments, 31 character profiles, four endings, and ten village requests.
- `git diff --check`: pass for the batch files.
- No player build was created or run.

## Files changed

- `Assets/_Hollowfen/Scripts/UI/UIScreen.cs`
- `Assets/_Hollowfen/Scripts/UI/UIManager.cs`
- `Assets/_Hollowfen/Scripts/UI/MainMenuScreen.cs`
- `Docs/systems/ui-framework.md`

## Unfinished / handoff

No implementation work remains. Do not create or run a player build until Trevor explicitly requests one.

## Feedback to Trevor

The green page was not a separate screen: it was the scene showing through after the UI stack became empty. Treating application roots as an explicit screen property makes that invalid state impossible through generic Back handling, while the main menu can still choose a useful root-specific action—the existing Quit confirmation.
