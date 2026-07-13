# Hollowfen — Production Task Queue

**How agents use this file:** pull the top item from **Next up** unless Trevor directs otherwise. One item ≈ one batch (one worksheet, one tagged commit). An item is DONE only when: play-mode verified via the Unity MCP bridge, system docs updated, worksheet written, committed + tagged. Reorder/add items as reality changes — this is a living queue. If an item turns out to be >1 batch, split it here before starting.

**Status snapshot (2026-07-12):** Acts I–III complete; **Act IV scenes 1–3 shipped** — 26 quests live and bridge-verified. Act III B (batches 19/21/22/23) + batch-20 (all species real) + Act IV: batch-24 `aldricOfferRead` (`act4_started`), batch-25 consult-the-village, batch-26 `wendSource` (clear-cut, Aldermark = real Grifola frondosa, Knowledge +15), **batch-27 `meetAldric`** (NPC_Aldric flag-gated at the new `manor` location; bible-verbatim negotiation ends at the fork; sets `aldric_meeting_started` + `final_choice_available` — fable-reviewed). **STOP POINT: the ending engine (item 13) is next in the story track** — 4-ending fork, canon-critical + FABLE-GATED, **Trevor's authorship**. **batch-28 (production UX track): settings screen rebuilt** to the code-built house style — fable-reviewed, all findings fixed. **batch-29: audio pipeline** — local Kokoro TTS VO (entrance scene: intro narration + Bram chain voiced, two-voice cast) + Misty Forest music bed; `DialogueLine.voiceClip` + narration clips + MusicManager; full coverage awaits the Q10 direction call. **batch-30: first-steps intro guide** — voiced parchment journal page after the homecoming intro (bible-transposed passage, live quest block, controls reference, "Set out →"); once per save; fable-reviewed (canon PASS; fixed a pre-existing swapped-gamepad-buttons bug in the settings binding table + an invisible-pause-under-overlay hazard). Growing: an "Act III–IV staging + world-dressing pass" (Theo/festival/Voss/Hollin placements, clear-cut dressing, Aldermark node+photo, **Aldric capsule→Meshy + manor building/props**). **batch-32 (production UX / ship-blocker): Georgia SDF font config fix** — all 3 project TMP fonts Dynamic→Static with full Latin-1 baked + source `.ttf` nulled (licensing) + churn killed; fable-reviewed, all required changes applied; the actual Mac `.app` boot test is split out as item 15b. Open: Q8 (Aldermark seedbook attribution), Q9 (credits copy for launch), Q10 (AI-VO direction + Steam disclosure), **Q11 (four UI symbols ✓✕△◉ have no font glyph — icon-set pass)**.

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
9. ~~**Act III B (scenes 4–8, quests 19–23)**~~ **COMPLETE 2026-07-12** (batches 19, 21, 22, 23 — all bridge-verified; Act III done end-to-end). Slices:
   - ~~**9a. `caldenReconcile` + `eddaApprentice` (scenes 4–5)**~~ **DONE 2026-07-12, tag `batch-19`** — quests 19–20 bridge-verified (full flow: auto-chain, flag-gated Calden routing, chapel-planks world swap, relationships/cards, post-arc repeats; 0 errors). Apprentice-delivery + chapel grow-beds deferred to their system passes.
   - ~~**9b. `theoCapitalOffer` (scene 6)** — FIRST CHOICE-UI CONSUMER~~ **DONE 2026-07-12, tag `batch-21`** — bridge-verified incl. the real DialogueScreen choice UI (on-disk `_choices` debut); fable-reviewed (6 findings fixed: Theo sell-loop regression, waypoint player-trap, spring contradiction, post-completion lockout, "For you." beat, night wording). Sets `theo_capital_offer_received` + Theo +10 + StoryCard_21. **Follow-up: Theo inn staging pass** (dual-placement at the Crooked Pintle via `theo_at_inn`; waypoint currently points at his wagon).
   - ~~**9c. `festivalHosted` (scene 7)**~~ **DONE 2026-07-12, tag `batch-22`** — bridge-verified: chain, Marra-anchored festival scene (Marra/Bram/Pell/Edda verbatim), Village Hope +20, Marra+15/Bram+10/Pell+12, StoryCard_22, `festival_hosted`; sell loop preserved. Cook/gather mechanic + physical square staging (lanterns) DEFERRED (QUESTIONS Q6 + backlog).
   - ~~**9d. `aldricLetter` (scene 8 → act3_complete)**~~ **DONE 2026-07-12, tag `batch-23` — ACT III COMPLETE** — Voss delivers Aldric's sealed letter (open-now/wait choice, 2nd choice-UI use); ScoreHooks sets `act3_complete`/`aldric_letter_received`/`voss_humanized`, Voss +5, StoryCard_23, grants `item.aldric_letter`. Full real-dialogue bridge-verified. Voss mill-doorway staging deferred (joins the Act III staging-pass backlog).
