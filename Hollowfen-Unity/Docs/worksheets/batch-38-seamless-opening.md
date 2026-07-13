# Batch 38 — Seamless opening: cinematic welcome → intro → game

**Date:** 2026-07-12 · **Status:** DONE (play-verified, screenshots) · tag `batch-38` (pending)
**Directive:** Trevor — "the intro png card should transition nicely into the load screen with welcome text
pop up and game load. it should all be seamless." Chose (via question) the **unified** flow: welcome card →
intro → game, with loading hidden inside the cinematic.

## What shipped
The new-game opening is now one continuous homecoming-image sequence with no plain load screen and no
gameplay flash:
1. **Cinematic welcome load screen** (`LoadingScreen` rebuilt) — for a new game it shows `homecoming.png` at
   the narration's **exact Ken-Burns A-state** (scale 1.20, pos A) with matching **letterbox bars**, a
   **"CHAPTER ONE / Homecoming"** welcome title that **pops up** (fade+scale ease), a bottom scrim, and a
   discreet italic "gathering the last light…" loading line. Scene_Hollowfen loads behind it.
2. **Opaque narration start** (`NarrationOverlay`) — cinematic mode now appears opaque immediately at the same
   A-state + full letterbox (no fade-from-transparent), so there's no gameplay glimpse.
3. **Seamless handoff** (`UIManager.LoadSceneAndOpen(..., cinematicHandoff)`) — after the scene loads, the
   welcome card **holds until the in-scene narration is showing** (same image), then **cross-fades out** over
   0.6s to reveal it. Image→image, same framing, same letterbox → seamless. Then the batch-36 narration plays
   (Ken Burns + the restored 6-beat passage + VO) and fades to reveal the game at the new spawn (batch-37),
   into the first-steps guide.
4. **Routing** (`SaveSlotScreen`) — only the **new-game** branch uses the cinematic handoff; Continue/Load keep
   the plain text load.

## Design
- Reuses the batch-36 in-scene narration rather than moving it pre-load — the welcome card just **wraps** the
  load and hands off to it. Much lower risk than a persistent pre-load controller, identical visual result
  (the loading is completely hidden behind the welcome card + first narration beats).
- Handoff safety: the hold has a 4s timeout (so a missing narration can't hang the load); the loading canvas is
  sort 90 (above narration's 70), so it stays on top until it fades.

## Verification (play-mode, fresh save, full flow)
Triggered `StartNewGame(0)` + `LoadSceneAndOpen("Scene_Hollowfen", null, true)`:
- Welcome card renders (homecoming image + CHAPTER ONE / Homecoming + letterbox + loading line) —
  screenshot `b38_flow_welcome.png`. (Fixed: the old legacy "LOADING" eyebrow was hiding it → now disabled.)
- Scene loads behind it; when narration is up the card fades out and closes; narration shows the same image
  seamlessly — screenshot `b38_flow_narration.png` (caption "It had been three years…").
- Verified: `activeScene=Scene_Hollowfen`, `NarrationOverlay.IsShowing=true`, loading gone.
- Compiles clean; lint 0/0; integrity 0/0.

## Known minor
Subtle brightness difference between the welcome card and the narration image (welcome's scrim/treatment vs the
narration's) — the handoff is image→image seamless in framing; a perfect brightness match can be tuned if Trevor
notices it in the live click-through.

## Docs updated
`systems/ui-framework.md` — LoadingScreen cinematic welcome + the seamless-handoff flow.
