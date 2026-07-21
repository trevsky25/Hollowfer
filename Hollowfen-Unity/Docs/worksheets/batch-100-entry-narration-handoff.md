# Batch 100 — Entry narration handoff

**Date:** 2026-07-17 · **Status:** verified

## Goal

Keep the new-game homecoming voice-over silent until the cinematic loading card has visibly reached 100%, and make the welcome narration show PlayStation controls immediately when the new game was entered with a PlayStation controller. Per Trevor's direction, this batch changes and verifies the Unity project only; it must not create or run a player build.

## Plan

- [x] Trace the loading-progress, scene-activation, narration, and controller-glyph paths.
- [x] Add an explicit loading-to-intro release boundary.
- [x] Preserve the menu Submit device and resolve the narration hint before its first visible frame.
- [x] Verify compilation and both behaviors in Unity Play Mode without building.
- [x] Update system docs and finalize this worksheet.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Intro timing | Loading presentation owns a short-lived intro hold and releases it after displayed progress reaches completion | Scene activation happens before the smoothed loading meter finishes; a shared lifecycle boundary is deterministic and avoids an arbitrary delay. |
| Prompt source | Record the device that performed `UI/Submit`, then refresh narration help immediately when shown | The input that starts the new game is the strongest signal of the player's intended control scheme, and immediate refresh prevents a one-frame keyboard fallback. |
| Build | Do not build | Trevor explicitly requested Unity-only changes until he says otherwise. |

## Verification evidence

- Unity script refresh/compile completed with zero Console errors.
- Data Integrity completed with `ERRORS=0 WARNINGS=0` across quests, dialogue, NPCs, locations, mushrooms, story cards/moments, character profiles, endings, and village requests.
- Normal Main Menu → empty journal → new-game transition, driven with a connected `DualSenseGamepadHID`:
  - At displayed loading progress `0.418`, `UIManager.IsCinematicIntroHeld=True`, narration was not showing, and DualSense mode remained active.
  - The first frame on which `NarrationOverlay.IsShowing=True` was observed at displayed progress `1.000`; the loading screen was still the top screen for the intended cross-fade.
  - The initial hint was `<sprite name="ps_cross"> · continue / <sprite name="ps_circle"> · skip`, not the keyboard fallback.
  - The first `00_Narrator` clip was assigned only after progress `1.000`; after unpausing stepped verification its AudioSource reported `isPlaying=True` at `time=1.81s`.
  - After the loading card closed, a keyboard device note switched prompts out of gamepad mode and DualSense switched them back, proving the temporary mode hold releases normally.
- A temporary Slot 0 journal created by the test was deleted after each run (`Ready → Empty`); all four slots were empty before testing.
- Unity was returned to `Scene_MainMenu` in Edit Mode and the Console was cleared.
- No player build was created or run.

## Docs updated

- `Docs/systems/ui-framework.md` — records visible 100% as the hard new-game intro/VO boundary.
- `Docs/systems/input.md` — records semantic Submit-device carryover and cursor-warp suppression.
- `Docs/systems/audio.md` — records the homecoming VO gate.

## Unfinished / handoff

No implementation work remains. Do not create a player build until Trevor explicitly requests one.

## Feedback to Trevor

The original prompt mismatch was not a missing PlayStation glyph mapping. Cursor locking during scene activation emitted mouse-state noise and displaced the correct last-used device just before narration; preserving the semantic Submit device across the transition fixes that lifecycle edge case.
