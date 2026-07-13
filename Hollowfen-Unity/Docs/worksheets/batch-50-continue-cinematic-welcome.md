# Batch 50 — Continue/Load gets the cinematic welcome card

**Date:** 2026-07-13 · **Status:** IN PROGRESS · tag `batch-50` (pending)
**Directive:** Trevor — New Game shows the cinematic welcome (ridge image + "CHAPTER ONE / Homecoming" +
marquee + drifting motes); Continue/Load falls back to the plain "Traveling to Hollowfen…" rolling dots —
mismatched. Make Continue/Load use the SAME cinematic welcome treatment with returning copy
("RETURNING TO / Hollowfen"). NOTE: the seamless image→narration handoff is NEW-GAME ONLY (no intro to
dissolve into on a load) — for Continue just fade the welcome card out to the game normally.

## Design
`LoadingScreen._isCinematic` was `_heroSprite != null && NextIsCinematic` (new game only). Widen it so a
load path can also request the cinematic card WITHOUT the seamless handoff.

- **LoadingScreen.cs**
  - New static `NextIsContinue` beside `NextIsCinematic`. `_isCinematic = _heroSprite != null &&
    (NextIsCinematic || NextIsContinue)`. Both reset in OnOpen.
  - New serialized copy `_returnEyebrow = "RETURNING TO"`, `_returnTitle = "Hollowfen"`.
  - Cache the eyebrow/title TMP refs in BuildCinematic (built once — DDOL screen); OnOpen sets their text
    per mode each open, so New-Game-then-Continue in one session shows the right copy.
- **UIManager.LoadSceneRoutine** — split the old `interactiveWelcome` into:
  - `showWelcome = loading.Cinematic` (EITHER mode → the animated hold-activation card: keeps the card
    alive through the scene-integration stall so a load never reads as a freeze).
  - `seamlessHandoff = cinematicHandoff && showWelcome` (NEW GAME only → hold until narration up, then the
    image→image cross-fade).
  - New tail: `else if (showWelcome)` → no narration to dissolve into, just `FadeOutAndClose(0.6s)` the
    welcome card to reveal the loaded game, then `Back()`. The card even masks the scene pop-in.
- **SaveSlotScreen** (load branch) sets `NextIsContinue = !newGame`.
- **MainMenuScreen.OnContinue** (the menu Continue button — the other load entrypoint) sets
  `NextIsContinue = true` before `LoadSceneAndOpen`.

## Extra fix found in verification
The welcome card shares UIManager's canvas at `sortingOrder=10`, which TIES with `_MiniMapCanvas` (order 10)
— on a continue (live HUD) the minimap poked through the card's top-right corner. New Game never showed it
because its intro hides the HUD. Fix: `_cineRoot` gets its own nested `Canvas overrideSorting sortingOrder=200`
(below the FadeOverlay at 32767) so the card covers the whole screen.

## Verification (play mode, driven via UIManager.LoadSceneAndOpen + EditorApplication.Step)
- [x] Compiles clean (0 errors).
- [x] **Continue/Load**: `LoadingScreen.Cinematic==true`, eyebrow "RETURNING TO", title "Hollowfen";
      full-screen cinematic card (ridge hero + letterbox + gold marquee + motes + "gathering the last
      light…"/"entering Hollowfen") held over the load — `b50_card2.png`. **No minimap poke-through** after
      the sortingOrder=200 fix (before: `b50_card.png`). Card then fades out to the loaded game (Act II
      save, HUD live) — `b50_continue_welcome.png`.
- [x] **New Game** unchanged: `Cinematic==true`, eyebrow "CHAPTER ONE", title "Homecoming",
      seamlessHandoff branch taken (`cinematicHandoff:true`).
- [x] Return-to-main-menu (PauseScreen, no flags) → `Cinematic==false` path (plain) — stays plain.
- No SDF/font churn; scenes not modified.