10. ~~**Phase 3 infra: review personas**~~ **DONE 2026-07-12, tag `batch-33`** — `Docs/review/` with a router
    (`README.md`: fan-out table batch-surface→persona→model + verdict protocol) and 5 persona specs
    (narrative-bible, steam-deck-cert, save-integrity, localization, performance), each with a repo-specific
    checklist, owned system docs, and PASS/PASS-WITH-CHANGES/BLOCK verdict format. Wired into night-shift.md
    Model Tiering (the WHEN) + the CLAUDE.md router. Formalizes the fable-gate that caught bugs in batches
    21/24/26/27/32. (Night-shift orchestration doc DONE in Batch 17: `Docs/night-shift.md`.)
11. **Phase 3 infra: visual regression + perf baseline** — scripted screenshot pass of the ~8 canonical screens at 1280×800 into `Docs/screenshots/batch-NN/`; fixed-path village frame-time capture appended to `Docs/benchmarks.md`.
12. **Act IV (scenes 1–3, quests 24–26 of the endgame)** — FABLE-REVIEW GATE (canon-critical endgame).
   - ~~**scene 1 `aldricOfferRead`**~~ **DONE 2026-07-12, tag `batch-24` — ACT IV STARTED** — Wren reads the sealed letter at the mill (prop-anchored `_MillLetter`); ScoreHooks sets `act4_started` + offer flags; StoryCard_24. Fable-reviewed (seal-continuity + grandmother lines fixed). ⚠️ `npc_consultations_unlocked` has no consumer yet — add "consult NPCs about Aldric" content.
   - ~~**scene 2 `wendSource`**~~ **DONE 2026-07-12, tag `batch-26`** — the upstream clear-cut (`_ClearCutSite` prop + `clear_cut` LocationData), Aldermark species #21 (= real **Grifola frondosa**, Q7 resolved), the Hollin scene, Knowledge +15 + evidence flags. Fable-reviewed (hesitation beat + seedbook wording fixed). Deferred to staging pass: Hollin's clear-cut placement, stump/camp dressing, Aldermark world node + photo.
   - ~~**scene 3 `meetAldric`**~~ **DONE 2026-07-12, tag `batch-27` — ACT IV SCENES 1–3 COMPLETE** — the mechanical SCAFFOLD: NPC_Aldric (placeholder capsule, dual-placement `_Aldric` FAO → `AldricGroup` gated on `wend_source_visited`) at the new `manor` LocationData (365,34.22,190); `Dialogue_Act4_MeetAldric` (8 bible-required lines verbatim, ends at the fork, NO `_choices`); Quest_26 chains off wendSource, reuses the pre-scaffolded `meeting_aldric` StoryCard, 0 deltas. ScoreHooks `meetAldric → [aldric_meeting_started, final_choice_available]`. Full chain bridge-verified; fable-reviewed (SHIP WITH CHANGES — silence moved after the final line, "he listened" beat restored). **Deliberately stops at `final_choice_available`** — the endings are item 13.
13. **Ending engine** — 4 score thresholds per story.md; unlock ONLY the chosen ending card 27–30 (fix the shared `unlockAt: 26`); letterboxed ending sequences + credits; `game_complete`. FABLE-REVIEW GATE (architecture + canon-critical).
14. **Hardening pass** (from the systems backlog: atomic saves, per-quest achievement hooks, TimeManager event reset, CurrentQuest-as-ID, dead-code sweep, seedbook collection-gating decision). FABLE-REVIEW GATE (save schema).
15. **Georgia SDF build fix + first Mac build boot test** — ship blocker (split into 15a/15b batch-32).
    - ~~**15a. Georgia SDF font config fix**~~ **DONE 2026-07-12, tag `batch-32`** — all 3 project TMP fonts
      converted Dynamic→**Static** with the full Latin-1 + punctuation set baked (Georgia 201 glyphs/2 pages,
      `LiberationSans SDF - Fallback` 204 incl. → ← ○ •), `ClearDynamicDataOnBuild: false`, `m_SourceFontFile`
      nulled (Georgia.ttf no longer redistributed — licensing), fallback wired into Georgia. Proven
      churn-immune (byte-identical hash across play/render/stop) — kills the recurring "git checkout the SDF
      churn" chore. Fable-reviewed (PASS WITH CHANGES; all required changes applied: Latin-1 baked, source
      nulled, build shrapnel reverted). Font rule + re-bake recipe in `conventions.md`.
    - ~~**15b. First Mac build boot test + build-scene-list cleanup**~~ **DONE 2026-07-12, tag `batch-34` —
      FIRST MAC BUILD SHIPS.** Dropped the 2 dead legacy scenes (build list = `[Scene_MainMenu, Scene_Hollowfen]`);
      fixed App Nap (relaunch with `NSAppSleepDisabled` set **while Unity is closed**); the first real player-compile
      caught + fixed a build-only vendor bug (`Magic Pig/Equipment System/…/IPBR_CharacterEquip.cs:63`, recorded in
      `Docs/vendored-build-fixes.md`). Build succeeded (4.49 GB dev `.app`, ~23 min first build), **`Georgia.ttf`
      absent** (batch-32 source-null verified), boots clean (Player.log: Input System + Physics init, 0 errors).
      Follow-ups: a **release build with stripping** (dev build is 4.49 GB — verify Deck size later), and the
      visual screenshot needs screen-recording permission (Player.log was the evidence this run).
