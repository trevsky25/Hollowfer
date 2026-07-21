# Batch 110 — Living Restoration Foundation

**Date:** 2026-07-18 · **Status:** verified

## Goal
Turn Act II's `cottagesReopen` world swap from two disappearing plank meshes into the first playable Living Restoration project: a reusable, save-backed project model; controller-first restoration ledger; staged cottage worksites; and a dawn-driven restored/occupied world reveal. This is the foundation for later mill, bridge, forge, chapel-garden, Pintle, and village-square projects.

## Plan
- [x] Audit the canonical quest, Pell dialogue, flags, dawn rule, cottage anchors, save system, and runtime UI conventions.
- [x] Add data-authored restoration projects, stage reconciliation, events, and save-slot persistence.
- [x] Build the controller-first Restoration Ledger and site interaction.
- [x] Author the Wenmar and North Lane surveyed/work/restored/occupied presentations.
- [x] Preserve and verify Pell's 24-copper exchange and next-dawn quest reveal.
- [x] Add focused verifier coverage and complete Play-mode verification.
- [x] Update system docs and finalize this worksheet.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Cottage contribution owner | Keep the canonical 24-copper transaction in Pell's dialogue. | The bible requires Wren to provide shutters and then speak with Pell; moving payment to the board would erase authored dialogue and alter the story flow. |
| Board role in this slice | The project UI reads as Pell's working ledger during the quest and becomes the reusable restoration board record after completion. | It makes the cottage onboarding legible without contradicting the reward that unlocks later restoration projects. |
| Legacy compatibility | Reconcile saved restoration state upward from `shutters_funded`, `cottages_reopened_1`, `cottages_reopened_2`, and quest completion. | Existing saves must inherit the correct visible stage immediately and must never regress. |
| Persistent shape | One `RestorationSnapshot` with parallel project/stage/day arrays inside `SaveSlotMeta`. | It matches Hollowfen's JsonUtility conventions, remains portable, and old schema-1 saves deserialize safely with a null snapshot. |
| World dressing | Project-owned stage roots with authored repair primitives plus instances of the existing medieval prop prefabs. | High-detail buckets, firewood, crates, benches, brooms, pots, and ladder blend with the village while the importer leaves every third-party source asset untouched. |
| Hydration contract | Merge stored stage with legacy quest/flag truth without saving during load, then rebroadcast the final stage. | Loading a slot must restore the exact world roots and board state without a temporary derived state overwriting a later saved stage. |
| Visible labor | Derive three first-match NPC schedule rows from the existing quest/flags instead of saving crew state. | Joren and Pell work 07:00–18:30 and Bram visits 11:00–13:30 during `WorkUnderway`; the canonical clock and story facts remain the only persistence source. |
| Dawn event semantics | Publish `DayFlagScheduler.FlagPromoted` only when a real day rollover successfully creates the next flag. | Immediate presentation can react exactly once, while slot hydration quietly restores the final world and cannot replay an old cinematic. |
| Reveal ownership and framing | Wait for the shared presentation lane, then use `PropFocusCinematic` plus a non-interactive lower third and an authored cottage-front approach. | The rest transition clears first, nested input/HUD ownership stays leak-free, and trees or Wren's prior facing cannot replace the actual restored cottage with an empty-ground shot. |

## Verification evidence
- Unity script validation: `RestorationContentImporter`, `RestorationProjects`, and `RestorationVerifier` each report 0 errors / 0 warnings.
- Runtime console after final focused pass: 0 errors.
- `RestorationVerifier.RunAll()`: `LIVING RESTORATION — PASS: 2 staged sites + village board, monotonic project stages, legacy flag migration, normalized parallel arrays, and full save/load round-trip`.
- The focused verifier additionally asserts visible WorkUnderway switching, the authored cottage façade/fixed reveal approach, one queue from a real dawn promotion, no hydration replay, and rehydrated Occupied roots/board visibility.
- `NPCScheduleVerifier.RunAll()`: `NPC SCHEDULES — PASS: 5 derived routines ... three time-bounded cottage-restoration roles ...`.
- Schedule coverage proves three distinct noon work anchors, Bram's 13:30 departure, the remaining pair's 18:30 departure, and the restored-state block.
- `DataIntegrity.RunAllAsReport()`: `ERRORS=0 WARNINGS=0`; coverage includes 1 restoration project.
- `tools/agent/lint_hollowfen.py`: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- Active Restoration Ledger production UI audit: 0 critical / 0 advisory; controller default focus present.
- Visual QA: `Docs/screenshots/batch-110/restoration-ledger.png`, `north-lane-occupied.png`, `restoration-work-crew.png`, and `restoration-dawn-reveal.png`. The reveal was captured at full caption opacity in the 4K Game view before being downsampled for documentation.

## Docs updated
- `Docs/systems/restoration.md`
- `Docs/systems/save.md`
- `Docs/systems/ui-framework.md`
- `Docs/systems/quests.md`
- `Docs/tests.md`
- `CLAUDE.md`

## Unfinished / handoff
The first complete Living Restoration tutorial slice is now in place: durable stage state, two physical sites, readable ledger, visible daytime labor, overnight promotion, cinematic cottage reveal, and occupied-world payoff. The next chunk is content breadth rather than missing foundation: author project data, contributions, labor casts, and staged presentations for the bridge, mill, forge, garden, Pintle, and village square. Final destructive verification used only `/tmp/hollowfen-restoration-labor.ZCiq3z`; real journals were never opened or changed. The scene and working tree contain unrelated user work and remain preserved.

## Feedback to Trevor
The existing cottage quest is an excellent restoration tutorial spine: its missing piece was a persistent project vocabulary and physical stage presentation, not a narrative rewrite.
