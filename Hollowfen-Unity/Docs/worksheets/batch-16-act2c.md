# Batch 16 — Act II C + Decision Inbox Cleared: ACT II COMPLETE

**Date:** 2026-07-11 · **Status:** committed (tag `batch-16`)

## Goal
Close Act II (scenes 6–8 per the bible): `meetHollin` (recognition before explanation), `cottagesReopen` (visible recovery — the world-state payoff), `caldenWarning` (principled opposition; the chapel garden locks until Act III). Plus: implement Trevor's blanket approval of all three QUESTIONS.md recommendations.

## What was built
- **Decision inbox cleared (Q1–Q3)**: tier display names locked as canon-pending (T1 Basket Common · T2 Knifework · T3 Yard-Grown · T4 Deepwood; `tier.tN.name` reserved in the LUT; conventions.md updated — Trevor may veto before first render). SaveSlotScreen copy fixed ("Journal 1–4", no false autosave claim). EA floor recorded as Acts I–II in steam-constraints.md.
- **Quests 13–15** chained from `edsGrandfather`; story cards on completion; bible rewards exact (hope +12 + Pell +8 on cottages; Calden −5 on the warning; `act2_complete` via ScoreHooks).
- **13 dialogue assets** (Hollin ×2, Bram ×2, Pell ×5, Joren hinges, Marra bread, Calden ×2) — bible lines verbatim, connective lines minimal/in-voice.
- **3 new NPCs**: Hollin (inn window, appears on `edda_grandfather_recovering`), **Elder Pell** (always-on at the well — bible-canon NPC missing from the router cast list, now added + integrity checker's canon set), Calden (mill door, appears on `calden_visiting`).
- **World-state swaps live**: two cottage board-groups using FlagActivatedObject's INVERTED mode (planks vanish on `cottages_reopened_1/2`); Wenmar boards drop the moment Pell's funding clears (24c spend), the north-lane cottage opens the dawn after (scheduler pair); chapel gate lock planks APPEAR on `chapel_garden_locked`.
- **Chapel gate**: QuestInteractable completes the act — sets the lock flag, unlocks `caldens_doubt` card, applies Calden −5, chain ends cleanly (no active quest until Act III).

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Q1–Q3 | Implemented per recommendations (Trevor: "implement all your recommendations") | Tier names canon-pending with veto window; journal copy; EA floor recorded |
| Pell staging | Always present at the well | He's the village recorder — no arrival beat in the bible |
| Funding mechanics | Pell dialogue spends 24c (2 silver), gated by `requiresCoinsCopper` with a no-coin variant | Composes existing entry conditions; "provide coin or materials" objective |
| Almy optional beat (scene 8) | Skipped | The bible specifies her SILENCE ("old enough to have roots") — a dialogue would contradict canon |
| Chapel siting | Gate at (214, 38, 314), the chapel's east edge | The chapel itself is a proxy-cube placeholder on a hill; all near probes hit 53–61m colliders |

## Verification evidence
- **9-step end-to-end bridge run, ALL PASSED, 0 console errors**: chain fast-forward → Hollin visible + Bram notice → first meeting (card, flags, chain) → Joren/Marra optional lines picked correctly → Pell funding (boards1 active→inactive, exactly 24c spent) → waiting variant → dawn (boards2 down, ledger line completes: hope +12, pell +8, Calden appears) → warning (flag + repeat variant next) → gate (item chain: locked flag, planks appear, quest complete, card, calden −5, `act2_complete`, active quest = none).
- Integrity: 0 errors over 15 quests / 42 dialogues / 10 NPCs on first pass after authoring.
- **New harness failure mode solved permanently**: Unity backgrounded → play mode frozen at frame 1 even after `open -a` (isApplicationActive=False). Fix: `EditorApplication.Step()` drives frames synchronously regardless of focus (June discovery, now built into the verify pattern AND smoke_play.py as an automatic fallback).

## Docs updated
- systems/quests.md, dialogue.md, npcs.md, time.md (counts, cast, scheduler pairs, Act II complete)
- conventions.md (tier names), steam-constraints.md (EA floor), QUESTIONS.md (3 answered), router CLAUDE.md (Elder Pell), tools/agent/smoke_play.py (Step() fallback)

## Unfinished / handoff
- Deferred: "restoration project board" system, chimney smoke visuals, Deep Wood rumor chain content (Act III), Hollin trust score mechanics (Act III), real building association for the board groups (placeholder plank clusters at plausible spots — cast-models/props pass owns real staging).
- Act III is gated on the **dialogue choice UI** (TODOS item: `theoCapitalOffer` needs it) — that's the next batch.

## Feedback to Trevor
- Act II is fully playable start to finish. The act-break moment (Calden at the mill, the gate locking) lands well even with placeholder capsules — worth a manual play-through for taste when you have an evening.
- The decision-inbox loop closed cleanly on its first cycle: three questions asked → one chat message from you → all three applied with paper trail. That's the pattern working as designed.
