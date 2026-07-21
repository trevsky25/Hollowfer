# Batch 69 — Field Guide Model Lighting & Controls

**Date:** 2026-07-15 · **Status:** verified

## Goal
Make every delivered mushroom model read clearly against the dark Field Guide surface, then let players directly inspect the large detail specimen with pointer and gamepad orbit/zoom controls.

## Plan
- [x] Replace the dim two-light preview with an isolated four-light studio rig.
- [x] Balance dark and pale source albedos with manifest-driven preview exposure.
- [x] Keep gameplay materials untouched and release temporary preview materials with viewport recycling.
- [x] Add detail-only pointer and gamepad orbit/zoom/reset controls.
- [x] Complete visual, runtime, integrity, lint, and smoke verification.
- [x] Finalize docs and restore clean editor state.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Lighting | Four short-range point lights: key, fill, rim, and underside bounce | The rigs share one Unity layer, so local lights avoid cross-lighting neighboring preview stages while filling the cap and gill shadows. |
| Exposure | Per-model `journalExposure` in `MushroomModelManifest.json` | Source albedo means range from about 0.17 to 0.93. One global boost either leaves Wood Ear/Brightspore muddy or clips Destroying Angel. |
| Material isolation | Runtime clone only for each active preview source material | Explicitly enables URP emission-as-ambient fill without mutating the shared gameplay material; viewport recycling destroys the clone. |
| Interaction scope | Large detail `RawImage` only | Index cards remain cheap, auto-rotating, and click-safe. The detail leaf accepts drag/wheel; right stick/triggers mirror existing mushroom inspect conventions. |

## Verification evidence
- All 20 modeled entries rendered through the same detail presenter during the exposure audit. The darkest non-transparent average luminance improved from 0.125 to 0.236 while the brightest pale specimen remained controlled at 0.710.
- Pointer verification reports `raycast=True`; an 80×−30 drag changed yaw by 22.4° and pitch by 8.4°, while one wheel step changed zoom from 1.0 to 0.878. A separate inspected state reached zoom 0.657 with a 12° pitch and remained inside the frame.
- `Docs/screenshots/batch-69/brightspore-balanced-1280x800.png` shows the balanced default view; `brightspore-orbit-zoom-1280x800.png` shows the user-controlled view.
- Unity compile and post-smoke console: 0 errors. Runtime component scan: 1,130 objects, 0 missing components.
- Data Integrity: `ERRORS=0 WARNINGS=0`, including all 20 manifest exposure values matching their species assets.
- Project lint: `ERRORS=0 WARNINGS=0 WAIVED=1`; scoped `git diff --check` passed.
- `tools/agent/smoke_play.py`: PASS at 5,048 frames with 0 new console errors.
- Temporary discovery hydration was runtime-only; the legacy discovery override remains absent. Editor restored to `Scene_MainMenu`.

## Docs updated
- `Docs/systems/menu-pages.md`
- `Docs/systems/input.md`
- `Docs/tests.md`
- `Docs/review/performance.md`

## Unfinished / handoff
No content gap was introduced. Aldermark remains the pre-existing 20/21 coverage exception because no canon Maitake model has been delivered.
