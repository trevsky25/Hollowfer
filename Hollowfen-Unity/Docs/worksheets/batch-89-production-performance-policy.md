# Batch 89 — Production performance policy

**Date:** 2026-07-16 · **Status:** verified; awaiting commit/tag

## Goal
Prepare Hollowfen for a fullscreen standalone production build with one explicit 60fps/native-quality
contract. Remove the uncapped frame loop, activate the gameplay camera's intended art-direction stack, and
keep display-mode changes from silently replacing the pacing policy.

## Plan
- [x] Audit active PC quality, URP renderer, camera, volume, and graphics controls.
- [x] Add refresh-aware 60fps production frame pacing.
- [x] Enable high-quality main-camera presentation without touching bespoke preview cameras.
- [x] Reapply pacing after fullscreen/resolution/quality changes.
- [x] Play-mode verification and focused performance-policy assertions.
- [x] Docs updated + worksheet written.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Frame target | Locked 60fps | Stable pacing and predictable GPU budget matter more than an uncapped number. |
| Synchronization | Hardware VSync when refresh is 60/120/180/240Hz; Unity 60fps cap otherwise | Unity ignores `targetFrameRate` while VSync is active; divisor-aware selection keeps clean displays smooth without turning 144Hz into 72fps. |
| Resolution policy | Native render scale, no automatic dynamic resolution | Preserve the game's authored image; add adaptive resolution only if a measured release build proves it necessary. |
| Anti-aliasing | SMAA High on the tagged main camera | Strong foliage/geometry edge cleanup without TAA history ghosting during camera motion. |
| Post pipeline | Active for SMAA/stop-NaN/dithering; scene volumes excluded (corrected batch-91) | Enabling every existing volume also enabled the asset pack's warm demo grade. A zero volume mask keeps the quality passes and restores Hollowfen's established palette. |
| Shadow/reflection escalation | Keep current PC URP budget | It is already high-quality: 2K four-cascade soft shadows, Forward+, SSAO, and reflection blending. Raising those blindly conflicts with the 60fps requirement. |

## Verification evidence

- Static audit: PC quality = native render scale, full mipmaps, anisotropic filtering, LOD bias 1.25 (rebalanced July 19 after a 46,762-LOD vendor-world audit); PC URP =
  HDR, Forward+, SRP Batcher, 2K four-cascade high soft shadows, full-resolution SSAO, reflection blending.
- C# validation: zero errors; initial compile completed with no new compile errors. Removed the only new
  Unity 6 deprecation warning (`GetInstanceID`) before runtime verification.
- Refresh mapping assertion: `59.94→1`, `60→1`, `75→0`, `119.88→2`, `120→2`, `144→0`, `165→0`,
  `180→3`, `240→4` (`0` means the strict software 60fps fallback).
- Live 120Hz gameplay assertion after 750 frames: target=60, VSync=2, quality=PC, HDR=true,
  dynamic-resolution=false, occlusion=true, post=true, SMAA/High, dithering=true, stopNaN=true, shadows=true.
- Original visual proof exposed a warm world-palette regression after hands-on testing. Batch-91 supersedes
  it with `Assets/Screenshots/batch91-neutral-color-performance.png` and isolates volume grading from SMAA.
- Policy introduced no runtime exceptions. The focused boot exposed 45 pre-existing vendor-world collider
  errors (one Terrain/MeshCollider incompatibility plus 44 negative-scale BoxColliders); those are parked in
  the build-cleanup queue rather than hidden or folded into this graphics/frame-pacing change.
- Main-menu smoke: 242 frames, 0 pre-play errors, 0 in-play errors, clean exit.
- Gotcha lint: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- Data integrity: `ERRORS=0 WARNINGS=0` across 26 quests, 75 dialogues, 11 NPCs, 14 locations,
  21 mushrooms, 30 story cards, four endings, and ten village requests.

## Docs updated

- `Docs/systems/settings.md` — production pacing/camera contract and display-change ownership.
- `Docs/steam-constraints.md` — concrete 60fps enforcement policy.

## Unfinished / handoff

A real production-player frame-time capture still follows the build; an Editor number is not treated as
standalone performance evidence. The pre-existing vendor collider startup errors now have a named build-cleanup
owner and require a collision-regression pass rather than a blind bulk transform rewrite.

## Feedback to Trevor

The PC renderer was already sensibly high-end. The camera needed explicit anti-aliasing and pacing, but the
scene's existing vendor demo volume was not a Hollowfen-authored final grade. Batch-91 separates those concerns:
the quality passes remain, while the world palette comes from Hollowfen's lighting system.
