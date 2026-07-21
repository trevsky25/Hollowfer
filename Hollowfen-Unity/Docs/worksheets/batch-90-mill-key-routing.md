# Batch 90 — Mill key insertion routing

**Date:** 2026-07-16 · **Status:** verified; awaiting commit/tag

## Goal

Keep the mill-door cinematography intact while making the physical key action read correctly: approach the
keyhole, place the tip on the opening, insert along the door depth, turn around the shaft, and remain attached
to the door for the reveal.

## Root cause

`KeyLockedDoor` instantiated the model directly at `KeyholeAnchor.rotation`. That anchor shares the door's
orientation, so the key model's +X shaft landed on the door's right axis instead of its depth axis. The prefab's
authored -90° root rotation was also replaced by the spawn rotation. The key therefore appeared immediately in
the lock, lay across the door face, and rotated around an implausible route.

## Implementation

- Added a fixed `_MillKey_InLockPivot` whose local +X points into the door and local +Z stays upright.
- Instantiated the model below that pivot with `worldPositionStays=false`, preserving the prefab pose and scale.
- Measured mesh/skinned-mesh bounds in pivot space to find the real positive shaft extent. The pre-insert pose
  puts that rendered tip on `KeyholeAnchor` instead of relying on another model-specific offset.
- Added a short eased acquisition move with a restrained vertical arc, followed by a straight insertion stage.
- Turned the pivot around its local shaft axis. Only after the turn is complete is the pivot reparented to the
  moving door leaf, retaining the existing camera hold and interior reveal.
- Added serialized timing/blocking controls with conservative scene defaults; no scene or prefab rewrite was
  required, and the original camera parameters are unchanged.

## Verification evidence

- Unity compile: zero errors.
- Geometry assertion against the live mill door and current key prefab:
  - rendered positive tip reach: `0.09697m`;
  - shaft-to-inward-door-normal dot product: `1.00000`;
  - pre-insert tip/keyhole error: `0.0000000m`;
  - seated tip depth beyond the keyhole plane: `0.08498m`.
- Default-speed play smoke: focus session completed, temporary pivot cleaned up, door collider disabled, and the
  `DemoDoor` leaf reached its open pose (`Y 156.57° → 56.57°`).
- Focused post-start console check: zero warnings and zero errors from the interaction.
- Edit-mode geometry check left `Scene_Hollowfen` clean; existing cinematic camera framing was not modified.

## Files changed

- `Assets/_Hollowfen/Scripts/Quests/KeyLockedDoor.cs`
- `Docs/systems/quests.md`
- `Docs/worksheets/batch-90-mill-key-routing.md`
