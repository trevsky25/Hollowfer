# Batch 24 — Act IV scene 1: The Lord's Offer (`aldricOfferRead`) — Act IV opens

**Date:** 2026-07-12 · **Status:** VERIFIED + fable-reviewed (findings fixed), tag `batch-24` — **ACT IV STARTED**

## Goal
TODOS item 12, scene 1 — the first Act IV beat. Wren reads Lord Aldric's sealed letter (delivered by Voss to close Act III) at the mill table by candlelight. The letter is *seductive* before it's sinister: patronage, tax relief, protection — then the hook, "all cultivation records, wild-harvest routes, medicinal preparations, and trade rights shall be held in trust by my estate." Wren: "Held in trust. Owned, in prettier clothes." Sets `act4_started`; unlocks NPC consultation about the offer. Chains off `aldricLetter`.

## What was built
- **Quest 24** `aldricOfferRead` (act 4), chained from `aldricLetter`; waypoint → the mill; unlocks StoryCard_24 (`aldric_offer`).
- **`Dialogue_Act4_Aldric_Offer`** — a solo reading beat: Wren's framing narration + the letter's verbatim lines (speaker `Aldric's letter`) + the "held in trust / owned in prettier clothes" turn + a close ("began a list of who I needed to ask"). Completes quest 24.
- **`_MillLetter` scene prop** (created via `execute_code`, the batch-18 `QuestInteractable._playsDialogue` pattern): an invisible trigger at the mill (232.9, 33.2, 317.8), layer 8, `_requiresActiveQuest: aldricOfferRead`, `_requiresItemId: item.aldric_letter` (must hold the letter to read it), plays the reading dialogue, `_deactivateOnUse`.
- **ScoreHooks**: `aldricOfferRead → [act4_started, aldric_offer_read, aldric_monopoly_clause_seen, npc_consultations_unlocked]`.
- **SpeakerColors**: added `Aldric` + `Aldric's letter` (#6b3a3a, oxblood — the red wax) → cleared 3 integrity warnings.
- Localization: quest name/objective + `item.aldric_letter.name`.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Reading trigger | A scene prop (`_MillLetter`) via the batch-18 QuestInteractable pattern, gated on holding `item.aldric_letter` | The bible frames it as reading the letter "at the mill table" — a prop-anchored solo beat, not an NPC talk; gating on the item makes "read the letter you were given" literal |
| Prop creation | `execute_code` (create GO + set private serialized fields by reflection + save scene) | Precise; the bridge's SO-ref assignment is fiddly. First scene-GameObject addition of the session. |
| Letter contents | Verbatim bible lines; contents ARE revealed here (unlike Act III's sealed letter) | This scene IS the reveal per the bible; the monopoly clause is the thematic hook |
| act4_started + offer flags | Via ScoreHooks on completion | Consistent with `aldricLetter`/`caldenWarning`; nothing consumes `act4_started` yet (scene 2 `wendSource` will) |
| Aldric's letter color | #6b3a3a oxblood (matches the red wax) | New speaker; distinct, formal, ties to the seal |

## Verification evidence
**Play-mode (bridge), all green** — integrity 0/0/0-warn (24 quests, 65 dialogues); 0 new console errors:
- Chain `aldricLetter → aldricOfferRead`.
- **`_MillLetter` prop wired correctly**: `_requiresActiveQuest=aldricOfferRead`, `_playsDialogue=Dialogue_Act4_Aldric_Offer`, `_requiresItemId=item.aldric_letter` (all confirmed by reflection in play mode).
- Reading the dialogue completes quest 24 → active→null (Act IV chain tail), StoryCard_24 (`aldric_offer`) unlocked, and ScoreHooks set **`act4_started` + `aldric_offer_read` + `aldric_monopoly_clause_seen` + `npc_consultations_unlocked`**.
- Saves backed up + restored; incidental TMP/Georgia SDF re-serializations reverted; scene diff = the `_MillLetter` GameObject only.

## FABLE REVIEW
**Verdict: SHIP WITH CHANGES.** Letter lines verbatim + Aldric holds "persuasive, not a monster"; `act4_started` trigger correct. Findings fixed:
| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | HIGH | Line 1 "broke the rest of the seal" contradicted BOTH Act III branches (OpenNow already broke it; Wait left it intact) | Reworded branch-neutral: "…finally spread the letter open on the table." |
| 2 | LOW | Line 4 dropped the grandmother's "and opinions in her eyebrows" (the sharper half) | Restored |
| 3 | — | "my father's daughter" for bible "Tobin's daughter" | Confirmed correct (Tobin IS Wren's father) — no change |
| 4 | note | `npc_consultations_unlocked` has no consumer yet | Logged in handoff (next batch delivers consult content) |
Reviewer also confirmed: don't dramatize journal-writing (bible pointedly keeps Tobin's journal *closed* in this scene).

## Docs updated
- `systems/quests.md` — header (quests → 24; **Act IV started**); ScoreHooks `aldricOfferRead → act4_started`.
- `systems/dialogue.md` — dialogue count → 65; SpeakerColors gained Aldric.
- `Localization.cs` — aldricOfferRead name/objective + `item.aldric_letter.name`.

## Unfinished / handoff
- **`npc_consultations_unlocked` has no consumer yet** (fable finding 4) — Act IV should add "consult NPCs about the offer" content (villagers commenting on Aldric) that gates on this flag, before `wendSource`/`meetAldric`, or it's a dangling promise. Same accepted pattern as `act3_complete`.
- **`_MillLetter` prop position** (232.9, 33.2, 317.8) is at the mill marker — an invisible trigger; a polish pass could place it precisely on a visible table prop (the mill interior layout wasn't surveyed).
- **Next Act IV**: scene 2 `wendSource` (the upstream clear-cut + Aldermark species + Hollin) and scene 3 `meetAldric` (NPC_Aldric at the manor — first physical Aldric; new location) → then the **ending engine** (item 13, FABLE-GATED, the 4-ending fork). `aldricOfferRead._nextQuest` is null; scene 2 chains it.

## Feedback to Trevor
- Act IV is now open, but the endgame from here is the game's thematic core — the four endings and the `meetAldric` negotiation encode what Hollowfen *means*. Those are worth your direct authorship/approval rather than autonomous drafting; I'd recommend the next autonomous work stay on scene 2 (`wendSource`, evidence-gathering — mechanical, lower-stakes) and pause the ending engine for you.
- The `_MillLetter` prop is the first scene-GameObject I've added this session (via `execute_code` + reflection + SaveScene). It's a clean, repeatable recipe — worth promoting into a `tools/agent/` helper if more prop-anchored beats are coming (Act IV has several).
