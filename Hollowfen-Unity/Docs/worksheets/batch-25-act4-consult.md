# Batch 25 — Act IV: Consult the Village about the Lord's Offer

**Date:** 2026-07-12 · **Status:** VERIFIED + committed, tag `batch-25`

## Goal
Consume the `npc_consultations_unlocked` flag that `aldricOfferRead` (batch-24) set but nothing read (fable-review finding). The bible's Act IV scene 1 unlocks "Consult NPCs about Aldric"; this batch delivers that: three ending-relevant villagers give their view of the offer when consulted, so the player weighs the fork before `meetAldric`.

## What was built
- **Three consult dialogues** (in-voice, repeatable, no outcomes), each leaning toward a different ending:
  - **Bram** — the road/prosperity case, then its price ("a road that comes because a lord allows it can be closed the same way") — the Ending-B tension.
  - **Almy** — old-knowledge guardianship ("what the garden knows, the garden should keep … I would not sign") — Ending A/D.
  - **Hollin** — lineage/independence ("Sable's book is not his to hold … The Hollow was not saved by a lord. It was saved by you not asking one.") — Ending A/D.
- **NPC entries**: a consult entry prepended to each of Bram/Almy/Hollin, gated `requiresFlagId: npc_consultations_unlocked` — inert in Acts I–III (flag unset), the Act IV line once set.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Scope | 3 ending-relevant NPCs (not all) | Covers the B vs A/D fork with distinct voices; controls NPC-wiring risk |
| No quest / card / score | Pure optional dialogue | The bible frames "consult NPCs" as an unlocked activity, not a numbered quest |
| Entry placement | Top, gated on the flag | None of the three has a basket-sale entry to shadow (verified); ⚠️ future Act IV active-quest entries (e.g. Hollin in `wendSource`) must be placed ABOVE this consult entry |
| Repeatable | Yes (no outcomes) | Consulting again just replays the opinion — safe |

## Verification evidence
**Play-mode (bridge), green** — integrity 0/0 (24 quests, 68 dialogues); 0 new console errors:
- Routing: with `npc_consultations_unlocked` UNSET → none of the three shows the consult (existing behavior preserved); with it SET → `bram`/`almy`/`hollin` PickDialog return their respective consult dialogues.
- No quest/score/flag mutations (pure opinion dialogue) — repeatable-safe.

## Docs updated
- `systems/dialogue.md` — dialogue count → 68.
- `systems/npcs.md` — the consult-entry-at-top pattern (Act IV active-quest entries must sit above it).

## Unfinished / handoff
- The consult entries sit at the TOP of Bram/Almy/Hollin (gated on `npc_consultations_unlocked`). **When `wendSource` (Act IV scene 2) adds a Hollin active-quest entry, place it ABOVE Hollin's consult entry** or the consult will shadow it once the flag is set.
- Optional future depth: consult opinions for Marra/Pell/Calden/Edda/Joren too (this batch covered the B-vs-A/D fork with 3 distinct voices).

## Feedback to Trevor
- These consult lines lightly telegraph the endings (Bram → patronage tension, Almy/Hollin → keep-it-ours). If the four endings shift in your authoring, revisit these so the village's counsel still matches where the fork actually leads.
