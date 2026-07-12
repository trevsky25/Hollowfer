# Hollowfen — Production Task Queue

**How agents use this file:** pull the top item from **Next up** unless Trevor directs otherwise. One item ≈ one batch (one worksheet, one tagged commit). An item is DONE only when: play-mode verified via the Unity MCP bridge, system docs updated, worksheet written, committed + tagged. Reorder/add items as reality changes — this is a living queue. If an item turns out to be >1 batch, split it here before starting.

**Status snapshot (2026-07-12):** 18 of ~23 quests live and bridge-verified. Act III A shipped (batch-18): the Sable reveal, the Witch's Cottage + seedbook (field guide now 20 species incl. the T4 trio), and the Old Wend riverbed proving Aldric's damage. Night shift is a supported mode. Next: Act III B (chapel reconciliation → the sealed letter), featuring the choice UI's first canon use. Q4 open (T4 species presentation).

---

## Next up (ordered)

1. ~~**Commit Batch 11 (Act II A)**~~ **DONE 2026-07-11** — bridge smoke test (0 errors, save hydration correct), committed `c6d7e70`, tag `batch-11`.
2. ~~**Backfill skeleton system docs**~~ **DONE in Batch 12** (2026-07-11, five parallel readers) — all `[BACKFILL]` markers replaced with code-verified content; audit findings moved to the hardening item below.
3. ~~**Phase 2 infra: data-integrity checks**~~ **DONE in Batch 14** — `DataIntegrity.cs` editor utility (UTF test assembly impossible: game code is Assembly-CSharp-coupled), 13 check categories targeting silent runtime failures, negative-tested via corrupt→detect→restore; manifest at `Docs/tests.md`.
4. ~~**Phase 2 infra: gotcha linter + pre-commit hook**~~ **DONE in Batch 14** — `tools/agent/lint_hollowfen.py` (5 rules + waivers file), `.githooks/pre-commit` (lint always, integrity when the bridge is up), `smoke_play.py` promoted from the Batch 11 harness. All negative-tested.
5. ~~**Act II B (scenes 4–5)**~~ **DONE in Batch 15** — quests 11–12 end-to-end verified (14-step bridge-driven playthrough, 0 errors). Deferred to later milestones: weekly wagon schedule (5–7 day cycle), market-price journal notes, species-gated first sale, medicinal-recipes + Edda-delivery-tasks systems, Chanterelle/Lacewig world prefabs (fold into Act II C or T2 species pass).
6. ~~**Act II C (scenes 6–8)**~~ **DONE in Batch 16 — ACT II COMPLETE** (quests 13–15, 9-step bridge-verified run, 0 errors). Hollin/Pell/Calden staged; cottage boards + chapel gate world-swaps live. Deferred: restoration project board, chimney smoke, Deep Wood rumor chain (Act III).
7. ~~**Dialogue choice UI**~~ **DONE in Batch 17** — `DialogueChoice[]` (text/branch/flag, max 4), numbered-pill UI in the journal style, keyboard/pad/mouse + public `SelectChoice(int)`; integrity checks extended (choice count/text, branch-graph cycles); verified with in-memory dialogues + screenshot. **Act III is unblocked.**
8. ~~**Act III A (scenes 1–3)**~~ **DONE in Batch 18** — quests 16–18, 7-step bridge-verified, 0 errors. T4 trio in the field guide, Wendlight forageable, Deep Wood + Old Wend staged. (Note: scenes were linear per bible — choices debut with `theoCapitalOffer`.) Deferred: Witchwell rare-source system, seedbook collection-gating for psychoactive/deadly species.
9. **Act III B (scenes 4–8, quests 19–23): The Chapel Garden Opens (`caldenReconcile`), Edda Asks (`eddaApprentice`), Theo's Capital (`theoCapitalOffer` — FIRST CHOICE-UI CONSUMER), The First Festival (`festivalHosted`, 4-dish prep), A Sealed Letter (`aldricLetter` → act3_complete)** — chapel garden reopen swap, apprentice hooks, festival staging, Voss delivers the letter.
10. **Phase 3 infra: review personas** — `Docs/review/` persona docs (Steam Deck cert, save integrity, localization, narrative/bible, performance), each owning its system docs; end-of-batch fan-out. (Night-shift orchestration doc DONE in Batch 17: `Docs/night-shift.md`.)
11. **Phase 3 infra: visual regression + perf baseline** — scripted screenshot pass of the ~8 canonical screens at 1280×800 into `Docs/screenshots/batch-NN/`; fixed-path village frame-time capture appended to `Docs/benchmarks.md`.

## Act content roadmap (after Act II)

