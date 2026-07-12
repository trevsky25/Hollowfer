# Batch 11 — Act II A: Quests 8–10, Cultivation, Day Scheduler, Tax Deadline

**Date:** 2026-06-11 (worksheet written retroactively 2026-07-11) · **Status:** verified 2026-06-11; committing now

## Goal
First slice of Act II per the bible: Scene 1 (The Vine-Tender's Lessons → `Quest_Act2_08_AlmyTeach`), Scene 2 (Joren's Forge → `Quest_Act2_09_ForgeKnife`), Scene 3 (Twelve Silver by Yule → `Quest_Act2_10_FirstTax`), plus the systems they need: cultivation grow beds, day-flag scheduler, and the tax deadline.

## What was built (from session memory + git status; predates the worksheet convention)
- **Quests 8–10** (`Assets/_Hollowfen/Data/Quests/Quest_Act2_*.asset`) continuing the Act I chain.
- **Cultivation**: `GrowBed.cs` / `GrowBeds.cs` (`Scripts/Cultivation/`) — Almy's garden grow beds with day-based maturation.
- **Time**: `DayFlagScheduler.cs` — day-elapsed event scheduling driving grow waits + the tax deadline.
- **TaxDeadline** + score integration (`Scripts/Quests/TaxDeadline.cs`, `ScoreHooks.cs` changes).
- **NPCs**: `NPC_Joren.asset`, `NPC_Voss.asset` + 11 new Act II dialogue assets (Almy lesson/wait, Joren commission/handoff/waiting/repeat, Voss demand/payment/waiting/repeat, Marra sell-basket).
- Supporting changes across DialogueData/DialogueScreen, NPCData, SaveCoordinator/SaveManager/SaveSlotMeta, Localization, map scripts, `Scene_Hollowfen`, and `Quest_Act1_07_MeetAlmy` (chain handoff into Act II).

## Verification evidence
- Play-verified end-to-end 2026-06-11 in the original session (quests 1–10 chain, clean-state reset harness). See memory `project_act1_build_progress`.
- Re-verified before this commit (2026-07-11, scripted smoke test over the MCP bridge): compile clean (`isCompiling=False, scriptCompilationFailed=False`); Play mode in `Scene_Hollowfen` ran 320+ frames with **0 console errors** (49 warnings = documented third-party pack noise); save hydration correct — `activeQuest=almyTeach`, 7 completed quests (all Act I), clock day 3 hour 8.7, 6 discovered locations, VillageHope 5; clean play-mode exit. Gotcha reconfirmed: backgrounded Unity needs `osascript activate` or App Nap suspends `EditorApplication.update` and frames never tick (PlayModeBackgroundTicker can't run if the app is napped).

## Docs updated
- None at the time (predates the system-docs convention) — Batch 13 backfills quests/dialogue/npcs/time/cultivation docs from code.

## Unfinished / handoff
- Act II scenes 4–8 are TODOS items (Theo/Edda, then Calden + completion state).
- Known session gotchas recorded in memory: editor focus throttling (PlayModeBackgroundTicker), CodeDom C#6 limits in `execute_code`, clean-state reset procedure.

## Feedback to Trevor
- This worksheet being retroactive is exactly the gap the convention closes: five systems shipped in Batch 11 with zero written trace outside chat memory.
