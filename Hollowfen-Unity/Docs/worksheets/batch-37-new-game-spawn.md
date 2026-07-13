# Batch 37 — New-game spawn relocation

**Date:** 2026-07-12 · **Status:** DONE (verified) · tag `batch-37` (pending)
**Directive:** Trevor — "the spawn point for new game load is really far from the first mission; I want it
close to that mission and aligned with the position described in the storyline."

## Diagnosis
- New games spawn at the scene's authored `PlayerArmature` position (verified: `StartNewGame` doesn't stamp a
  transform; `PlayerSpawnRestorer` only overrides for saved games).
- Old spawn: **(397, 32, 34)** — far SE, outside the walled town on the east road. ~165u to the first-mission
  trigger.
- First mission = `Quest_Act1_01_Arrive`, completed at **`_TriggerZone_VillageSquare` (282, 39, 152)** (a
  `QuestTrigger` with `_completesQuestIfActive = Quest_Act1_01_Arrive`). Bram (269, 88.5) is the *next* step.

## Change
Moved `PlayerArmature` to **(306, 36.5, 72)**, yaw 298° (facing down the open lane into the village).
- On walkable Terrain (raycast-verified), clear of props/fences (OverlapSphere clean).
- **84u to the square** (was 165) · 41u to Bram — roughly half the old trek, and on a scenic open lane.
- Story-aligned: it's the "walked the east road into Hollowfen" arrival — an over-the-shoulder view of Wren on
  the cobbled lane with the misty village + inn ahead (matches the `homecoming.png` intro composition).
- Verified the town center near the square is dense with buildings (head-on approaches face a wall), so the
  open-lane spot reads best; the objective waypoint guides the player from there to the square.

## Verification
Play-mode POV screenshot from the new spawn: Wren on the lane, open misty village ahead (the T-pose is the
known Wren Mixamo rig bug — separate worklist item, not this batch). Only affects new games (Continue/Load use
the saved transform). Lint 0/0, integrity 0/0.

## Follow-up (Trevor's 2nd request — seamless intro→load→welcome→game)
Not in this batch — it's a cross-scene UX feature with a design fork (see chat). Planned as the next batch.
