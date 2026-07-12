# Batch 26 — Act IV scene 2: The Source of the Wend (`wendSource`)

**Date:** 2026-07-12 · **Status:** VERIFIED + fable-reviewed (findings fixed), tag `batch-26`

## Goal
TODOS item 12, scene 2. Wren follows the Wendlight upstream, past the Old Wend, to the real cause of the river's failure: an **upstream clear-cut** — stumps, drag-trails, a cold camp, the throttled spring — every stump bearing Aldric's seal-mark. At the living edge she finds **Aldermark**, a useful mushroom (Wren: "Of course something useful grows where harm stops") = evidence + leverage in one. Chains off `aldricOfferRead`. Knowledge +15.

## What was built
- **Aldermark species (#21)** — resolving Q7: mapped to the real **Hen of the Woods / Maitake (Grifola frondosa)**, a real fungus that colonizes the base of stressed and cut hardwoods, "pale and stubborn in the shade," prized culinary + medicinal (the leverage). Folk name "Aldermark" kept; real Latin + accurate ID/habitat/notes. No photo yet (→ wants list); registered in `MushroomFieldGuideDatabase` (21 entries).
- **Clear-cut location** — new `LocationData_ClearCut` (`clear_cut`, region `wend`) + a scene `LocationMarker` (discover radius 20), positioned upstream of the Old Wend at (130, 33.35, 372) — raycast-grounded.
- **`_ClearCutSite` scene prop** (execute_code): one GameObject carrying the `LocationMarker` AND a `QuestInteractable` (`_requiresActiveQuest: wendSource`, `_playsDialogue: Dialogue_Act4_WendSource`, `_discoversSpecies: [Aldermark]`, `_deactivateOnUse`).
- **`Dialogue_Act4_WendSource`** — Wren's clear-cut framing + the bible-verbatim Hollin exchange ("This is where the river was broken." / "Not broken. Loosed." / "That is worse." / "Yes.") + the Aldermark line + the seal-mark reveal. Completes the quest.
- **ScoreHooks**: `wendSource → [wend_source_visited, clearcut_evidence_found, aldermark_sample_collected]`. Knowledge +15 on the quest. StoryCard_25.
- Localization: quest name/objective + `loc.clear_cut.*`.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Aldermark real species (Q7) | Grifola frondosa (Hen of the Woods) | Real stump/tree-base colonizer, "pale and stubborn in the shade," genuinely valuable (leverage) — the "useful thing that grows where harm stops"; Trevor greenlit "a real stump-colonizer" |
| Clear-cut placement | (130, 33.35, 372), upstream of the Old Wend/timber-mark trail | Continues the Wendlight→timber-mark line the player already follows; raycast-grounded on terrain |
| Hollin at the clear-cut | Dialogue prop-anchored (batch-18 style); physical Hollin placement DEFERRED | Keeps the batch tractable; Hollin's voice is in the scene, his 3rd dual-placement joins the staging pass |
| Visual clear-cut dressing (stumps/camp) | DEFERRED to staging pass | The interaction + dialogue + LocationData carry the beat; stump/camp props are world-art |
| Aldermark world forage node | DEFERRED; sampling represented by the interaction (`_discoversSpecies` + `aldermark_sample_collected`) | No world model yet; a forage node + model is a later pass |

## Verification evidence
**Play-mode (bridge), all green** — integrity 0/0 (25 quests, 69 dialogues, 13 locations, 21 mushrooms); 0 new console errors:
- Chain `aldricOfferRead → wendSource`.
- `_ClearCutSite` wired: `_requiresActiveQuest=wendSource`, `_playsDialogue=Dialogue_Act4_WendSource`, `_discoversSpecies=[aldermark]`, `LocationMarker._data=clear_cut` — all confirmed by reflection in play mode.
- Aldermark discovery works (`MushroomDiscovery.IsDiscovered("aldermark")` after MarkDiscovered).
- Scene dialogue completes wendSource → active→null (chain tail), StoryCard_25 (`wend_source`), **Knowledge +15**, ScoreHooks set `wend_source_visited` + `clearcut_evidence_found` + `aldermark_sample_collected`.
- Saves backed up + restored; incidental MainMenu/TMP/Georgia re-serializations reverted; game-scene diff = the `_ClearCutSite` object only.

## FABLE REVIEW
**Verdict: SHIP WITH CHANGES.** Required-dialogue verbatim + in order; the seal-tie is canonical (bible line 2144: "seen burned into timber at the Wend's old bend"); Grifola frondosa judged a "defensible, actually strong" mapping (don't add alder — real oak associate). Fixes applied:
| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | MED | The scene's signature beat — Wren's hesitation before the knife (bible ~2352) — was dropped | Added a first-person transposition ("I did not cut them at first… before I trusted myself with the knife") before the harvest line |
| 2 | LOW-MED | "Sable's book" should be the established "Sable's seedbook name for…" formula; + the seedbook attribution is a small canon commitment | Reworded to the formula; parked the attribution as QUESTIONS Q8 (veto-able) |
| 7 | style | "started run down" misparses | → "had run down to a trickle" |
Other findings (no change): auto-harvest via `_discoversSpecies` is correct (a refuse-branch would fork endings for no payoff — the specimen is needed at the manor); null `_photo` renders fine (MushroomDetailScreen null-guards → dark hero panel); achievement fires via the StoryCard_25 unlock.

## Docs updated
- `systems/quests.md` — header (quests → 25; Act IV scene 2; ScoreHooks `wendSource` evidence flags).
- `systems/foraging.md` — 21 species; Aldermark = Grifola frondosa (real stump-colonizer), photo/world-model pending.
- `systems/dialogue.md` — dialogue count (69).
- `QUESTIONS.md` — Q7 answered (Aldermark→Grifola frondosa); Q8 opened (seedbook attribution, veto-able).

## Unfinished / handoff
- **Post-fix re-verification:** the two fixes are additive Wren-narration lines in an already-fully-verified dialogue (chain + prop + Aldermark discovery + completion + Knowledge +15 + evidence flags all play-verified pre-fix); completion is line-count-agnostic; integrity 0/0 confirms the 9-line parse. Null-photo render + achievement hook confirmed by code inspection.
- **Deferred to the Act IV staging pass** (growing): Hollin's 3rd placement (at the clear-cut), the visual clear-cut dressing (stump/camp/drag-trail props), and an Aldermark **world forage node + model** (currently sampling = the interaction's discovery). Also Aldermark needs a **field-guide photo** (Grifola frondosa, Wikimedia) — on the wants list.
- **Next: Act IV scene 3 `meetAldric`** — NPC_Aldric (first physical; needs a placeholder capsule + a manor location) at the negotiation, the final-choice trigger → then the **ending engine** (item 13). Both canon-critical — recommend Trevor's direct authorship. `wendSource._nextQuest` is null; scene 3 chains it.

## Feedback to Trevor
- The Act IV staging debt is now 4 items (Theo/festival/Voss/Hollin placements + the clear-cut/Aldermark world dressing). All the same shape (flag-gated NPC/prop placement) — one focused "Act IV staging + world-dressing pass" would make Acts III–IV read spatially before the pre-EA screenshots.
- `meetAldric` is the negotiation that resolves into the four endings. I've built everything up TO it; the meeting itself + the endings are where the game says what it means — those are yours to author. I can scaffold NPC_Aldric + the manor location whenever you want the mechanical shell.
