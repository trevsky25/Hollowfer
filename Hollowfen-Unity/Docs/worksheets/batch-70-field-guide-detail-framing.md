# Batch 70 — Field Guide Detail Framing

**Date:** 2026-07-15 · **Status:** verified

## Goal
Make the mushroom study visibly larger when a player opens a Field Guide entry, without changing index-card framing or clipping tall/wide models during automatic yaw rotation.

## Implementation
- Detail presenters now fit a rotation-safe horizontal radius and camera-projected vertical extent instead of a full 3D bounding sphere.
- The fit uses a 2% camera-plane margin. Index presenters retain the existing sphere framing.
- The mount is reset before measuring a newly selected species, so framing cannot inherit the previous user's pitch/yaw.

## Verification evidence
- Brightspore opens approximately 25% larger than the previous sphere fit at the same 1280×800 target.
- All 20 modeled entries were sampled at 12 yaw angles each. Worst projected fill was Lacewig at `0.980`, leaving the intended 2% margin with no clipping.
- Visual evidence: `Docs/screenshots/batch-70/brightspore-larger-detail-1280x800.png`.
- Unity compile and console: 0 errors.
- Data Integrity: `ERRORS=0 WARNINGS=0`.
- Project lint: `ERRORS=0 WARNINGS=0 WAIVED=1`; scoped `git diff --check` passed.
- `tools/agent/smoke_play.py`: PASS after 241 stepped frames with 0 new console errors.
- Temporary discovery hydration remained runtime-only; editor restored to `Scene_MainMenu`.

## Docs updated
- `Docs/systems/menu-pages.md`
- `Docs/tests.md`
