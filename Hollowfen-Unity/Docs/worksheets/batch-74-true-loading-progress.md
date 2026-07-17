# Batch 74 — True loading progress

**Date:** 2026-07-15 · **Status:** SUPERSEDED by batch-77 (raw milestone smoothing still froze between Unity reports)
**Directive:** Replace the choppy/random-looking new-game and continue loading rail with a continuously active bar that truthfully reflects load state.

## Delivered

- Replaced the looping marquee streak with a real gold fill, moving clipped sheen, and pulsing leading edge.
- Routed both new-game and continue paths through the same progress contract.
- Mapped Unity's `AsyncOperation.progress` scene-data phase from `0..0.9` into `4..90%`.
- Added explicit truthful milestones for activation started (`94%`), activation complete/first-frame settling (`98%`), and completion (`100%`).
- Made reported progress monotonic: coarse Unity updates are visually smoothed, never reversed, and never exceeded with invented time-based progress.
- Removed the artificial 0.8-second hold at 90%; the loading card now advances as soon as scene data is ready.
- Kept the bar visibly alive during real plateaus through sheen/lead animation rather than fake fill movement.
- Added a live numeric percentage to the atmospheric loading caption.
- Fixed loading-screen reuse: welcome and loading-line coroutines are stopped on close, so repeated Continue operations no longer accumulate writers that fight over the UI.
- Held the completed card just long enough for the smoothed fill to visibly reach 100% before the existing new-game/continue fade handoff.

## Verification

- Unity compilation: zero Console errors.
- Progress mapping: raw `0 / 0.45 / 0.9` → visible `0.04 / 0.47 / 0.90`.
- Monotonic phase test: `0.47 → 0.47 → 0.94 → 0.94 → 0.98 → 1.00` even when lower raw values arrive later.
- Runtime-built hierarchy contains `ProgressTrack/ProgressFill`; legacy `MarqueeTrack` is absent.
- Data Integrity: `ERRORS=0 WARNINGS=0`.

## Files

- `Assets/_Hollowfen/Scripts/UI/LoadingScreen.cs`
- `Assets/_Hollowfen/Scripts/UI/UIManager.cs`
- `Docs/systems/ui-framework.md`
