# Batch 100 — Mill threshold and journal finale polish

**Date:** 2026-07-18 · **Status:** complete

## Goal
Polish the Act I mill objective as one continuous sequence: seat the mill key precisely in its lock,
eliminate the interior rendering instability reported around the table and furnishings, and expand the
hidden-journal pickup into a full cinematic field-guide reveal whose final localized letter is rendered
in Tobin's cursive hand.

## Plan
- [x] Reproduce and measure the key/keyhole alignment and interior rendering fault.
- [x] Correct the insertion pose and mill rendering authority.
- [x] Map the journal narration to the canonical Field Cap, Wood Ear, and Pinecrest full-page paintings.
- [x] Render the localized final page in a dedicated cursive TMP font role.
- [x] Compile and run targeted Play-mode verification.
- [x] Update system docs and finalize this worksheet.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Journal species scope | Field Cap, Wood Ear, and Pinecrest | These are the three canonical complete entries named and unlocked by the Act I journal scene. |
| Journal pacing | Seven beats across six spreads | Each named species receives its own painting and identification read; the final two beats remain on Dad's letter for the signature. |
| Page framing | Preserve full frame with only 0.5–3.5% scale drift | The 1672×940 journal spreads are already cinematic compositions; large Ken Burns crops would hide field marks and marginalia. |
| Dad's hand | Cedarville Cursive, pre-baked static TMP atlas | It reads as handwriting, remains localized live text, and ships without runtime glyph generation. The source and OFL license are both included. |
| Mill rendering | Force only the active interior furnishing hierarchy to LOD0 | The small room can afford 59 active high-detail groups; the exterior and world retain their normal LOD budget. |
| Key alignment | Move the full route 0.025 m screen-right | The same correction applies to approach, insertion, and seated poses, avoiding an end-frame snap. |

## Verification evidence

- Scene diagnosis found 59 active furnishing LOD groups in Play Mode. The populated table alone
  switched at `0.3707266` screen-relative height, explaining the synchronized geometry pop while
  crossing the threshold. `MillInteriorRenderStabilizer` held the active set at LOD0.
- The imported key mesh's measured shaft-tip cross-section is centered to within 0.3 mm; the visible
  miss was therefore authored shot alignment, not mesh-pivot drift. The 0.025 m route correction moved
  the seated pivot about 109 px right in the 3840×2160 verification frame and onto the lock plate.
- Runtime journal contract: seven captions, seven 24 kHz mono VO clips, six images, seven image-map
  entries, full-frame motion enabled, page image 5, and cursive page text enabled.
- The five changed Chatterbox reads validated at 11.14–13.30 seconds, peaks 0.816–0.970, and RMS
  0.0747–0.0869; staged and Unity WAV hashes match.
- Cedarville Cursive loaded from Resources with 82 required glyphs, a 1024×1024 pre-baked atlas,
  static population mode, and no missing authored characters.
- Visual Play-mode review confirmed the species spreads remain legible during their subtle motion,
  Dad's whole localized letter renders as true connected cursive, and the final `Dad.` caption remains
  the last emotional beat.
- Unity compile/console: zero errors. `lint_hollowfen.py`: `ERRORS=0 WARNINGS=0 WAIVED=1`.
  `DataIntegrity.RunAllAsReport`: `ERRORS=0 WARNINGS=0` across 28 story moments and the complete data
  graph. `smoke_play.py`: PASS after 362 frames with zero in-play errors and zero post-stop errors;
  teardown skips groups Unity has already disabled.
- Targeted non-scene `git diff --check`: PASS. The already-dirty shared scene retains unrelated
  Unity-authored whitespace outside this batch's two serialized additions.

## Docs updated

- `Docs/systems/quests.md`
- `Docs/systems/ui-framework.md`
- `Docs/systems/localization.md`
- `Docs/systems/audio.md`
- `Docs/tests.md`

## Unfinished / handoff

No implementation blocker remains. The final human pass is the normal start-to-finish quest play:
receive Bram's key, unlock the mill, walk and turn around the table, then pick up the journal without
using debug quest state.

## Feedback to Trevor

The journal now teaches before it grieves: Wren studies one complete identification spread at a time,
learns the structural differences among gills, jelly folds, and pores, then reaches the warning and
only afterward discovers her father's letter. That makes the mushroom knowledge feel earned while
preserving the scene's emotional turn.
