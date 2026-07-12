# Batch 27 — Act IV scene 3: The Meeting (`meetAldric`) — MECHANICAL SCAFFOLD ONLY

**Date:** 2026-07-12 · **Status:** IN PROGRESS · tag `batch-27` (pending)

## Goal
TODOS item 12, scene 3 — the *mechanical shell* for the final-choice trigger. Build NPC_Aldric
(first physical antagonist, placeholder capsule + flag-gated placement), a `manor` LocationData +
scene marker, and the bible-verbatim negotiation dialogue, chained off `wendSource`. Wire it to set
`aldric_meeting_started` + `final_choice_available` on completion, then **STOP**.

**Explicit non-goal (hand back to Trevor):** the four endings + the ending-selection logic (TODOS
item 13, the ending engine) are NOT authored here — canon-critical + FABLE-GATED creative core.
The dialogue ends after "…what you think prosperous is allowed to cost." with no `_choices`.
`meetAldric._nextQuest` stays null.

## What is being built
- **NPC_Aldric** (`aldric`) — NPCData, one entry: active-quest `meetAldric` → `Dialogue_Act4_MeetAldric`.
  Placeholder capsule via the flag-gated dual-placement pattern (`_Aldric` FAO host → `AldricGroup`).
- **`manor` LocationData** (`LocationData_Manor`, region `manor`) + always-active `Marker_Manor`
  (LocationMarker, discover radius 20) at **(365, 34.22, 190)** — clean terrain east of the village.
- **`Dialogue_Act4_MeetAldric`** — the 8 bible-required lines (incl. "You concede that?" /
  "I concede arithmetic." beyond the narrative passage). `_completeQuest` → meetAldric.
- **Quest_Act4_26_MeetAldric** (`meetAldric`, act 4, order 26) — waypoint = manor,
  unlock StoryCard_26 on complete, deltas 0 (endings own the scoring), `_nextQuest` null.
- **StoryCard_26** — REUSED the pre-existing `StoryCard_26_MeetingAldric.asset`
  (`meeting_aldric`, guid `73cbf90e…`, already in `StoryCardDatabase`, real image, `_unlockAt: 25`).
  It was scaffolded earlier alongside the ending cards 27–30. I first created a duplicate and
  deleted it; `Quest_26._unlockStoryCardOnComplete` points at the existing card's guid.
- **ScoreHooks** `meetAldric → [aldric_meeting_started, final_choice_available]`.
- Wire `wendSource._nextQuest → meetAldric`.
- Localization: `quest.meetAldric.*`, `loc.manor.*`, `story.meeting_aldric.*`; MapScreen region case `manor`.

## Gating decision
Aldric's `AldricGroup` capsule appears on `wend_source_visited` (set exactly when scene 2 completes =
meetAldric becomes the active quest). No `_offFlagId` (the ending sequence supersedes the meeting;
leaving him present post-meeting is harmless for the scaffold). Marker_Manor is discoverable on
proximity regardless (marker on the always-active host, per convention).

## New asset GUIDs
| Asset | guid |
|---|---|
| Quest_Act4_26_MeetAldric | be1d1f6805f5410da490e633a51a491f |
| Dialogue_Act4_MeetAldric | d5931fa2b0b74aa2b72fd1ba8a9da871 |
| NPC_Aldric | e135d5e1bc474585841a7e20fa756acf |
| LocationData_Manor | b83b82ae920e46a3858e580928fbd9fc |
| M_NPC_Aldric_Placeholder.mat | (auto-guid) |
| StoryCard (reused, not new) | 73cbf90e7e4274a968931398f711dc5c |

