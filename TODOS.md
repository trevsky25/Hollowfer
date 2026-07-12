# Hollowfen — Production Task Queue

**How agents use this file:** pull the top item from **Next up** unless Trevor directs otherwise. One item ≈ one batch (one worksheet, one tagged commit). An item is DONE only when: play-mode verified via the Unity MCP bridge, system docs updated, worksheet written, committed + tagged. Reorder/add items as reality changes — this is a living queue. If an item turns out to be >1 batch, split it here before starting.

**Status snapshot (2026-07-11):** Act I complete + committed (`c2b9405`, quests 1–7). Batch 11 (Act II A: quests 8–10, grow beds, day scheduler, tax deadline) is play-verified but **uncommitted in the working tree**. Batch 12 (agentic infra) in progress this session.

---

## Next up (ordered)

1. **Commit Batch 11 (Act II A)** — re-verify quests 8–10 in Play mode (clean-state reset first), then commit the working tree as Act II A with worksheet `batch-11-act2a.md` (write retroactively from session memory/transcripts), tag `batch-11`. Keep infra files (Batch 12) as a separate commit.
2. ~~**Backfill skeleton system docs**~~ **DONE in Batch 12** (2026-07-11, five parallel readers) — all `[BACKFILL]` markers replaced with code-verified content; audit findings moved to the hardening item below.
3. **Phase 2 infra: EditMode data-integrity tests** — Unity Test Framework assembly under `Assets/_Hollowfen/Tests/`: every quest's dialogue refs resolve · every dialogue asset's speaker NPC exists · localization IDs referenced by SOs exist in the LUT · no null entries in database SOs · build-settings scene list correct · achievement IDs non-empty on quests. Plus `Docs/tests.md` manifest (what each test proves). 
4. **Phase 2 infra: gotcha linter + pre-commit hook** — `tools/agent/lint_hollowfen.py` scanning `Assets/_Hollowfen/Scripts` for prohibited patterns (conventions.md list: hardcoded display strings, legacy Input, dataPath saves, public fields, emoji in dialogue assets, missing .meta) with `--fix` where mechanical; wire as git pre-commit.
5. **Act II B (scenes 4–5): The Trader's Ledger + Brightspore at the Bedside** — Theo + Edda NPCs, market expansion + tonic beats per bible. 
6. **Act II C (scenes 6–8): A Stranger at the Inn, Two Boards Come Down, Father Calden's Doubt** — Father Calden NPC, Act II completion state + flags.
7. **Phase 3 infra: review personas + wrap-up skill** — `Docs/review/` persona docs (Steam Deck cert, save integrity, localization, narrative/bible, performance), each owning its system docs; end-of-batch fan-out procedure; night-shift orchestration doc.
8. **Phase 3 infra: visual regression + perf baseline** — scripted screenshot pass of the ~8 canonical screens at 1280×800 into `Docs/screenshots/batch-NN/`; fixed-path village frame-time capture appended to `Docs/benchmarks.md`.

## Act content roadmap (after Act II)

- **Act III — Discovery** (8 scenes): Hollin's Inheritance · The Witch's Cottage (T4 gate) · The Wend's True Course · The Chapel Garden Opens · Edda Asks · Theo's Capital · The First Festival in Three Years · A Sealed Letter. New NPC: Hollin. Likely new content: T4 rare-wild mushrooms, Witch's Cottage location, festival event staging.
- **Act IV — The Choice** (3 scenes + endings): The Lord's Offer · The Source of the Wend · The Meeting. New NPC: Lord Aldric. Ending fork implementation (`ACH_END_INDEPENDENCE` et al.), Act IV completion state.
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
- **Meshy asset wants list** — see graphics-pipeline memory; key/book models don't exist in kitbash packs (needed for mill key, Almy's seedbook if shown in 3D).
- **Audio pass** — music, ambient, SFX beyond current mixer scaffolding.
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
