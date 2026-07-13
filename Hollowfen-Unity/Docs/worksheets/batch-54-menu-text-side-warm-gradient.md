# Batch 54 — Main menu: warm tint → text-side gradient only

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-54` (pending)
**Directive (Trevor):** "I like the yellow tint on the main menu but I don't want it everywhere — I want
Wren foraging to be the TRUE colour of the image, and only have the yellow cleanly faded as a background
behind the text."

## Change (`MenuCinematics.cs`, code-only — the menu builds at runtime)
- **Removed** the full-screen `Cinematic_WarmGrade` (`Color(0.82,0.62,0.34, 0.06)`) that tinted the entire
  hero gold.
- **Added** `Cinematic_LeftWarm`: a warm-amber gradient anchored to the LEFT, spanning `_leftWarmWidth`
  (0.56 of screen width), opaque (`_leftWarmStrength` 0.9) at the far-left edge and cleanly faded to
  transparent before the hero — via a new `MakeHorizontalFade` sprite (alpha 1→0 left→right, pow falloff
  1.9). Sits below the TextCard (text stays on top), above the vignette.
- **Softened** the vignette `0.55 → 0.40` so Wren's side reads truer.

## Verification (play mode, Scene_MainMenu)
- [x] Compiles clean; `Cinematic_WarmGrade` gone, `Cinematic_LeftWarm` present (~56% width).
- [x] Screenshot `b54_menu.png`: Wren foraging is TRUE colour (red vest, green forest, gold chanterelles,
      birch bark all read cleanly) with no overall tint; the warm gold is a clean left→right gradient behind
      the title/tagline/nav that fades out well before Wren. Text legible on the warm band.

Tunable via `_leftWarmColor` / `_leftWarmStrength` / `_leftWarmWidth` if Trevor wants it warmer/wider.
