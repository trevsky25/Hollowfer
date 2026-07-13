# Batch 35 — Cinematic Pass #1: living-painting main menu

**Date:** 2026-07-12 · **Status:** DONE (play-verified, screenshots) · tag `batch-35` (pending)
**Directive:** Trevor — "I want the UI to be AMAZING and cinematic. this should wow the user." Chose the
**Main Menu (living painting)** as the first Cinematic Pass target.

## What shipped
`MenuCinematics.cs` — one runtime component on the MainMenuScreen Canvas that turns the static hero menu
into a living painting. All effects are **UI-native** (the canvas is Screen-Space-Overlay, so a camera
post-processing volume can't touch it) and **procedural** (no new art assets — sprites are generated in code):
- **Drifting golden spores** (34 motes): soft radial-gradient dots, warm gold, per-mote upward drift + sine
  sway + alpha twinkle, wrapping at the edges. Thematic for a mushroom game (spores catching the light).
- **Soft mist** (5 large low-alpha sage puffs) drifting slowly across the lower scene for depth.
- **Painterly vignette + warm grade**: two full-screen overlays — a radial dark-edge vignette + a very subtle
  warm tint — that frame and unify the palette (the "faked" post-processing that composites in overlay).
- **Ken Burns**: the hero art (`BG_WrenImage`) slowly ping-pong scales (1.0↔1.06 over ~48s) + drifts, so the
  painting breathes.
- **Ink-bleed reveal**: on menu open, the title block (eyebrow → title → divider → subtitle → tagline → CTA →
  nav) fades + rises in a staggered ease-out; the "Hollowfen" title gets a longer bloom-in (scale 1.035→1.0).
- Finds the existing menu elements by name → **no serialized wiring**, no new art, works in the build (pure
  runtime UnityEngine.UI + Texture2D; no editor-only APIs).

## Design decisions (restraint won)
- **Overlay canvas → UI-native atmosphere, not a post-processing Volume.** Verified `renderMode =
  ScreenSpaceOverlay` first; real bloom/vignette would be composited away. So vignette/grade are UI overlays
  and the motes use bright soft sprites that read as glowing without bloom.
- **Cut a title glow.** First pass added a warm halo behind "Hollowfen" — it rendered as a floating smudge
  above the title (RectTransform pivot math) and read cheap. Removed. Lesson recorded: on this strong hero
  art, obvious added elements look wrong; the win is *subtle motion*, and the art carries the mood.
- **Spores over dust.** Bumped motes from many-tiny to fewer-larger-brighter (34, 4–14px, twinkling) so they
  read as gentle drifting spores in motion without dominating the still.

## Gotcha recorded
Adding a component via the bridge **serializes the code defaults at add-time**; later changing a
`[SerializeField]` default in code does NOT update the already-added instance. To pick up new defaults, remove
+ re-add the component (or set each field via `manage_components set_property`). Computed (non-serialized)
values and new code paths DO take effect on recompile.

## Verification
- Compiles clean (0 CS errors). Lint 0/0, integrity 0/0.
- Play-mode: atmosphere builds (34 motes / 5 mist / vignette confirmed via bridge), title reveal completes
  (alpha 0→1), Ken Burns drifts (bgScale 1.0→1.02+). Two frames ~1.2s apart show the motes drifted to new
  positions and the framing shifted — the menu is provably *moving* (screenshots `b35_frameA/B.png`).
- Captured via `ScreenCapture.CaptureScreenshot` (overlay UI needs it — a camera-render screenshot excludes
  Screen-Space-Overlay canvases).

## Next in the Cinematic Pass (proposed)
- **Menu focus glow + ambient audio bed** (interactive + sound — the other half of "cinematic").
- Then the other surfaces from the arc: cinematic dialogue (letterbox + two-shot), story-beat moments, the
  discovery moment.

## Docs updated
`systems/ui-framework.md` — MenuCinematics living-painting layer + the overlay-UI-native-atmosphere pattern.
