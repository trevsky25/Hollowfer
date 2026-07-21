# Batch 106 — Field identification gate + region UI clearance

**Date:** 2026-07-18 · **Status:** complete

## Goal

Keep region-arrival titles clear of the compass, make mushroom quiz focus and feedback immediately
legible, celebrate a passed inspection with an animated verification mark, and require every species
to pass its field-identification test before any wild or cultivated specimen can be harvested.

## Plan

- [x] Trace the region toast layout, identification UI, knowledge states, save flags, and harvest policy.
- [x] Move the region toast into a compass-safe top band and add a verifier seam.
- [x] Restyle quiz choices with an unmistakable focused state and contain all hint/error copy.
- [x] Add an unscaled animated specimen-verification checkmark and accessible completion copy.
- [x] Separate journal discovery from field verification at every harvest entry point.
- [x] Compile, run focused verification and smoke checks, capture visual evidence, and update system docs.

## Progression contract

- A readable/discovered journal page is knowledge, not permission to cut a specimen.
- `mushroom_identified_<speciesId>` is the persistent proof that the player passed that species'
  three-stage field test. Existing saves that already completed a test keep that permission.
- Story-taught Field Mushroom, Wood Ear, Pinecrest, and every other species still require their own
  field verification once; harvesting never grants or backfills verification.
- Both the inspect CTA and the synchronous/cinematic harvest commit path recheck the same shared rule.

## Implementation

- `RegionArrivalToast` now lands at a 140px top inset instead of touching either the compass pill or
  its optional waypoint label; `WorldFeedbackVerifier` rejects any future inset below 132px.
- Quiz options now rest as dark ink cards and gain a gold fill, leading focus marker, 2.5px outline,
  contrasting dark label, and slight scale lift when selected. Wrong answers retain a persistent red
  state as focus moves on.
- Hint, correction, and next-step copy share a 650×78 rounded feedback card with normal wrapping,
  autosizing, padded bounds, and ellipsis as a final overflow guard.
- Passing Safety hides the decision controls and plays an unscaled stamped-seal animation: an
  expanding gold ring, green field seal, rotating/overshooting checkmark, explicit verification
  copy, and the existing **Prepare to Cut** footer.
- `MushroomKnowledge.IsFieldIdentified` is now the canonical persisted proof. Page state and harvest
  policy use it; `RecordIdentification` succeeds correctly when a story already marked the journal
  page discovered. Harvest no longer calls `MarkDiscovered` as an accidental back door.
- `GameplayFoundationVerifier` proves all 21 story-unlocked/discovered profiles remain blocked with
  no identification flags, names Field Mushroom/Wood Ear/Pinecrest explicitly, then proves all 21
  unlock when their saved flags are present.

## Verification

- Unity domain reload/compile: current assemblies loaded with zero compiler errors.
- Runtime visual QA at 3840×2160:
  - answer screen shows a dark resting state, bright selected card, leading `>` marker, strong border,
    and a fully contained feedback card with clean separation from the third answer and footer;
  - completion screen shows the cream checkmark inside the green/gold seal, readable success copy,
    and the single **Prepare to Cut** action;
  - Wend arrival card begins at 140px, below both the compass and `Father's Mill · 14m` waypoint line.
- `Gameplay Foundation`: PASS — 63 stable wild nodes, all 21 persistent identification harvest
  gates, buyer/purse persistence, forage respawn, four cultivation recipes, and clock boundaries.
- `World Feedback`: PASS — four localized regions, six triggers, ambience/music routing, and the
  compass/waypoint-safe arrival title.
- Unity `Data Integrity Report`: 0 errors / 0 warnings.
- CLI lint: 0 errors / 0 warnings (1 existing waiver); CLI data integrity: 0 / 0.
- General Play Mode smoke: 2,640 live frames, active `meetAlmy`, 0 errors / 0 warnings.
- `git diff --check` is clean for this batch's source/docs.

## Unfinished / handoff

Do not create or run a player build unless Trevor explicitly requests one.
