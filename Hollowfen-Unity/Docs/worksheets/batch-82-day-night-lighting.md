# Batch 82 — Actual day/night lighting

**Date:** 2026-07-16 · **Status:** DONE in working tree (compile, focused Play Mode verifier, visual QA, and regression smoke green)
**Directive:** Make night genuinely darker and atmospheric while preserving navigation, then turn the existing clock into a cohesive day, dusk, and night presentation.

## Visual arc

1. Deep night: dark blue procedural sky, low cool moon key, restrained reflections, denser blue fog, and warm inhabited landmarks.
2. Dawn: the moon yields smoothly to a low amber sun while exposure, fog, and ambient color lift together.
3. Day: neutral-warm sunlight and a readable misty village retain the established environment-pack character.
4. Golden hour: light and sky warm before losing intensity.
5. Dusk: color and exposure descend continuously into the moonlit state rather than popping at sundown.

## Implementation

- Split rendering policy out of `TimeManager` into `DayNightLighting`; all ordinary ticks, rests, and explicit time jumps use the same evaluated hour.
- Added ten smooth art-direction keyframes across the directional key, Trilight ambient, fog, reflection intensity, procedural sky, and URP post grade.
- Runtime-cloned the vendor skybox and created a runtime-only priority volume, so source assets stay untouched and the pack's fixed `+1` exposure no longer makes night look like dim daytime.
- Added six broad, shadowless `NightLight` pools at inhabited village landmarks. They fade from the shared night blend, flicker gently without allocations, and fully disable by day.
- Added an idempotent scene importer and focused verifier. Public lighting readbacks exist for verification/debugging, while the keyframe table remains code-owned.

## Verification

- C# compilation: zero errors.
- `DayNightLightingVerifier.RunAll`: PASS — progressive noon/dusk/night exposure; darker sky and ambient; deeper fog; cool low-intensity moon; six village practicals on at night and off at noon.
- Focused verifier runtime console: zero errors (`0|48|6`; warnings/logs are known vendor and scene noise).
- Visual QA: fixed elevated day/dusk/night captures plus a ground-level late-night village-well capture under `Docs/screenshots/batch-82/`. Night reads immediately as night, the route and silhouettes remain legible, and the warm practical gives the cool scene a focal anchor.
- General smoke: PASS after at least 240 frames with zero new console errors.
- Save hygiene: verifier and visual captures did not save; the precautionary focused-run backup was restored.
- Batch-scope `git diff --check`: clean after Unity scene whitespace normalization.

## Performance shape

- No new meshes, textures, shaders, baked lights, or shadow casters.
- The state table is static; per-frame evaluation allocates nothing and touches one key light, RenderSettings, one small runtime volume, and a cloned sky material.
- Six spatially separated point lights use no shadows and disable under 1% night blend. Each practical performs one Perlin sample and one `MoveTowards` per frame.
- A visible lantern/fixture pass remains world dressing and should recheck the Steam Deck village frame-time floor if it adds overlapping shadowed lights.

## Primary files

- `Assets/_Hollowfen/Scripts/Time/{DayNightLighting,NightLight,TimeManager}.cs`
- `Assets/_Hollowfen/Scripts/Editor/{DayNightLightingImporter,DayNightLightingVerifier}.cs`
- `Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity`
- `Docs/systems/time.md`, `Docs/tests.md`, and `Docs/screenshots/batch-82/`
