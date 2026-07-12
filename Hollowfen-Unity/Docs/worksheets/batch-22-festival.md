# Batch 22 — Act III B scene 7: The First Festival in Three Years (`festivalHosted`)

**Date:** 2026-07-12 · **Status:** VERIFIED + committed, tag `batch-22`

## Goal
TODOS 9c. Author `festivalHosted` (Act III scene 7): the village hosts its first festival in three years — Marra/Bram/Edda/Pell coordinate, the square fills, Wren hides washing bowls. Chains off `theoCapitalOffer`. Village Hope +20; Marra +15 / Bram +10 / Pell +12; unlocks StoryCard_22 + the Act III final-letter trigger (`festival_hosted`).

## Scope decision (the big one)
The bible says "collect ingredients for four dishes … timed cooking/prep before sundown." **No cooking/recipe system exists, and one of the four dish species (lacewig = oyster) has no world prefab** (not forageable). A timed 4-dish cooking minigame is a whole system — out of scope for one content batch. **Shipped: the festival as the coordination SCENE** (the verbatim Marra/Bram/Pell/Edda beat) anchored to Marra, completing the quest with the canonical Hope + relationship outcomes. The gather/cook mechanic + physical square-staging (lanterns, festival props) are deferred + parked as QUESTIONS Q6.

## What was built
- **Quest 22** chained from `theoCapitalOffer` (`_nextQuest`), waypoint → Marra's kitchen, Hope +20 / Marra+15/Bram+10/Pell+12, unlocks StoryCard_22.
- **`Dialogue_Act3_Festival`** — the scene: Marra's "four dishes by sundown" rally, Bram (lantern hooks), Pell (more ink), then Wren slipping away and the Edda "hiding with dishes" aside (verbatim bible lines) + a soft Wren close. Outcomes: completes quest 22, sets `festival_prepared` + `festival_hosted`.
- **`Dialogue_Act3_Festival_Repeat`** — post-completion Marra line ("No one called it rustic. I am almost disappointed.").
- **NPC_Marra**: festival entry (activeQuest=22) prepended; the post-completion repeat placed BELOW her basket-sale entry (batch-21 lesson: a broad questCompleted repeat above the sale would starve the sell loop).

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Festival gameplay | Coordination dialogue scene, no forage/cook gate | No cooking system; lacewig not forageable; a timed minigame is its own system (QUESTIONS Q6) |
| Anchor | Talk to Marra (waypoint → her kitchen); the multi-NPC dialogue IS the square scene | Avoids scene GameObject surgery; faithful to the scene's dialogue + outcomes |
| Relationship deltas | Marra +15 / Bram +10 / Pell +12 (no Edda) | Bible Act III relationship table attributes exactly these to the festival |
| Physical staging (lanterns, square trigger) | Deferred | World staging is a separable pass; `festival_hosted` flag is the hook a later staging pass consumes |
| Fable review | Not gated | Content on existing rails (dialogue + quest deltas); the scope call is parked for Trevor, not invented canon |

## Verification evidence
**Play-mode (bridge, `EditorApplication.Step()`), all green** — integrity 0/0 (22 quests, 60 dialogues); compile clean; 0 new console errors:
- Chain `theoCapitalOffer → festivalHosted`; Marra routing: activeQuest=festivalHosted → `Dialogue_Act3_Festival`.
- Completing festivalHosted: active→null (chain ends), StoryCard_22 (`first_festival`) unlocked, **Village Hope +20**, Marra +15 / Bram +10 / Pell +12, flags `festival_prepared` + `festival_hosted`.
- Post-completion Marra routing: empty basket → `Festival_Repeat`; **full basket → `Marra_SellBasket` (sell loop intact)** — the batch-21 ordering lesson applied (festival repeat placed below her `almyTeach`-gated sale entry).
- Saves backed up + restored.

## Docs updated
- `systems/quests.md` — header (quests → 22; Act III B scenes 4–7).
- `systems/dialogue.md` — dialogue count → 60.
- `QUESTIONS.md` — Q6 (festival cook/gather scope).
- `Localization.cs` — festivalHosted name/objective.

## Unfinished / handoff
- **Festival systems + staging pass (TODO):** (a) the gather/cook mechanic if Trevor picks Q6 option (b)/(c); (b) physical square staging — lanterns + festival dressing driven by the `festival_hosted` flag (a `FlagActivatedObject` hook), and a proper square trigger; (c) `lacewig` needs a `_worldPrefab` if it should be forageable.
- Next queue item: **9d `aldricLetter` → `act3_complete`** (Voss delivers the letter; closes Act III B). `festivalHosted._nextQuest` is null; 9d will chain it.

## Feedback to Trevor
- The festival is the third "coordinate several NPCs in one dialogue" scene where a single DialogueData carrying 4 speakers stands in for a staged square. That reads fine at this fidelity, but the pre-EA polish pass should decide whether these communal beats want real spatial staging (NPCs actually gathered, lanterns lit) or stay as narrated scenes — worth a deliberate call rather than defaulting.
