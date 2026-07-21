# Batch 83 — NPC village rhythm

**Date:** 2026-07-16 · **Status:** DONE in working tree (compile, focused Play Mode verifier, visual QA, and regression smoke green)
**Directive:** Keep building outward from the day/night cycle by making Hollowfen's villagers respond to time and story progress, beginning with Theo's long-pending wagon-to-inn staging.

## Design

- A routine is derived from the same clock, quest, and flag state already persisted by the game. NPC placement has no new save payload or migration path.
- Ordered slots allow story overrides to outrank ordinary daily behavior without duplicating routing policy.
- Overnight windows wrap naturally across midnight. The first pass uses a shared 18:30–07:00 Crooked Pintle gathering.
- Runtime moves wait while an interaction is open or the player is near either endpoint, preventing visible teleports while keeping the implementation independent of future navigation AI.
- Schedule hosts and actors are separate: a hidden actor cannot accidentally disable the component responsible for bringing it back.

## Authored routines

- **Theo:** absent before his wagon-arrival flag; wagon all day before Edda's apprenticeship; wagon by day and Pintle by evening afterward; active Capital-offer quest forces the Pintle at any hour. The Capital quest waypoint now points to the Pintle.
- **Joren:** forge until `forgeKnife`, then Pintle evenings and forge days.
- **Bram:** village well until `meetAlmy`, then Pintle evenings and well days.
- **Pell:** village well until `cottagesReopen`, then Pintle evenings and well days.

## Implementation

- Added `NPCSchedule` with serialized conditions, wraparound hour matching, event-driven refreshes, a low-frequency unscaled poll, deferred moves, and read-only verifier/debug state.
- Added an idempotent `NPCScheduleImporter` that creates `_NPCSchedules/{Actors,Anchors}`, authors the four schedules, removes Theo from the wagon art's visibility group, and corrects the Capital waypoint.
- Added `NPCScheduleVerifier` for derived-state, milestone, boundary, route, and no-visible-pop coverage.
- Kept `_TheoWagon` responsible only for wagon art. The actor can now move independently without changing the existing arrival presentation.

## Verification

- C# compilation: zero errors.
- `NPCScheduleVerifier.RunAll`: PASS — four unique routines; milestone-gated evenings; Theo wagon/Pintle story override; corrected Capital waypoint; wrapping night windows; and near-player relocation deferral.
- Focused verifier runtime console: zero errors (`0|48|6`; warnings/logs are known scene/vendor noise).
- Visual QA: day and night Crooked Pintle captures under `Docs/screenshots/batch-83/`; the four placeholder villagers gather after dark and return to their established daytime areas.
- General smoke: PASS after at least 240 frames with zero new console errors.
- Save hygiene: the focused verifier snapshots and restores runtime state and does not call save; the precautionary save backup was restored.
- Batch-scope `git diff --check`: clean after Unity scene whitespace normalization.

## Performance shape

- Four always-active schedule hosts, each polling at 0.35-second unscaled intervals; no per-frame schedule work.
- Slot evaluation is a short first-match scan with no LINQ or routine allocations.
- Relocation is a transform update, not pathfinding. Future navigation/animation can consume the same resolved destination without replacing schedule policy.
- No new meshes, materials, textures, lights, colliders, or save records.

## Primary files

- `Assets/_Hollowfen/Scripts/NPCs/NPCSchedule.cs`
- `Assets/_Hollowfen/Scripts/Editor/{NPCScheduleImporter,NPCScheduleVerifier}.cs`
- `Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity`
- `Assets/_Hollowfen/Data/Quests/Quest_Act3_21_TheoCapitalOffer.asset`
- `Docs/systems/npcs.md`, `Docs/tests.md`, and `Docs/screenshots/batch-83/`
