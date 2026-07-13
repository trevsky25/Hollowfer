# Batch 47 — Square-corner sweep: rounded chrome everywhere

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-47` (pending)
**Directive:** Trevor — "square box around the dialog boxes, the pop up text boxes, and other elements
where they are supposed to be rounded… compass and on screen elements have it as well."

## Root cause of the reported square (interaction prompt)
Runtime audit of every canvas for sprite-less Images (the square signature) found the prompt's box:
`InteractionPrompt/Bg` + `GoldRule` (+`Label`) were **stale scene-serialized children from an older HUD
iteration**, rendering a solid square rectangle behind the code-built rounded pill. `BuildIfNeeded` never
wiped pre-existing children. Fixed both ways: (a) the code now destroys all children before building
(self-healing), (b) the stale children were deleted from Scene_Hollowfen (saved).
- Compass/tracker/clock/coin were already rounded (compass "square band" = a roof beam behind it;
  WaypointPip square is rotated 45° = the intentional diamond).
- `_MapCanvas` stale serialized children exist but MapScreen wipes children at build → never render; skipped.

## The sweep (square-by-construction chrome → design-system rounded)
New helpers `UICanvasUtil.Roundify(img, radius)` / `RoundifyOutline(img, radius, thickness)`. Changes:
- **InspectScreen**: panel → rounded parchment 24 (parchment TEXTURE dropped for the flat paper family —
  matches pause/settings/ConfirmModal), vignette inset 10 so its gradient doesn't overhang the curve,
  4-strip double frame → two rounded inset outlines (16/12), preview frame/bg → 16/14, edibility chip →
  pill 15 + Circle dot, Forage/Leave buttons → 14 + rounded outline + glyph chip 10. Dead `BuildRect` removed.
- **InventoryScreen**: same conversions (panel 24, frames 16/12, preview 16/14, close button 14 + outline,
  glyph 10). Dead `BuildEdges` removed.
- **StoryScreen + FieldGuideScreen** cards: face → 14 + `Mask` clipping the square art/photo thumb to the
  radius; FieldGuide edibility dot → Circle.
- **WrenScreen**: stat cards/kit tiles 12, body cards/pullquote 14.
- **MushroomDetailScreen**: edibility chip → pill 19, features/notes cards 14.
- **SaveSlotScreen**: scene-authored slot rows Roundified 16 at OnInitialize (legacy TMP-migration pending).
- Intentional squares kept: scrims, full-bleed BGs, hairline rules/underlines/separators, letterbox bars.

## Two rendering gotchas found while verifying (logged for the doc)
1. **UI `Mask` + URP renders the mask graphic CYAN** (and kills the rounding) — first card-clipping
   attempt used `Mask(showMaskGraphic=true)` on the rounded fill; replaced with an inset "mounted
   photograph" composition (thumb inset 8px inside the rounded card) — reads better anyway.
2. **uGUI `Outline` component + sliced sprite = gray wash** — Outline re-draws the whole 9-slice 4×
   offset, stacking alpha over the card. Replaced every Outline on rounded chrome with a
   `RoundifyOutline` hairline child (Story/FieldGuide cards, Wren stat/kit/body cards,
   MushroomDetail features/notes).

## Verification (play mode)
- [x] Compiles clean.
- [x] Prompt renders as a clean pill at Bram — `zoom_prompt_fixed.png` (children now exactly Bg/Hairline/Label).
- [x] Inspect screen: rounded panel, rounded double frames, rounded FORAGE/LEAVE buttons + glyph chips —
      `inspect_rounded.png`.
- [x] Field guide: rounded ink cards + hairline, inset photos, circle dots, rounded gold focus glow —
      `fieldguide_final.png` (after the Mask→inset and Outline→hairline fixes).
- Story/Wren/Detail/SaveSlot use the identical helpers/pattern; Trevor doing the visual pass.