16. **Localization wiring pass** (dialogue restructure is the big piece — see systems/localization.md gap list). FABLE-REVIEW GATE (architecture).
17. **Build cleanup sweep** + false-confidence test re-drill (tests.md audit item).
18. **Pre-EA checklist execution** (see section below) — builds, Deck audit, Steamworks, store assets. Several items need Trevor (Steam account, store page, trailer) — park those in QUESTIONS.md as they surface.

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
- **Wendlight world prefab remodel** — Wendlight is now the real Liberty Cap (Psilocybe semilanceata) but its Old Wend world node still uses a mystical glowing tinted-variant prefab. Retint/remodel to read like a real Liberty Cap (Meshy/model pass).
- **Theo inn staging pass** — `theoCapitalOffer` (batch-21) is set at the empty inn per the bible, but Theo is staged at his wagon (waypoint points there). Add the dual-placement (`theo_at_inn` set on `eddaApprentice` complete + inn-Theo group at the Crooked Pintle + wagon `_offFlagId`), repoint the waypoint/objective to the inn, and optionally gate the "night" framing on `TimeManager.IsNight` (would be its first consumer).
- **Festival staging + systems pass** — `festivalHosted` (batch-22) ships as a Marra-anchored coordination scene. If Trevor wants more (QUESTIONS Q6): a gather gate (forage the 3 wild dish species; needs a small `QuestForageObjective._setsFlagId` extension) and/or a real cooking/timer minigame; plus physical square staging — lanterns + festival dressing driven by the `festival_hosted` flag, and a proper square trigger. Also: `lacewig` needs a `_worldPrefab` if it should be forageable.
- **Relax `HasDialogueCycle`** — the integrity rule errors on ANY dialogue-graph cycle, forcing choice hubs to re-enter at the NPC layer. Allow cycles that pass through a choice node (the player always holds an exit pill, so no timeScale-0 trap), enabling true one-sitting branching conversations before the choice-heavy Act IV.
- **Cinematic dialogue camera** — two-shot dolly per `Docs/dialog-system.md`; finally consumes `DialogueLine.isCloseup` (authored in assets, currently unread — do NOT delete it in the dead-code sweep).
- **Cast models pass** — replace placeholder capsules (Joren, Voss, Marra, Almy…) via the Meshy pipeline; key/book models don't exist in kitbash packs (mill key, Almy's seedbook). See graphics-pipeline memory wants list.
- **Audio pass** — author RegionTrigger ambience volumes first, then AmbienceManager, SFX on existing events, mixer routing. VO+music pipeline EXISTS since batch-29 (`Docs/systems/audio.md`, `tools/agent/generate_vo.py`); full-cast VO coverage + Voice mixer group/slider + region music states pend the Q10 direction.
- **Build cleanup sweep** — leftover `Save 1`/`Steam 1` folders, retire `UITestDriver`, legacy Input usage in `LocationDebugHUD`, TMP migration in ConfirmModal/SaveSlot/Loading screens (+ MainMenu — SettingsScreen DONE in batch-28, use it as the template), 31 canon locations pass, content-vs-bible sweep.
- **Credits real copy** — presentation rebuilt in batch-28 (editorial hierarchy in the settings Credits tab, shipped 7-line copy kept verbatim); final launch copy = QUESTIONS Q9 (recommend a credits+licenses audit batch during pre-EA).

## Pre-EA production checklist (month ~10–12)

- [ ] Mac + Windows builds boot clean from Steam depot layout
- [ ] Steam Deck: 1280×800 UI audit, 60fps village floor, input glyph audit, Verified checklist pass
- [ ] All achievements registered + firing through Steamworks
- [ ] Steam Cloud saves round-trip Mac↔Windows
- [ ] Simplified Chinese localization complete
- [ ] Full-game playthrough (Acts I–II minimum for EA) with zero blockers
- [ ] Store page assets (capsule art, trailer, screenshots)
- [ ] False-confidence test audit (do tests actually assert what they claim?)
- [ ] **Steam AI-content disclosure** if any AI-generated VO/art ships (Q10) — required on the store page
- [ ] **Font + asset licensing audit** — Georgia is a licensed Microsoft font: verify redistribution rights (or swap to an OFL serif) before any public build; check every asset pack's attribution requirements against the credits copy (feeds QUESTIONS Q9)

## Done

- **Batch 12** (2026-07-11, in progress): agentic infrastructure Phase 1 — doc router split, system docs, TODOS queue, worksheets, tools/agent.
- **Batch 11** (2026-06-11, verified, uncommitted): Act II A — quests 8–10, cultivation grow beds, day-flag scheduler, tax deadline.
- **Batches 1–10** (`c2b9405` and earlier): Act I complete (quests 1–7), saves/scores/clock, UI framework + menu pages, foraging vertical slice, map system + redesign, main-menu port.
