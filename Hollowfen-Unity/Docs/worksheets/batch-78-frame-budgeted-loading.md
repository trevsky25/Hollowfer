# Batch 78 — Frame-budgeted scene loading

**Date:** 2026-07-16 · **Status:** DONE (compile + profiler + data integrity verified)
**Directive:** The new-game and continue loading card must keep rendering while the large Hollowfen scene loads; percentage smoothing alone is insufficient if Unity starves the main thread.

## Root cause

Batch 77 correctly advanced the presented percentage on every rendered frame, but the full `Scene_Hollowfen` transition could still stop producing frames. Unity's background scene integration was running at its default priority, and several hidden gameplay systems also allocated large RenderTextures and built UI during scene startup. The real bar therefore appeared frozen even though its own update logic was continuous.

Unity 6 also reported the held async operation's ready plateau as approximately `0.888…` in the live transition. A `0.89` activation gate could remain closed even after the operation had reached its quantized plateau.

## Implementation

- `UIManager` temporarily sets `Application.backgroundLoadingPriority` to `BelowNormal` only for the cinematic new-game/continue transition, then restores the caller's previous priority in `finally`.
- The activation gate releases at `0.88`; Unity still owns the genuine readiness check and will not activate early.
- `MapCamera` no longer allocates its 2048×1024 render target or renders offscreen at scene startup. `MapScreen` enables and initializes it on first open, then disables it on close.
- `MushroomPreviewer` no longer creates its 1024×1024 target, camera, or lights in `Awake`. The shared rig is created by the first real specimen request and is disabled whenever no model is shown.
- `InspectScreen` and `InventoryScreen` defer their programmatic UI construction until first open and bind the lazy preview texture after `Show()` creates it.
- `LoadingScreen` records rendered-frame count and longest frame gap for editor/development diagnostics; `UIManager` emits the result after genuine completion.

## Verification

- Unity compilation completed with zero errors.
- Full main-menu → Hollowfen profiling showed `Loading.UpdatePreloading` / `Application.Integrate Assets in Background` constrained to roughly 1.5 ms per sampled frame under the stricter `Low` diagnostic setting; final shipping balance uses `BelowNormal` for greater throughput while retaining a bounded integration slice.
- The live Unity 6 `0.888…` plateau was reproduced and the activation gate no longer waits above it.
- Before first use, both the full-map runtime texture and mushroom-preview runtime texture remained unallocated and the full-map camera remained disabled.
- Data Integrity: `ERRORS=0 WARNINGS=0`.

## Files

- `Assets/_Hollowfen/Scripts/UI/UIManager.cs`
- `Assets/_Hollowfen/Scripts/UI/LoadingScreen.cs`
- `Assets/_Hollowfen/Scripts/Map/MapCamera.cs`
- `Assets/_Hollowfen/Scripts/Map/MapScreen.cs`
- `Assets/_Hollowfen/Scripts/Foraging/MushroomPreviewer.cs`
- `Assets/_Hollowfen/Scripts/Foraging/InspectScreen.cs`
- `Assets/_Hollowfen/Scripts/Foraging/InventoryScreen.cs`
- `Docs/systems/ui-framework.md`