- **Act III A — Discovery** (scenes 1–3, quests ~16–18): Hollin's Inheritance · The Witch's Cottage (ruined; T4 gate) · The Wend's True Course. New areas: Deep Wood, Witch's Cottage, dry Wend riverbed + Wendlight species.
- **Act III B** (scenes 4–8): The Chapel Garden Opens (`caldenReconcile`, chapel garden world swap) · Edda Asks (`eddaApprentice`) · Theo's Capital (`theoCapitalOffer` — needs choice UI) · The First Festival in Three Years (`festivalHosted`) · A Sealed Letter (`aldricLetter`); cottage-repaired world swap.
- **Act IV — The Choice** (3 scenes): The Lord's Offer (`aldricOfferRead`) · The Source of the Wend (`wendSource`) · The Meeting (`meetAldric`). New: NPC_Aldric, clear-cut location, Aldermark species.
- **Ending engine** — 4 score thresholds per story.md; unlock ONLY the chosen ending card 27–30 (⚠️ fix shared `unlockAt: 26` on those cards); letterboxed ending sequences + credits; `game_complete` flag.
- **The Old Wood** — `Scene_OldWood` doesn't exist yet; scope when Act III demands it (40fps floor allowed per steam-constraints.md).

## Systems backlog (schedule between act batches)

- **Hardening pass (from the 2026-07-11 doc-backfill audit)** — (a) achievement hooks fire ONLY on story-card unlocks, not quest completions — violates our non-negotiable; add per-quest achievement firing. (b) Save writes aren't atomic (`File.WriteAllText` in place) — temp-file+rename to survive crash-mid-write. (c) `TimeManager` static events lack the `ResetOnLoad` delegate reset the other stores have. (d) `SaveSlotMeta.CurrentQuest` stores localized text, not an ID. (e) Dead code sweep: `MapScreen.BuildFrame` + stale header, `DialogueScreen.BuildFrame`, `QuestBootstrap._startDelaySeconds`, `QuestData._order`, `DialogueLine.isCloseup` (keep if cinematic pass is coming). One focused batch.
- **Input consolidation onto action maps** — DialogueScreen/MapScreen/InspectScreen poll devices directly; the Dialogue action map is entirely unused. Blocks any future rebinding feature; fold into the settings/controls milestone.
- **Georgia SDF build fix** — font loads editor-only via AssetDatabase; move to `Resources/` or serialized refs. **Ship blocker; do before any build milestone.**
- **Wren animation fix** — Mixamo rig vs StarterAssets mismatch (palms-up run, curled idle fingers). Own focused session: Mixamo-sourced anims, avatar reconfig, or hand mask.
- **Mushroom mesh decimation** — Field Mushroom is 414k verts; decimate all Meshy exports before EA.
- **Localization wiring pass** — the big one is dialogue: `DialogueLine.speaker`/`.text` are raw strings AND speaker doubles as the SpeakerColors dictionary key, so localizing means restructuring (IDs + speaker enum/id). Also: menu pages through `Localization.Get`, QuestHUD eyebrow, StoryBeats captions, map chrome strings, LUT completion; then Simplified Chinese translation (pre-EA). Full gap list in `Docs/systems/localization.md`.
- **Steamworks SDK** — wire AchievementManager to real Steam achievements; Steam Cloud config; rich presence.
- **Region-enter toasts** — LocationRegistry events exist, UI not built.
- **Input asset consolidation** — merge StarterAssets map into project InputActions when a concrete need arises.
- **Mushroom tier buildout** — T2 (Joren's tools unlock species), T3 cultivation species beyond intro, T4 rares. T5 stays unimplemented (bible-reserved).
- **Cinematic dialogue camera** — two-shot dolly per `Docs/dialog-system.md`; finally consumes `DialogueLine.isCloseup` (authored in assets, currently unread — do NOT delete it in the dead-code sweep).
- **Cast models pass** — replace placeholder capsules (Joren, Voss, Marra, Almy…) via the Meshy pipeline; key/book models don't exist in kitbash packs (mill key, Almy's seedbook). See graphics-pipeline memory wants list.
- **Audio pass** — author RegionTrigger ambience volumes first, then AmbienceManager, SFX on existing events, mixer routing.
- **Build cleanup sweep** — leftover `Save 1`/`Steam 1` folders, retire `UITestDriver`, legacy Input usage in `LocationDebugHUD`, TMP migration in ConfirmModal/SaveSlot/Loading screens, 31 canon locations pass, content-vs-bible sweep.
- **Credits real copy** — Settings → Credits tab has placeholder text.

## Pre-EA production checklist (month ~10–12)

- [ ] Mac + Windows builds boot clean from Steam depot layout
- [ ] Steam Deck: 1280×800 UI audit, 60fps village floor, input glyph audit, Verified checklist pass
- [ ] All achievements registered + firing through Steamworks
- [ ] Steam Cloud saves round-trip Mac↔Windows
- [ ] Simplified Chinese localization complete
- [ ] Full-game playthrough (Acts I–II minimum for EA) with zero blockers
- [ ] Store page assets (capsule art, trailer, screenshots)
- [ ] False-confidence test audit (do tests actually assert what they claim?)

## Done

- **Batch 12** (2026-07-11, in progress): agentic infrastructure Phase 1 — doc router split, system docs, TODOS queue, worksheets, tools/agent.
- **Batch 11** (2026-06-11, verified, uncommitted): Act II A — quests 8–10, cultivation grow beds, day-flag scheduler, tax deadline.
- **Batches 1–10** (`c2b9405` and earlier): Act I complete (quests 1–7), saves/scores/clock, UI framework + menu pages, foraging vertical slice, map system + redesign, main-menu port.
