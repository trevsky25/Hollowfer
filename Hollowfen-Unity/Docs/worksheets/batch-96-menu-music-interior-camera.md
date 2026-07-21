# Batch 96 — Menu music and interior camera stability

**Date:** 2026-07-17 · **Status:** complete

## Goal
Replace the title screen's noise-dominant ambience with a clear musical identity, and stop the third-person camera from crossing or slicing building walls while Wren enters and turns inside interiors, especially the mill.

## Plan
- [x] Trace the title audio graph and inspect the active score/ambience sources.
- [x] Reproduce the mill interior from several camera headings and inspect its wall colliders and LODs.
- [x] Add a title-screen score mix and lower the procedural Old Wood texture.
- [x] Make the shared gameplay follow camera safe for tight interiors.
- [x] Compile, inspect live audio routing, and repeat the mill camera sweep.
- [x] Run integrity checks and finalize evidence.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Title music | Reuse the licensed `Misty Forest.wav` score at 0.42 with a 3.5-second fade. | The title scene previously had no music source at all; this gives it a melodic identity without introducing an unlicensed asset or a second score language. |
| Menu ambience | Keep Old Wood wind/birds, capped at 0.16. | Environmental detail remains audible, but it now supports the score instead of reading as static with occasional chirps. |
| Interior camera | Obstacle radius 0.35 m, near clip 0.10 m, Perlin amplitude 0.08. | The old 0.50 positional noise was applied after collision avoidance and could move a correctly solved camera pose through architecture; the wider buffer and shorter near plane protect tight corners. |
| Scope | Fix `PlayerFollowCamera.prefab`, not only the mill. | The report applies to multiple buildings, and every normal gameplay interior uses this shared camera. |

## Diagnosis evidence

- `Scene_MainMenu` contained only `AmbienceManager`; there was no `MusicManager` or imported music source in the live title scene.
- The mill is `World/Small Village/Buildings/BasicBuilding3_3`. Its inside/outside wall LOD groups are enabled, wall materials are opaque URP/Lit, and its structure has active Default-layer box colliders.
- The follow camera's obstacle filter already includes those Default-layer colliders, but its collision radius was 0.15 m while the lens near plane was 0.20 m.
- More importantly, `CinemachineBasicMultiChannelPerlin.AmplitudeGain` was 0.50. Cinemachine noise is evaluated after the body/collision stage, so the final pose could be moved well beyond the collision clearance and visibly cross a wall as the camera breathed or turned.

## Verification evidence

- Unity refreshed and compiled with no C# errors. Both `Scene_MainMenu` and `Scene_Hollowfen` validate with zero missing scripts, broken prefabs, or other scene issues; the final console read returned zero errors.
- Live title audio: `Misty Forest` is playing as a 193.48-second, 48 kHz stereo loop through the Music group at 0.420. Old Wood day/night loops are playing through Master at 0.113 / 0.022, confirming music is the dominant layer rather than synthesized wind.
- Live camera contract: collision enabled, 0.35 m radius, 0.10 m near clip, and 0.08 Perlin amplitude on the shared `PlayerFollowCamera` instance.
- Mill west-wall stress pose: camera `(229.68, 34.47, 317.64)`, measured wall clearance `0.349 m` against `BasicBuilding3WallsOutside/Cube (8)`.
- Mill north-wall stress pose: camera `(232.70, 34.58, 321.10)`, measured wall clearance `0.350 m` against `BasicBuilding3WallsOutside/Cube (2)`.
- Targeted Play-mode captures of both stress poses and a centered interior view showed the wall shell and ceiling intact; only authored door/window openings reveal the exterior.
- `lint_hollowfen.py`: `ERRORS=0 WARNINGS=0` (one existing waiver).
- `run_integrity.py`: `ERRORS=0 WARNINGS=0` across 26 quests, 75 dialogues, 11 NPCs, 14 locations, 21 mushrooms, 30 story cards, two story moments, 31 character profiles, four endings, and ten village requests.
- `git diff --check`: PASS for the batch files.

## Files changed

- `Assets/_Hollowfen/Scenes/Scene_MainMenu.unity`
- `Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerFollowCamera.prefab`
- `Docs/systems/audio.md`
- `Docs/systems/input.md`

## Unfinished / handoff

Dedicated regional compositions remain an optional future music pass; the existing adaptive engine already accepts them. This batch deliberately changes only the normal gameplay follow camera, not authored dialogue, prop-focus, forage, map, or journal cameras.

## Feedback to Trevor

The title now begins with an actual score rather than asking the procedural woodland layer to behave like music. Indoors, the collision solver now owns the final camera pose: subtle movement remains, but it is too small to push the lens back through the mill walls after collision has been resolved.
