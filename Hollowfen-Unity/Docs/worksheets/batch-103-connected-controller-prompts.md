# Batch 103 — Connected-controller prompt alignment

**Date:** 2026-07-18 · **Status:** implemented and verified

## Goal

Ensure bottom Continue/Skip help and every other shared glyph-aware prompt show the correct controller brand whenever a controller is connected, with keyboard prompts used only when no controller is available.

## Plan

- [x] Audit Intro Guide, dialogue, narration, inspect, inventory, interaction, purse, and cutting prompts.
- [x] Reproduce the connected-DualSense/keyboard-prompt mismatch in the live editor.
- [x] Replace last-input mode selection with connection-first, brand-aware controller selection.
- [x] Verify PlayStation Continue/Skip sprites, keyboard fallback, hot-plug fallback, and the brand-refresh path.
- [x] Run UI, lint, and data-integrity checks; update input documentation.

## Root cause

The shared resolver required a gamepad to provide the most recent meaningful input before `IsGamepadActive` became true. Unity could therefore report two connected DualSense controllers and correctly resolve the Circle sprite while dialogue/narration still chose their keyboard localization branch. Cursor changes and keyboard/mouse activity could also switch a visible help line away from the connected controller.

## Implementation

- `ControllerGlyphs.IsGamepadActive` now means an enabled connected gamepad exists.
- `ControllerGlyphs.For` resolves the held transition pad, Input System's current gamepad, the semantically submitted gamepad, then any enabled connected gamepad.
- Keyboard/mouse activity no longer suppresses a connected controller's prompts.
- Device disconnects fall through to another enabled controller or keyboard mode when none remain.
- The interaction prompt now compares the resolved glyph itself, so changing between connected
  controller brands refreshes the visible icon even though both devices are still gamepads.
- The first objective card no longer mixes keyboard and generic Xbox labels. Move, Interact, Set Out,
  and Field Guide help update live; a DualSense gets Left Stick, Triangle, Cross, and D-Pad Up while
  a controller-free session gets WASD, E, Enter, and J.

## Verification evidence

- Before the fix, live Unity reported two connected DualSense controllers while
  `ControllerGlyphs.IsGamepadActive` was false; Circle could resolve but the footer chose keyboard copy.
- After the fix, the same editor session reported controller mode active and resolved
  `<sprite name="ps_cross">` / `<sprite name="ps_circle">`.
- The real homecoming narration was opened in Play Mode and visually showed Cross for Continue and
  Circle for Skip in its bottom help line.
- Temporarily disabling both connected pads changed controller mode to false and returned empty
  controller glyphs; restoring them immediately restored PlayStation mode.
- Unity refreshed and compiled the edited scripts. Repository checks passed:
  `lint_hollowfen.py` (0 errors, 0 warnings), `run_integrity.py` (0 errors, 0 warnings), and
  `git diff --check`.
- Per project instruction, no player build was created.

## Files changed

- `Assets/_Hollowfen/Scripts/UI/ControllerGlyphs.cs`
- `Assets/_Hollowfen/Scripts/UI/IntroGuide.cs`
- `Assets/_Hollowfen/Scripts/UI/InteractionPromptHUD.cs`
- `Assets/_Hollowfen/Scripts/Foraging/InspectScreen.cs`
- `Assets/_Hollowfen/Scripts/Localization.cs`
- `Docs/systems/input.md`

## Unfinished / handoff

Do not create or run a player build until Trevor explicitly requests one.
