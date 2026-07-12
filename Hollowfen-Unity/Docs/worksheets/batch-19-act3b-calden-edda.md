# Batch 19 — Act III B (scenes 4–5): The Chapel Garden Opens · Edda Asks

**Date:** 2026-07-12 · **Status:** VERIFIED + committed (`4125fdf` authored during the stalled night shift; re-verified 2026-07-12 AM after the App-Nap fix, tag `batch-19`)

## Goal
TODOS item 9a. Author Act III scenes 4–5 per the bible: `caldenReconcile` (Father Calden reverses his Act II warning, admits the church buried women's/healers' land knowledge, gives Wren the chapel-garden key — a gate opening, "not forgiveness") and `eddaApprentice` (Edda formally asks to apprentice; Wren accepts, "but you start with cleaning the knife"). Both chain off `wendlightFound` (Act III A tail). This continues consuming the Act III B slice top-down on the night shift.

## Plan
- [ ] Create Quest_Act3_19_CaldenReconcile + Quest_Act3_20_EddaApprentice assets (chain: wendlightFound → 19 → 20)
- [ ] Author dialogues: Calden records-request (day-gate), Calden reconcile (verbatim), Calden post-reconcile repeat; Edda apprentice (verbatim), Edda post-accept repeat
- [ ] Wire NPCData entries (Calden: records→reconcile by `calden_records_read` flag; Edda: apprentice entry)
- [ ] DayFlagScheduler pair `calden_records_requested → calden_records_read` (mirror Act II `tonic_delivered → edda_check_due`)
- [ ] Chapel garden reopen: inverted swap — set `_offFlagId: calden_garden_unlocked` on `_ChapelGateLock`; reconcile dialogue sets that flag
- [ ] wendlightFound._nextQuest → caldenReconcile
- [ ] Refresh, compile clean, run_integrity
- [ ] Play-mode verification (bridge, EditorApplication.Step)
- [ ] Docs updated + worksheet finalized

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Relationship deltas | Use per-scene **Unlocks** block (Calden +15, Almy +5; Edda +20) | Scene-specific spec is more granular than the Act-III aggregate Relationship table (Calden +20 / Edda +25). Discrepancy noted for Trevor. |
| Chapel garden reopen | Inverted swap via `_offFlagId` on existing `_ChapelGateLock`, NOT clearing `chapel_garden_locked` | `GameScores` flags are additive-only (no unset); `_offFlagId` override-to-inactive already exists (Hollin inn/mill pattern) |
| Chapel-garden functional grow beds | DEFERRED to a cultivation pass | Bible "unlock beds" = the site opens; functional GrowBed cultivation wiring is its own system pass (mirrors batch-16 deferring the restoration board) |
| Apprentice-delivery system | DEFERRED; set `apprentice_system_unlocked` flag only | The delivery/observation task loop is a system; the scene's deliverable is the acceptance beat + flag (mirrors other deferred systems) |
| "Wait one day" records beat | DayFlagScheduler pair (canon Act II pattern) | Faithful to bible objective; reuses the existing `_TimeManager` scheduler rather than inventing a shortcut |
| Fable-review gate | SKIP for 19 | Content on existing rails: no new system, no save-schema change, `_offFlagId`/DayFlagScheduler/NPC-entry mechanics all pre-exist; dialogue is verbatim-from-bible transcription. Gate reserved for 9b (theoCapitalOffer, first branching authoring). |
| Calden not repositioned to the gate | Accept current spot ({234.5,32.8,309}, ~20u from gate); waypoint = chapel | Whole hamlet reads as one area; repositioning the group host is avoidable risk |

## Verification evidence
**FULLY VERIFIED 2026-07-12 AM** (after disabling App Nap on the editor bundle and reloading Unity). Compile clean (no CS errors; `Localization.cs` OK). `run_integrity.py` — **0 errors / 0 warnings**, coverage 20 quests / 51 dialogues / 10 NPCs / 12 locations / 20 mushrooms. `lint_hollowfen.py` — PASS.

**Play-mode flow (bridge-driven, `EditorApplication.Step()`, 0 new console errors):**
```
afterWend active=caldenReconcile              (wendlightFound → caldenReconcile auto-chain)
caldenPick noRead=Dialogue_Act3_Calden_Records
caldenPick requested=Dialogue_Act3_Calden_Records
caldenPick read=Dialogue_Act3_Calden_Reconcile  (flag-gated two-step routing)
afterReconcile active=eddaApprentice          (caldenReconcile → eddaApprentice auto-chain)
card chapel_garden=True
caldenDelta=15 almyDelta=5
keyHeld=True                                   (item.chapel_garden_key granted)
planksAfter=False   (was True)                 (chapel garden WORLD SWAP via calden_garden_unlocked)
eddaPick apprentice=Dialogue_Act3_Edda_Apprentice
afterApprentice active=null                    (chain ends cleanly)
card edda_apprentice=True
eddaDelta=20
caldenRepeat=Dialogue_Act3_Calden_Repeat       (post-completion routing)
eddaRepeat=Dialogue_Act3_Edda_Repeat
DayFlagScheduler pair live: calden_records_requested → calden_records_read (arrays len 5)
```
Saves backed up before the run and restored after (`scratchpad/saves-backup-batch19`).

