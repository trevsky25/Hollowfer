# Batch 94 — Recoverable final choice

**Date:** 2026-07-17 · **Status:** implementation complete; verification pending
**Production-audit finding:** quitting or crashing after the `meetAldric` outcome autosave but before choosing an ending left no active quest and no NPC dialogue route back to the fork.

## Goal

Make the final choice recoverable entirely from state that is already persisted, without a save-schema migration or a second source of truth.

## Implementation

- Added `NPCDialogueEntry.blockedByFlagId`, the inverse of `requiresFlagId`.
- Added an Aldric recovery row: completed `meetAldric` + `final_choice_available`, blocked by `game_complete`, routes back to the canonical meeting and four ending choices.
- Kept the active-quest row first so ordinary first-match dialogue priority is unchanged.
- Extended data integrity to reject removal/miswiring of the recovery route, impossible require+block flag rows, and false unconditional-shadow findings for forage/blocked gates.
- Extended `EndingEngineVerifier` to write and reload the exact vulnerable snapshot, prove Aldric resolves the fork, then prove the prompt disappears after completion.

## Safety

The recovery row reuses the canonical meeting rather than duplicating ending data. Replayed outcomes are safe: completed quests and story-card unlocks are idempotent, while this node has no coin/item/score grant to duplicate.

## Verification

- [ ] Unity compile: zero errors.
- [ ] Data integrity: zero errors/warnings.
- [ ] `EndingEngineVerifier.RunAll`: post-quest disk recovery and all four ending commits pass.
- [ ] Main-menu and gameplay smoke.
- [ ] Tester save directory restored byte-for-byte from the production-audit backup.

### Test in Play Mode

1. Reach Aldric's four-choice ending fork, then quit before selecting an ending.
2. Reload the same slot and return to Aldric.
3. Expected: the Talk prompt is present and replaying the meeting returns to the same eligible/locked four-choice fork.
4. Choose an ending and return/remain in Hollowfen after credits.
5. Expected: the slot is marked complete, exactly one ending owns the save, and Aldric no longer offers the recovery conversation.
