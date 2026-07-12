# Batch 19 — Act III B (scenes 4–5): The Chapel Garden Opens · Edda Asks

**Date:** 2026-07-12 · **Status:** AUTHORED — VERIFICATION BLOCKED (Unity editor stalled mid-refresh; committed untagged, needs play-verify before `batch-19` tag)

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
**Partial — blocked by a Unity editor stall.** What IS confirmed (from `~/Library/Logs/Unity/Editor-prev.log`, the live editor log):
- All 11 touched assets imported with **zero import errors**: the 7 new dialogue/quest assets, both edited NPCs (Calden/Edda), edited Quest_Act3_18, and the scene reimported. Scene auto-reloaded from backup (13s deserialize) — no external-modification modal.
- Script compilation was **requested** (from the `Localization.cs` edit) at 01:05 PDT.
- `lint_hollowfen.py` — **PASS** (ERRORS=0 WARNINGS=0 WAIVED=1), run standalone (no bridge needed).

What is NOT confirmed (bridge never answered again after 01:05): script compile result (Localization.cs), `run_integrity.py`, and the play-mode flow.

**The stall (root cause for the morning):** `refresh_unity` kicked off import + a requested recompile. The editor then went idle at ~0.2% CPU with the live log frozen at 01:05 for 12+ minutes. Recovery attempted and FAILED: ~8 bridge retries, `open -a Unity`, `osascript activate` ×3 (Unity confirmed frontmost), `caffeinate -u` (display wake), CPU sampling. The `Unity.ILPP.Runner` compile helper spawned then exited; the editor never resumed its main loop / domain reload. Classic hard App-Nap / wedged main thread — the MCP bridge pumps commands on the main thread, so a stalled main thread = permanent "ping not answered". Did NOT force-kill/restart Unity (would risk editor state + is a Trevor-present action).

## Docs updated
**Deferred to the verify pass** — intentionally NOT touching `Docs/systems/*.md` yet, because their 7-line headers assert play-verification status and this batch is unverified. Once someone reloads Unity and the play-verify below is green, update: `quests.md` (quests 19–20, chain tail now → caldenReconcile, DayFlagScheduler pair, chapel `_offFlagId` inverted-reopen), `dialogue.md` (dialogue count +5, two-step NPC dialogue gating by a scheduler flag), `npcs.md` (Calden records→reconcile flag gate; apprentice acceptance). Worksheet + TODOS split + `Localization.cs` keys ARE done.

## Unfinished / handoff
**RESUME (morning / next shift):**
1. **Reload Unity** (it is wedged): bring the editor to the foreground and let it finish the pending recompile+domain-reload, OR quit and reopen the project. Confirm `Scene_Hollowfen.unity` is intact (the DayFlagScheduler `_whenFlags/_thenFlags` gained a 5th pair `calden_records_requested→calden_records_read`; `_ChapelGateLock` FlagActivatedObject `_offFlagId` is now `calden_garden_unlocked`).
2. `read_console` → confirm 0 compile errors (esp. `Localization.cs`). Then `python3 tools/agent/run_integrity.py` — expect 20 quests / 51 dialogues / 10 NPCs clean.
3. **Play-verify the flow** (bridge, `EditorApplication.Step()`): fast-forward to end of `wendlightFound` → it should auto-chain `caldenReconcile` (waypoint = chapel). Talk to Calden → "records" dialogue sets `calden_records_requested`, quest stays active. Force a day rollover (`TimeManager.OnDayChanged`) → DayFlagScheduler sets `calden_records_read`. Talk to Calden again → reconcile dialogue: completes quest 19, grants `item.chapel_garden_key`, sets `chapel_garden_key_received/chapel_garden_unlocked/calden_garden_unlocked`, Calden +15/Almy +5, unlocks StoryCard_19, **the `ChapelGate_Planks` barrier deactivates** (verify visually), and auto-chains `eddaApprentice` (waypoint = mill). Talk to Edda → apprentice dialogue: completes quest 20, sets `edda_apprentice_accepted/apprentice_system_unlocked`, Edda +20, unlocks StoryCard_20. Check both post-completion repeat dialogues don't re-fire outcomes.
4. If green: update the system docs (above), set worksheet Status to verified, `git tag batch-19`, regen+republish dashboard. If a scene edit didn't survive the reload, re-apply the two scene edits (they're small; see step 1).

**Committed untagged** this shift (see commit) so the tree is clean; nothing stashed; editor was NOT in play mode.

**Known follow-ups seeded for later batches:** chapel-garden functional grow beds (cultivation pass), apprentice delivery/observation task loop (system pass), and the +15-vs-+20 Calden / +20-vs-+25 Edda relationship-number discrepancy between the bible's per-scene Unlocks block and its Act-III aggregate table (I used the per-scene numbers — Trevor may want the aggregate; trivial to flip in the two quest assets).

## Feedback to Trevor
- **This stall is the night shift's #1 reliability risk and it's worth hardening.** The editor App-Naps into a wedged state after `refresh_unity` triggers a recompile while backgrounded, and nothing I can do headlessly (activate, caffeinate -u, waits) un-wedges it — the MCP bridge can't help because it *needs* the same stalled main thread. Two concrete mitigations worth queuing: (1) have `caffeinate` include `-u` re-assertion or run the editor with App Nap disabled for the Unity.app bundle (`defaults write com.unity3d.UnityEditor5.x NSAppSleepDisabled -bool YES`) as a documented night-shift precondition; (2) teach the harness/tools a "compile churn" wait that polls the Editor log's mtime + the ILPP.Runner process rather than the bridge, so a shift can distinguish "still compiling" from "wedged" without guessing. I've written the diagnosis into this worksheet; consider promoting the `NSAppSleepDisabled` line into `night-shift.md` preconditions.
- The diagnosis path that actually worked was reading `~/Library/Logs/Unity/Editor-prev.log` (the *live* log here, not Editor.log) + `ps` CPU sampling. Might be worth a `tools/agent/unity_health.py` that prints {live-log mtime, editor %CPU, ILPP.Runner present?, bridge ping} in one shot for fast triage.
