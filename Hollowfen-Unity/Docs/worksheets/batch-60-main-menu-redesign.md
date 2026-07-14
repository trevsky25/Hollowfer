# Batch 60 — Main menu redesign: Forage hero, leaner nav, no warm gradient

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-60` (pending)
**Directive (Trevor):** hero text → "Forage" (familiar), clicking it loads the 4-journal picker; drop the
Continue button; make Story/Wren/Field Guide/Settings bigger + uncap the Settings text; remove the yellow
gradient behind the text completely — cleaner without it.

## What changed
- **Hero → "Forage →"** (was "New Game →"). Its button already opened the save-slot picker, which lists all
  four journals (existing saves to continue + empty slots to begin) — so "Forage" cleanly covers both
  new-and-continue. Verified: Forage click → `SaveSlotScreen`.
- **Continue button removed** (redundant now the hero opens the journal picker). Deleted `Btn_Continue` +
  its leading divider from the NavRow (HorizontalLayoutGroup reflows). `MainMenuScreen` cleaned up:
  dropped the `_continueButton` field, `OnContinue`, `HasAnySave`, `GameplaySceneName`, and the unused
  `Hollowfen.Save`/`SceneManagement` usings; `DefaultSelected` now = the hero (verified default focus =
  Btn_NewGame). `OnNewGame`→`OnForage`.
- **`ACH_NEWGAME_FIRST` moved** from the menu click → `SaveSlotScreen.OnSlotSelected` new-game branch (it
  should fire when a new journal is actually started, not when opening the picker — which now also serves
  "continue").
- **Nav bigger + Settings uncapped**: Story/Wren/Field Guide/Settings labels 16 → **26**; each button's
  `LayoutElement` min/pref width re-fit to the text's `GetPreferredValues` at the new size; dividers → 24;
  "SETTINGS" → "Settings" (title-case, matching the others). All 4 read as one consistent nav now.
- **Yellow gradient removed**: deleted the runtime `Cinematic_LeftWarm` warm-amber band in
  `MenuCinematics` (batch-54). The hero image reads at true colour; the neutral dark legibility gradients
  (scene `Overlay_Left/BottomGradient`) + the vignette still carry the left-column text. `_leftWarm*`
  fields kept (unused) for easy restore.

## Verification (Play mode)
- Screenshot `b60_menu.png`: true-colour Wren/forest (no gold cast), "Forage →" hero, nav row
  "Story · Wren · Field Guide · Settings" larger + title-case, fits the left column, all legible on the
  darker true-colour image. `leftWarmGradientExists=False`, `defaultFocus=Btn_NewGame`.
- Forage click → `SaveSlotScreen`. All nav buttons `navMode=Automatic` (deleting Continue doesn't break
  the gamepad chain). Compile clean, no errors.

## Test script for Trevor
1. Play from `Scene_MainMenu` → the image is true-colour (no yellow tint); the hero reads "Forage →".
2. Click/press Forage → the four-journal picker (continue a saved journal, or pick an empty slot to begin).
3. There is no Continue button; the nav row (Story · Wren · Field Guide · Settings) is larger and Settings
   is title-case. Pad-navigate it — Automatic navigation still reaches every item.
4. (If you want the warm tint back, it's a one-line un-comment in MenuCinematics — the fields are retained.)