## FABLE REVIEW
**Verdict: SHIP WITH CHANGES** (fable-model reviewer, per Model Tiering / night-shift.md). All 8
bible-required lines present, in order, verbatim; ending at the fork with `_choices: []` confirmed
as the correct scaffold boundary; no narration line editorializes toward an ending; no register/
anachronism/oath violations. Fixes applied:
| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | MED | "the room went very quiet" was folded in BEFORE Wren's final line; bible lands the silence AFTER it — and that's why the scaffold felt cut off, not held | Trimmed the Aldermark line; added a closing narration line "The room went very quiet around us." → the dialogue now ends on the bible's held breath, and the future choice UI attaches after the silence |
| 2 | MED | "He listened when she spoke of the Wend" was dropped — line 5 opened cold on the woodcutters with no antecedent | Prepended "He listened while I spoke of the Wend." to line 5 |
| 3 | LOW | dialect mix: "oak-panelled" (UK) vs bible "oak-paneled" | → "oak-paneled" (repo-wide dialect drift noted for a later sweep, not this batch) |
| 4 | LOW | "the way a man regrets" looser than bible "as one might regret" | → "as a man regrets" |
| 5 | LOW | confirm the card unlocks on this quest path | Already play-verified: `IsStoryCardUnlocked("meeting_aldric")==True` after completing meetAldric |
Non-findings (kept): bare "the Aldermark" (matches batch-26 usage); "The answer surprised him" is
carried by Aldric's "You concede that?"; the dropped "careful man / ledgers" beat is covered by the
StoryCard `_wrenNote` + the manor's map description. Final line count: 12, integrity re-confirmed 0/0.

## Verification evidence
**Play-mode (bridge), Scene_Hollowfen, all green** — integrity `ERRORS=0 WARNINGS=0`
(26 quests, 70 dialogues, 11 NPCs, 14 locations, 21 mushrooms); 0 new console errors (only the
pre-existing missing-script / BoxCollider warnings):
- Asset refs resolve: meetAldric → card `meeting_aldric`, waypoint `manor`, `_nextQuest` null;
  dialogue `act4.meet.aldric` completeQuest=meetAldric; NPC `aldric`; manor region `manor`.
- **Chain:** `StartQuest(wendSource)` → `CompleteQuest("wendSource")` → ActiveQuest becomes
  `meetAldric` **and** `wend_source_visited=True` (ScoreHooks).
- **FAO:** `AldricGroup.activeSelf` flipped True on the flag (activeInHierarchy True).
- **NPC:** `NPC_Aldric.PickDialog()` → `act4.meet.aldric` while meetAldric active.
- **Completion:** `CompleteQuest("meetAldric")` → ActiveQuest→null (endings deferred ✓),
  `aldric_meeting_started=True`, `final_choice_available=True`, StoryCard `meeting_aldric` unlocked.
- Scene objects persisted on disk (grep `Marker_Manor`/`_Aldric`/`AldricGroup`/`NPC_Aldric`;
  scene +287 lines). Saves backed up + restored; only my files in `git status` (no SDF/TMP/MainMenu
  re-serialization this run). One benign incidental: 3 `BasicBuilding3_3` prefab instances had
  imperceptible `m_LocalRotation` float drift from the scene save (static buildings, not my content).

## Docs updated
- `systems/quests.md` — header (26 quests; Act IV scenes 1–3; meetAldric scaffold + STOP point;
  `meetAldric` ScoreHooks flags).
- `systems/dialogue.md` — count 70; `Dialogue_Act4_MeetAldric` = first Aldric-spoken scene.
- `systems/npcs.md` — cast 11; Aldric added (dual-placement `_Aldric` FAO → `AldricGroup`, gated on
  `wend_source_visited`, single active-quest entry, silent after the meeting).
- `systems/map.md` — 14 POIs (incl. `manor`); `manor` region + the `LocalizeRegion` case reminder.

## Handoff — the STOP point
Everything up TO the final choice is built and verified. `meetAldric` completes into
`final_choice_available` and then the quest chain ends (ActiveQuest null). The next batch is
**TODOS item 13, the ending engine** — 4 score thresholds → unlock ONLY the chosen ending card
(27–30; fix their shared `_unlockAt: 26`), letterboxed ending sequences + credits, `game_complete`.
That is canon-critical + FABLE-GATED and is **Trevor's authorship** — do not auto-build it.
Deferred (staging pass, unchanged): Aldric capsule → Meshy model; manor building/props dressing
(the manor is a LocationData + marker + capsule only — no building geometry yet).
