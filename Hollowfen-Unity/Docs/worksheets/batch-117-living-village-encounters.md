# Batch 117 — Living village encounters

**Date:** 2026-07-19 · **Status:** implemented and focused-verifier green

## Goal

Make Hollowfen's restored places host observable relationships between villagers, with scenes that exist at a specific place, time, and—in one case—weather condition instead of following an NPC everywhere.

## Shipped

- Four paired scenes at the apothecary, forge, chapel garden, and Crooked Pintle.
- Exact schedule-slot dialogue gating so the scene cannot trigger when a participant is elsewhere.
- A deterministic wet-weather requirement for Bram and Edda's rain ledger.
- Eight durable participant memories, four +2 NPC bonds, relationship gains, and hope/knowledge consequences.
- One-shot completion flags that disperse both participants as soon as the moment ends.
- 20 index-matched 24 kHz mono voice performances, including four reference-conditioned Wren reads.
- A reusable schedule registry and spatial dialogue contract for future meals, walks, work sessions, and overheard scenes.

## Verification evidence

- `LivingVillageEncounterVerifier.RunAll()` — PASS for paired staging, spatial routing, weather gating, durable outcomes, and dispersal.
- `NPCScheduleVerifier.RunAll()` — PASS across all seven derived schedules and apothecary appointments.
- `RelationshipSystemVerifier.RunAll()` — PASS for quest-priority routing, round-trip persistence, favors, memories, bonds, and post-restoration roles.
- Data Integrity: `ERRORS=0 WARNINGS=0` across 145 dialogues.
- Project lint: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- Voice manifest: PASS for all 410 current dialogue lines; Unity wired 410 lines across 145 assets.
