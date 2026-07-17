# Batch 91 — Neutral world color correction

**Date:** 2026-07-16 · **Status:** verified; awaiting commit/tag

## Goal

Remove the yellow world tint introduced by batch-89 without sacrificing its stable 60fps pacing or main-camera
quality improvements.

## Root cause

The production policy enabled URP post-processing so SMAA High, stop-NaN, and dithering could run. The gameplay
scene also contains the environment pack's global `Medieval Fantasy Volume Profile`, which had previously been
inert because the camera bypassed post-processing. That vendor profile applies a deliberately warm grade:

- gain `(1.00, 0.92, 0.82)`;
- lift `(1.00, 0.97, 0.96)` and gamma `(0.93, 0.93, 1.00)`;
- warm color filter `(1.00, 0.953, 0.939)` and temperature `+3`;
- ACES, depth of field, motion blur, and film grain.

It stacked with Hollowfen's time-of-day lighting and shifted the entire world toward yellow/orange.

## Implementation

`ProductionPerformancePolicy` keeps `renderPostProcessing=true`, but now assigns the tagged main camera a zero
volume layer mask. URP can still execute SMAA High, NaN suppression, and dithering; no global or local scene
volume can silently recolor gameplay. Day/night presentation continues through the existing sun, skybox,
ambient trilight, fog, and reflection controls—the look Trevor approved before the performance pass.

The vendor profile and scene object were not edited, so third-party assets remain intact. Preview, field-guide,
map, and RenderTexture cameras are still outside the production main-camera policy.

## Verification evidence

- Live 120Hz policy state: target `60`, VSync `2`, HDR on, dynamic resolution off, post pipeline on,
  `SMAA High`, volume mask `0`.
- Midday visual check restored cool sky/stone neutrals, foliage greens, and Wren's red clothing without the
  global amber wash: `Assets/Screenshots/batch91-neutral-color-performance.png`.
- Midnight check remained dark and cool through Hollowfen's direct lighting/fog/sky controls; no yellow cast.
- Focused post-start console check: zero new errors after the known vendor collider startup noise was cleared.
- C# compile: zero errors.

## Files changed

- `Assets/_Hollowfen/Scripts/Settings/ProductionPerformancePolicy.cs`
- `Docs/systems/settings.md`
- `Docs/steam-constraints.md`
- `Docs/worksheets/batch-89-production-performance-policy.md`
- `Docs/worksheets/batch-91-neutral-world-color.md`
