# Batch 18 — Act III A: Sable, the Witch's Cottage, the Wend's True Course

**Date:** 2026-07-12 · **Status:** committed (tag `batch-18`)

## Goal
Act III scenes 1–3 per the bible: `hollinReveals` (the Sable reveal), `findWitchCottage` (the seedbook, T4 knowledge), `wendlightFound` (the dry riverbed proves Aldric's damage). The Deep Wood opens; the story turns from economics to suppressed history.

## What was built
- **Quests 16–18** chained from `caldenWarning`; bible rewards exact (Hollin +15/Almy +10 on the reveal; Knowledge +15 on the Wend truth); cards `hollin_inheritance`/`witch_cottage`/`wend_truth`.
- **Three new field-guide species (18–20)**: Moonring + Hollowheart (entry-only, from the seedbook) and **Wendlight** (forageable; pale tinted-variant prefab with subtle emission — "never spell-like"; 60% scale). Database extended to 20. Copy derived strictly from bible text; Latin = "Unrecorded" (Q4 in QUESTIONS.md, veto-able).
- **Hollin's inn→mill move**: FlagActivatedObject gained `_offFlagId` — the inn placement yields when `hollin_at_mill` sets (via caldenWarning's ScoreHooks); both placements share one NPCData.
- **Prop-anchored dialogue**: QuestInteractable gained `_playsDialogue` + `_discoversSpecies` — the seedbook table grants the book, unlocks the T4 trio (Knowledge +3 via discovery hooks), and plays the arrival scene; the timber mark plays Wren's riverbed monologue. Quest completion moves into the dialogue outcome.
- **Staging**: Witch's Cottage placeholder hut + Witchwell spring disc at (262, 35, 455) deep wood; Old Wend = 4 Wendlight nodes in a descending line west of the hamlet (sits LOWER than the village, reads as a real abandoned watercourse) ending at the Aldric timber mark (150, 31, 350); two new map POIs.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Dialogue choices in these scenes | NOT used | Bible scenes 1–3 are linear; choices debut with `theoCapitalOffer` (Act III B) — canon over feature-showcasing |
| T4 trio presentation | Latin "Unrecorded", edibility Unknown, no photos | Fictional species; bible gives names + one-liners only; parked as Q4 with options |
| Scene-2/3 dialogue anchoring | QuestInteractable `_playsDialogue` extension | Scenes anchor to PROPS (seedbook, timber mark), not NPCs; also gives Wren monologue capability for free |
| Cottage build | Primitive placeholder hut | Found buildings are LOD sub-parts (risky clones); props pass owns the real cottage |
| Witchwell spring "non-cultivable rare source" | Visual only, system deferred | Act IV/ending feature per the bible asset table |

## Verification evidence
- **7-step end-to-end bridge run, ALL PASSED, 0 console errors**: Act II fast-forward → Hollin relocates (inn OFF/mill ON) → Sable reveal (quest, flags act3_started/sable_named, card, +15/+10) → seedbook (item + 3 discoveries + wendlight_known + Knowledge +3 + dialogue opens) → arrival dialogue completes cottage quest (flags, card) → Wendlight harvest (count 1) → timber mark (dialogue, Knowledge +15, evidence flags, card, chain ends clean) → Act III repeat line picked.
- Integrity: 0 errors over 18 quests / 46 dialogues / 10 NPCs / 12 locations / 20 mushrooms — first pass after authoring.

## Docs updated
- systems/quests.md (QuestInteractable + FlagActivatedObject extensions, counts), dialogue.md (prop-anchored dialogues, count), npcs.md (dual-placement pattern), foraging.md (20 species, T4 trio), QUESTIONS.md (Q4).

## Unfinished / handoff
- Act III B (scenes 4–8) next: caldenReconcile (chapel garden REOPENS — the gate lock planks get an off-flag or inverted swap), eddaApprentice, **theoCapitalOffer (first choice-UI consumer)**, festivalHosted (4 festival dishes system), aldricLetter (act3_complete).
- Deferred: "environmental clues to stay on the old path" (scene 2 objective — waypoint suffices for v1), Witchwell as a rare source system, Liberty Cap/Fly Agaric/Deadly Galerina collection-gating behind the seedbook (compendium rule — no gating system exists yet; note for hardening).
- Q4 open: T4 Latin/photos presentation.

## Feedback to Trevor
- The bible's scene 2 unlock list quietly implies a collection-gating system ("cannot collect Liberty Cap/Fly Agaric/Deadly Galerina until spoken with Sable") that doesn't exist — I've noted it rather than built it; worth a queue decision when the psychoactive/deadly species get world nodes.
- Wendlight in the dry riverbed at dusk is going to be a genuinely pretty beat once the real model + a dawn-mist pass exist.
