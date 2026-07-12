# Batch 15 — Act II B: The Trader's Ledger + Brightspore at the Bedside

**Date:** 2026-07-11 · **Status:** committed (tag `batch-15`)

## Goal
Act II scenes 4–5 per the bible: `theoTrade` (Theo's wagon, first market sale, better prices than Marra) and `edsGrandfather` (Edda's ask → Almy's guidance → Brightspore forage → Marra's tonic → delivery → next-day recovery, the act's emotional center). First content batch built entirely on the Phase 2 rails.

## What was built
- **Quests 11–12** chained from `firstTax`; story cards `theo_trade`/`edda_grandfather` unlock on completion; scene-spec rewards exactly (Theo +8; Edda +15, Almy +8, Hope +10).
- **10 dialogue assets** (Theo: FirstSale/Waiting/RepeatSale/Repeat · Edda: Ask/WaitNight/Recovery/Repeat · Almy: Brightspore · Marra: Tonic) — bible lines verbatim, connective variants minimal and in voice. Theo pays 8c/item (vs Marra's 5c) with a permanent repeat-sale loop.
- **NPC_Theo + NPC_Edda** with condition tables; placeholder capsules (teal/dry-grass) staged in scene; SpeakerColors entries.
- **Brightspore goes live**: `MushroomWorld_Brightspore` prefab variant (Goldfoot copy, lacquered-amber material + slight emission), `Mushroom_16_Brightspore.WorldPrefab` wired, one node at the birch meadow past Old Wood edge. Discovery → `brightspore_known` + Knowledge+1 via ScoreHooks.
- **Small additive system extensions** (all pattern-consistent): DialogueData `_consumeForage`+count outcome; NPCDialogueEntry `requiresForage` condition; QuestInteractable `_requiresItemId` gate + `_setsFlagId`; NEW `FlagActivatedObject` (mirrors a GameScores flag onto a target's active state).
- **Staging via existing systems**: wagon arrives the dawn after tax is paid (`DayFlagScheduler: wenmar_tax_paid → theo_wagon_arrived` + FlagActivatedObject); Edda appears when `theo_trade_unlocked`; tonic delivery is an item-gated QuestInteractable setting `tonic_delivered`; recovery gated by scheduler pair `tonic_delivered → edda_check_due`. Two new map POIs (Theo's Wagon, Edda's Cottage).

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Wagon arrival | Dawn-after-tax via scheduler flags, then stays | Composes existing systems; bible's 5–7-day cycle deferred to the trade-system milestone |
| First sale gating | Any non-empty basket (dialogue references Goldfoot) | Entry conditions can't check species counts; species-gated sales deferred |
| Tonic consumption | New `_consumeForage` outcome + `requiresForage` entry condition | 20 lines total, reusable for recipes/gifts; prevents Marra's sell-loop eating the Brightspore (tonic entry ordered FIRST) |
| "Return next day" beat | `_setsFlagId` on the delivery interactable + scheduler pair | Keeps QuestInteractable declarative; no bespoke quest-step code |
| Edda placement | Outside the mill door near Almy (bible: "stood outside the mill at dusk") | Cottage interior out of scope; delivery point is the cottage door west of the mill |

## Verification evidence
- **End-to-end flow driven in Play mode via bridge (14 steps, ALL PASSED, 0 console errors)**: chain fast-forward → wagon absent → dawn brings wagon (flags+activation) → FirstSale picked with basket → sale outcomes (payment = basket×8c, flags, card, +8 Theo, chain to edsGrandfather) → Edda appears → Ask → Almy guidance → Brightspore harvest (count/known/Knowledge+1) → Marra tonic (picked via requiresForage, key item granted, ingredient consumed, flag) → delivery (item gate honored, flag set, prop hidden) → WaitNight variant → next dawn Recovery (quest complete, card, hope+10/edda+15/almy+8) → Theo repeat-sale loop. Saves backed up/restored around the run.
- Data integrity: 0 errors over 12 quests / 29 dialogues / 7 NPCs / 10 locations (ran clean on first pass after authoring).
- Placement verified with aerial survey screenshots; two staging bugs found and fixed: raycasts hitting RegionTrigger volumes (fix: `QueryTriggerInteraction.Ignore`) and the delivery point originally in the river.

## Docs updated
- `systems/quests.md` (FlagActivatedObject, QuestInteractable fields, counts), `systems/dialogue.md` (consume outcome, speakers, count), `systems/npcs.md` (requiresForage, Theo/Edda), `systems/time.md` (scheduler pairs).

## Unfinished / handoff
- Deferred (noted in TODOS): weekly wagon schedule flavor (5–7 day cycle), "market price notes in journal" (no journal feature), species-gated first sale, bible rewards "medicinal recipes unlock" + "Edda delivery tasks" (future systems), real wagon prop + Theo/Edda models (cast-models pass).
- Verification harness gotchas learned: bridge returns `success:false` during play-mode entry (tolerate in polls); `osascript` can hang when Unity is busy — `open -a Unity` is the robust activation; Physics.Raycast hits RegionTrigger volumes unless `QueryTriggerInteraction.Ignore`.

## Feedback to Trevor
- The two staging bugs were caught by *looking* (aerial screenshots), not by any check — visual verification (Phase 3) remains the biggest remaining gap for world-placement work.
- Q1 (tier display names) never actually blocked this batch — the bible's own lines carried it. It WILL matter for Act II C/Theo's expanded trade talk.