### The stall (root cause, now FIXED) — kept for the record:
- All 11 touched assets imported with **zero import errors**: the 7 new dialogue/quest assets, both edited NPCs (Calden/Edda), edited Quest_Act3_18, and the scene reimported. Scene auto-reloaded from backup (13s deserialize) — no external-modification modal.
- Script compilation was **requested** (from the `Localization.cs` edit) at 01:05 PDT.
- `lint_hollowfen.py` — **PASS** (ERRORS=0 WARNINGS=0 WAIVED=1), run standalone (no bridge needed).

What is NOT confirmed (bridge never answered again after 01:05): script compile result (Localization.cs), `run_integrity.py`, and the play-mode flow.

**The stall (root cause for the morning):** `refresh_unity` kicked off import + a requested recompile. The editor then went idle at ~0.2% CPU with the live log frozen at 01:05 for 12+ minutes. Recovery attempted and FAILED: ~8 bridge retries, `open -a Unity`, `osascript activate` ×3 (Unity confirmed frontmost), `caffeinate -u` (display wake), CPU sampling. The `Unity.ILPP.Runner` compile helper spawned then exited; the editor never resumed its main loop / domain reload. Classic hard App-Nap / wedged main thread — the MCP bridge pumps commands on the main thread, so a stalled main thread = permanent "ping not answered". Did NOT force-kill/restart Unity (would risk editor state + is a Trevor-present action).

## Docs updated
- `systems/quests.md` — header count (quests →20), Act III B chain tail, DayFlagScheduler `calden_records_requested→calden_records_read` pair, chapel `_offFlagId` inverted-reopen note.
- `systems/dialogue.md` — header dialogue count (→51), Act III B dialogues + two-step NPC gating by a scheduler flag.
- `systems/npcs.md` — Calden records→reconcile flag-gated routing; Edda apprentice acceptance + post-arc repeats.
- `Localization.cs` — quest name/objective keys for caldenReconcile + eddaApprentice.
- TODOS.md — item #9 split into 9a–9d; 9a marked done.

## Unfinished / handoff
**Batch DONE + verified + tagged `batch-19`.** Next Act III B slice: **9b `theoCapitalOffer`** — the first choice-UI consumer (FABLE-REVIEW GATE). Note `eddaApprentice._nextQuest` is currently null; 9b must set it (wendlightFound→19→20→**21**).

**Known follow-ups seeded for later batches:** chapel-garden functional grow beds (cultivation pass), apprentice delivery/observation task loop (system pass), and the +15-vs-+20 Calden / +20-vs-+25 Edda relationship-number discrepancy between the bible's per-scene Unlocks block and its Act-III aggregate table (used the per-scene numbers — trivial to flip in the two quest assets if Trevor prefers the aggregate).

## Feedback to Trevor
- **This stall is the night shift's #1 reliability risk and it's worth hardening.** The editor App-Naps into a wedged state after `refresh_unity` triggers a recompile while backgrounded, and nothing I can do headlessly (activate, caffeinate -u, waits) un-wedges it — the MCP bridge can't help because it *needs* the same stalled main thread. Two concrete mitigations worth queuing: (1) have `caffeinate` include `-u` re-assertion or run the editor with App Nap disabled for the Unity.app bundle (`defaults write com.unity3d.UnityEditor5.x NSAppSleepDisabled -bool YES`) as a documented night-shift precondition; (2) teach the harness/tools a "compile churn" wait that polls the Editor log's mtime + the ILPP.Runner process rather than the bridge, so a shift can distinguish "still compiling" from "wedged" without guessing. I've written the diagnosis into this worksheet; consider promoting the `NSAppSleepDisabled` line into `night-shift.md` preconditions.
- The diagnosis path that actually worked was reading `~/Library/Logs/Unity/Editor-prev.log` (the *live* log here, not Editor.log) + `ps` CPU sampling. Might be worth a `tools/agent/unity_health.py` that prints {live-log mtime, editor %CPU, ILPP.Runner present?, bridge ping} in one shot for fast triage.
