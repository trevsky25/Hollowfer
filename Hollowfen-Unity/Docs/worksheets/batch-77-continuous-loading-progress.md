# Batch 77 — Continuously advancing loading progress

**Date:** 2026-07-15 · **Status:** SUPERSEDED by Batch 78 (the presentation model was correct, but the full scene transition could still starve rendered frames)
**Directive:** The new-game and continue loading fill must visibly advance from 0 to 100 instead of freezing at Unity's coarse `0 → 0.9 → done` scene-loading reports.

## Root cause

Batch-74 smoothed only toward the latest raw `AsyncOperation.progress` milestone. That preserved a strict measurement but could not move while Unity held the same value, producing the observed `0%` freeze, jump to roughly `94%`, second freeze, then completion.

## Implementation

- Reframed the fill as a phase-aware presentation meter. Real Unity progress accelerates it, while a gentle monotonic crawl keeps it moving when `AsyncOperation` does not report new work.
- Scene-data presentation is capped below activation; activation/settling has its own cap; only `CompleteLoad()` can unlock 100%.
- Catch-up speed is bounded so a coarse Unity milestone cannot visually teleport the fill across the rail.
- Per-frame presentation delta is capped, preventing a long scene-integration frame from turning into a large visible jump on the first frame afterward.
- Completion has a faster but still animated convergence, with up to 1.5 seconds to land on 100% and a 0.12-second readable completion beat before fading.
- The moving sheen, lead pulse, caption percentage, new-game handoff, and continue handoff remain driven by the same displayed value.
- Added pure `AdvanceDisplayedProgress(...)` logic so monotonic movement, caps, and completion can be tested without loading a scene.

## Verification

- Unity compilation completed with zero errors; only established project/package warnings remain, with no new `LoadingScreen` warning.
- Five-second simulated raw-progress plateau: visible progress advanced on all `300/300` frames and reached `14.1%` instead of freezing.
- Coarse raw jump to `90%`: visible progress advanced smoothly to `46.1%` over the next two seconds rather than teleporting.
- Simulated five-second integration frame: the next rendered-frame step was capped at `0.8%`.
- Scene-data cap `93.5%`; activation/settling cap `98.5%`; neither phase can reach 100%.
- Genuine completion animated from `46.9%` to `100%` in 42 frames, monotonically.
- Live Play Mode `LoadingScreen` test: the real fill advanced on every sampled rendered frame through a five-second zero-report plateau (`12.4%`), continued from the coarse milestone to `44.4%` before completion, never flattened or reversed, and landed with both `ProgressFill=99.9%` and caption `entering Hollowfen · 100%` at `7.708s`.
- Data Integrity: `ERRORS=0 WARNINGS=0`; post-verification Unity console: 0 errors.

## Files

- `Assets/_Hollowfen/Scripts/UI/LoadingScreen.cs`
- `Assets/_Hollowfen/Scripts/UI/UIManager.cs`
- `Docs/systems/ui-framework.md`
