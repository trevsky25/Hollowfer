# Batch 21 — Act III B scene 6: Theo's Capital (`theoCapitalOffer`) — the choice-UI debut

**Date:** 2026-07-12 · **Status:** VERIFIED + fable-reviewed (all findings fixed) — tag `batch-21`

## Goal
TODOS 9b. Author `theoCapitalOffer` (Act III scene 6, quest 21): Theo makes a Capital partnership offer at the empty inn; Wren may ask optional questions (cost / Hollowfen / timing) and leaves without deciding. This is the **first on-disk consumer of the `_choices` branching UI** (added batch-17, never used in shipped content). Chains off `eddaApprentice`. Unlocks the Capital (Ending C) candidate path via `theo_capital_offer_received`; Theo +10; StoryCard_21.

## Key constraint discovered
`DataIntegrity.HasDialogueCycle` flags ANY cycle through `_nextDialog`/`_choices` as an **Error** ("a cycle traps the player at timeScale 0"). So a question→hub→question loop is illegal. Design must be an acyclic DAG.

## Design (acyclic choice graph + NPC-level re-entry)
- **Intro** (`Theo_Capital`) — full verbatim core offer; `_setFlagIds:[theo_offer_heard]` (fires before choices); `_choices` → [Cost, Hollowfen, Timing, Leave].
- **Cost / Hollowfen / Timing** — terminal branches (lines only, no next/choices → close). Player re-talks to Theo to ask another.
- **Hub** (`Theo_Capital_Hub`) — short reprompt + the same 4 choices; NPC routes here on re-talk once `theo_offer_heard` is set (keeps the full pitch from replaying). Shares the 4 branch leaves with Intro → still a DAG (no node points back up).
- **Leave** (`Theo_Capital_Leave`) — Wren defers; outcomes: `_completeQuest` 21, `_setFlagIds:[theo_capital_offer_received]`. (Theo +10 + StoryCard_21 on the quest.)
- **Repeat** (`Theo_Capital_Repeat`) — post-completion idle line.
- NPC_Theo entries prepended: A) activeQuest=21 + flag theo_offer_heard → Hub; B) activeQuest=21 → Intro; C) questCompleted=21 → Repeat.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Choice graph shape | Acyclic; re-ask via re-talking to Theo (flag-routed Hub) | Cycle checker errors on any loop; re-entry at the NPC layer is legal + reads as "mulling the offer" |
| Core canon lines | All in the Intro (always seen), choices add optional depth | Guarantees the emotional core ("For you." / "the offer is clean") lands even if the player asks nothing |
| Night ("empty inn at night") | Narrative framing only; NOT hard-gated on `TimeManager.IsNight` | Would be the first IsNight consumer + complicates verification; deferred, noted |
| Theo at the inn | **DEFERRED to a staging pass** (fable finding 2). For now: waypoint + objective point at Theo's WAGON (`theos_wagon`, where he actually is); the inn dual-placement (`theo_at_inn` flag + inn-Theo + wagon `_offFlagId`) is a TODO. No authored line references the inn, so text stays coherent. | Scene GameObject creation is separable staging; a waypoint promising an empty inn is a player-trap — avoided by pointing at the real actor |
| Ending-C unlock | Set canon flag `theo_capital_offer_received`; ending engine (item 13) will read it + Theo relationship | No ending engine yet; the "accept" resolves in Act IV per the bible |

## FABLE REVIEW
**Verdict: SHIP WITH CHANGES.** Authored voice PASSED (no canon contradiction; verbatim core faithful; accept/decline correctly deferred to Act IV). Findings addressed before commit:
| # | Sev | Finding | Fix applied |
|---|---|---|---|
| 1 | REQUIRED | Post-completion Repeat entry sat above the basket-sale entry → permanently killed Theo's sell loop (first-match wins) | Moved the Repeat entry below the completed+basket RepeatSale, above the idle |
| 2 | REQUIRED | Waypoint/objective promised an inn with no Theo in it | Retargeted waypoint → `theos_wagon`; objective → "find him at his wagon"; removed the dead `theo_at_inn` ScoreHooks; staging deferred + documented |
| 3 | rec | Repeat "roads stay open till spring" contradicted Timing/Ending-C (spring = roads OPEN) | Reworded: "The offer stands till spring. Past that, I make no promises." |
| 4 | rec | One tap on Leave locked out the 3 question branches for the rest of the game | Repeat is now a post-completion hub re-offering the 3 questions (no-outcome terminals) + a "Not now." close |
| 5 | minor | "For you." merged with the cost line, losing the pivot beat | Split into its own DialogueLine |
| 6 | minor | Leave said "tonight" but night isn't gated | Reworded to "the only honest answer there is." |
Also: "Wren internal conflict journal entry" bible unlock has no mechanism → parked as QUESTIONS Q5 (carried by StoryCard_21 body for now).

## Verification evidence
**Play-mode (bridge, `EditorApplication.Step()`), pre-fix pass — all green:**
- Chain `eddaApprentice → theoCapitalOffer`; NPC routing: Intro (offer active, not heard) → Hub (after `theo_offer_heard`) → Repeat (completed). Intro exposes 4 choices with correct branch targets (Cost/Hollowfen/Timing/Leave).
- Leave completes quest 21: active→null (chain ends), StoryCard_21 unlocked, Theo +10, flag `theo_capital_offer_received`.
- **Real DialogueScreen UI:** opened the on-disk Intro, advanced the 5 lines → `IsChoosing=true, 4 choices`; `SelectChoice(0)` navigated to the Cost branch. First on-disk `_choices` consumer works end-to-end.
- Integrity 0/0 (21 quests, 58 dialogues); acyclic cycle check passed; 0 new console errors in play.
- **Post-fix re-verification (all green):** integrity 0/0 (21 quests, 58 dialogues); compile clean; play-mode — post-completion Theo with EMPTY basket → `Theo_Capital_Repeat`, with FULL basket → `Dialogue_Act2_Theo_RepeatSale` (**sell loop restored**, finding 1); Repeat hub re-offers 4 choices (Cost/Hollowfen/Timing/close, finding 4).
Saves backed up + restored (`scratchpad/saves-backup-batch21`).

## Docs updated
- `systems/dialogue.md` — first on-disk `_choices` consumer; the acyclic-DAG + NPC-re-entry pattern (cycle checker errors on loops).
- `systems/quests.md` — quest 21, chain tail; ScoreHooks unchanged.
- `systems/npcs.md` — Theo offer/hub/repeat routing + the "repeat entry must sit below the sale entry" ordering gotcha.

## Unfinished / handoff
- **Theo inn staging pass (TODO):** dual-placement at the Crooked Pintle (`theo_at_inn` flag set on `eddaApprentice` complete + inn-Theo group + wagon `_offFlagId`), then repoint the waypoint/objective back to the inn. Optional: bind the "night" framing to `TimeManager.IsNight` (first consumer).
- Next queue item: 9c `festivalHosted`. `theoCapitalOffer._nextQuest` is null; 9c will chain it.

## Feedback to Trevor
- The `HasDialogueCycle` integrity rule forbids ANY dialogue loop, which forced the re-talk-to-ask-more design. The fable reviewer's suggestion is sound: relax it to allow cycles that pass through a *choice* node (the player always holds an exit pill, so it can't trap at timeScale 0). That would enable a true one-sitting branching conversation — worth a small hardening task before more choice-heavy scenes (Act IV).
