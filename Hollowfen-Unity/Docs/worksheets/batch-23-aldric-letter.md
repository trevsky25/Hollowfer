# Batch 23 — Act III B scene 8: A Sealed Letter (`aldricLetter`) — Act III closes

**Date:** 2026-07-12 · **Status:** VERIFIED + committed, tag `batch-23` — **ACT III B COMPLETE**

## Goal
TODOS 9d — the Act III finale. Voss comes between tax days, not to collect but to deliver Lord Aldric's personal sealed letter (dark red wax, the mark from the Wend's old bend). Wren chooses to break the seal now or wait until evening; either way the letter is received and **Act III completes** (`act3_complete`, which will gate Act IV). Chains off `festivalHosted`. Voss +5 ("more than a tax function"), StoryCard_23, grants `item.aldric_letter`.

## What was built
- **Quest 23** chained from `festivalHosted`; waypoint → village well (Voss's actual area — staging honest, see below); Voss +5; unlocks StoryCard_23.
- **`Dialogue_Act3_Voss_Letter`** (intro) — Voss's verbatim delivery ("I am not here to collect." … "Do you want to?" / "No.") + Wren's seal-recognition beat; grants `item.aldric_letter`; ends with 2 choices → **second choice-UI use**: [Break the seal now → OpenNow] / [Set it aside until evening → Wait].
- **OpenNow / Wait** — terminal branches, each completes quest 23 (contents NOT revealed — that's Act IV's `aldricOfferRead`). Acyclic.
- **`Dialogue_Act3_Voss_Repeat`** — post-completion line.
- **ScoreHooks**: `aldricLetter → [aldric_letter_received, voss_humanized, act3_complete]` — the idiomatic quest→flag route (nothing consumes `act3_complete` yet; Act IV will).
- **NPC_Voss**: aldricLetter entry (active → intro) + post-completion repeat prepended above his Act II FirstTax entries (no basket-sale to shadow).

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| open-now vs wait | A 2-branch choice (both → complete + `act3_complete`) | Bible objective literally says "choose to open it now or wait"; a faithful, low-stakes second choice-UI use |
| Letter contents | NOT shown here | Bible reveals them in Act IV `aldricOfferRead`; showing them would spoil the act break |
| `act3_complete` wiring | Via ScoreHooks on quest completion | Matches every other quest→flag mapping; the reviewer-endorsed route; nothing downstream yet so full latitude |
| Voss mill-doorway staging | DEFERRED; waypoint → village well (where Voss actually stands) | Same as Theo/festival — scene GameObject staging is a separable pass; a waypoint must not point at an empty spot (batch-21 fable lesson) |
| Fable review | Not gated | Verbatim content + a trivial 2-way choice + a marker flag; no new system/architecture (Act IV + ending engine ARE fable-gated per TODOS) |

## Verification evidence
**Full real-dialogue end-to-end (bridge, `EditorApplication.Step()`), all green** — integrity 0/0 (23 quests, 64 dialogues); 0 new console errors:
- Chain `festivalHosted → aldricLetter`; Voss routing → `Dialogue_Act3_Voss_Letter`, 2 choices.
- Drove the REAL DialogueScreen: advanced the intro → choices present + **`item.aldric_letter` granted**; `SelectChoice(0)` → OpenNow branch; advanced it → quest completes, screen closes.
- On completion: aldricLetter completed, **active→null (Act III chain ends)**, StoryCard_23 (`sealed_letter`) unlocked, Voss +5, and ScoreHooks set **`act3_complete` + `aldric_letter_received` + `voss_humanized`**.
- Post-completion Voss routing → `Voss_Repeat`.
- Saves backed up + restored.

## Docs updated
- `systems/quests.md` — header (quests → 23; **Act III complete**; ScoreHooks `aldricLetter → act3_complete`).
- `systems/dialogue.md` — dialogue count → 64; second choice-UI consumer.
- `Localization.cs` — aldricLetter name/objective.

## Unfinished / handoff
- **`act3_complete` is set but nothing consumes it yet** — Act IV's entry quest (`aldricOfferRead`, TODOS item 12) will gate on it. The letter's actual contents are deliberately unrevealed until then.
- **Voss mill-doorway staging** joins the deferred staging pass (Theo, festival). Waypoint currently → village well (Voss's real spot).
- **ACT III B COMPLETE** (quests 19–23). Next queue: infra items #10 (review personas) / #11 (visual regression), or **Act IV** (item 12, `aldricOfferRead` — FABLE-REVIEW gated) + the **ending engine** (item 13, FABLE-REVIEW gated).

## Feedback to Trevor
- Act III is done end-to-end and every quest is bridge-verified, but **three staging debts have accumulated** (Theo→inn, festival→square+lanterns, Voss→mill door). They're all the same shape (an NPC needs a scene-gated second placement) and would batch efficiently into one "Act III staging pass." Worth doing before the pre-EA screenshot/trailer work so the Act III beats read spatially, not just narratively.
