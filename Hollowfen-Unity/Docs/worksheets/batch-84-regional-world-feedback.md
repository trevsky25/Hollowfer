# Batch 84 — Regional world feedback

**Date:** 2026-07-16 · **Status:** DONE in working tree (compile, focused Play Mode verifier, visual QA, and regression smoke green)
**Directive:** Make the newly lit and scheduled world feel alive through region-aware day/night ambience, adaptive music state, and restrained location-entry presentation before cast models and world dressing.

## Experience

- Hollowfen Village, the Wend, the Old Wood, and Aldric's Manor now have distinct deterministic procedural atmospheres with separate day and night beds.
- Ambience follows the same continuous night blend as lighting; region changes use a four-second equal-power crossfade rather than cutting or restarting.
- The existing Misty Forest score stays phase-continuous while its volume and low-pass response glide by region and time. Two banks are ready for dedicated regional tracks later.
- Entering a known region presents a quiet top-center title and one-line mood note. It uses unscaled time, captures no input, pauses nothing, and sits below map/dialogue layers.

## Implementation

- Refactored `AmbienceManager` from a menu-only forest loop into the shared menu/gameplay engine: two day/night sources per bank, two region banks, lazy fixed-seed synthesis/cache, 48 kHz mono output, seamless loop fold, limiter, mixer routing, and live Ambience preference.
- Refactored `MusicManager` into a two-bank adaptive state engine. Null region tracks inherit the main bed without a restart; state volume/filter changes glide smoothly.
- Added `RegionCatalog` as the localized presentation authority shared by map and arrival UI.
- Added `RegionArrivalToast` as code-built overlay order 14, below map/dialogue and above ordinary HUD.
- Added southern-village, clear-cut, and manor volumes; assigned explicit priority to all six authored triggers; trigger disable now unregisters stale state.
- Added idempotent importer and focused verifier.

## Verification

- C# compilation: zero errors.
- `WorldFeedbackVerifier.RunAll`: PASS — four localized regions, six trigger volumes, real event fan-out, 48 kHz distinct day/night synthesis, region caching/crossfade, adaptive score states, independent ambience mute, and arrival title.
- Visual QA: `Docs/screenshots/batch-84/region-arrival-old-wood.png` at 1280×800. The title clears quest/minimap chrome and remains readable without obscuring Wren or the route.
- General smoke: PASS after at least 240 frames with zero new console errors.
- Save hygiene: no save methods are called; verifier restores clock, preference, manager state, and toast visibility.
- Lint/data integrity: zero errors and zero warnings.
- Batch-scope `git diff --check`: clean after Unity scene whitespace normalization.

## Performance shape

- Only two loops for the active region are synthesized at startup. Other region pairs synthesize lazily once and remain cached for the scene; maximum four profiles is roughly 18 MB of mono float sample data.
- Four ambience sources and two music sources are 2D, non-spatial, and mixer-routed. No per-frame allocations, scene searches, imported ambience assets, or shadow/render cost.
- Continuous work is six volume assignments/filter moves plus the already-evaluated lighting blend. Region synthesis occurs once per first visit, not every transition.
- Arrival UI builds once and otherwise sleeps between events.

## Primary files

- `Assets/_Hollowfen/Scripts/Audio/{AmbienceManager,MusicManager}.cs`
- `Assets/_Hollowfen/Scripts/Map/{RegionCatalog,RegionTrigger,MapScreen}.cs`
- `Assets/_Hollowfen/Scripts/UI/RegionArrivalToast.cs`
- `Assets/_Hollowfen/Scripts/Editor/{WorldFeedbackImporter,WorldFeedbackVerifier}.cs`
- `Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity`
- `Docs/systems/{audio,map}.md`, `Docs/tests.md`, and `Docs/screenshots/batch-84/`
