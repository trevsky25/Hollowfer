# Tests & Verification Manifest
What every automated check in this project actually proves, how to run it, and what is NOT covered. Three layers: gotcha lint (filesystem, no Unity), data integrity (asset layer, via the editor), play-mode smoke (runtime boot health).
Run: `tools/agent/lint_hollowfen.py` (always) · `tools/agent/run_integrity.py` or menu "Hollowfen/Data Integrity Report" (Unity open) · `tools/agent/smoke_play.py` (Unity open + visible). Pre-commit runs lint always and integrity when the bridge is up (`git config core.hooksPath .githooks`, set per clone).
Checker code: `Assets/_Hollowfen/Scripts/Editor/DataIntegrity.cs` — an editor utility, NOT a Unity Test Framework assembly, because game code compiles into Assembly-CSharp (coupled to no-asmdef third-party sources) and test assemblies can't reference it.
Philosophy: checks target failures that are SILENT at runtime (Localization.Get returns the raw id on a miss; PickDialog skips null entries; extra relationship ids are ignored). Loud failures don't need tests — the console already catches them.
Waiver policy: lint waivers in `tools/agent/lint_waivers.txt`, each pointing at the TODOS item that owns the fix. A waiver is a debt marker, not a dismissal.
Status: all three layers verified through 2026-07-21. Focused verifiers cover save-file integrity, durable inventory batches, endings, presentation ownership, repeatable gameplay, village requests, Living Restoration, the apothecary, day/night, dynamic weather, NPC schedules, relationship memory/personal arcs, regional feedback, audio/voice, and active production UI. Batch 120 adds a gated visual/performance baseline; Batch 121 exposes the synchronous checks through a safe native Pipeline allowlist. Destructive state verifiers use an isolated temporary save directory where supported.

> Self-healing doc: adding a check? Document it here. Hitting a new failure class? Add a check AND a row here in the same batch.

---

## Layer 1 — Gotcha lint (`tools/agent/lint_hollowfen.py`)

No Unity required; runs in every pre-commit. Scans `Assets/_Hollowfen` only (never third-party).

| Rule | Severity | Proves |
|---|---|---|
| `legacy-input` | ERROR | No `UnityEngine.Input.*` / `Input.GetKey…` — new Input System only (Steam Deck cert). |
| `datapath-save` | ERROR | No runtime `Application.dataPath` — saves must be persistentDataPath (Steam Cloud). Editor-only import tooling is excluded because it is stripped from players. |
| `emoji` | ERROR | No emoji in dialogue/story-card assets (canon voice rule). |
| `missing-meta` | ERROR | Every file/folder has its `.meta`, no orphans — prevents broken GUID refs on other machines. |
| `public-field` | WARN | No public mutable fields on MonoBehaviour/ScriptableObject classes (data structs/DTOs exempt). Heuristic — treat as a nudge. |

## Layer 2 — Data integrity (`DataIntegrity.RunAll`, asset layer)

Runs inside the editor (menu / bridge / batchmode `-executeMethod …DataIntegrity.RunCLI`). Categories:

| Category | Severity | Proves |
|---|---|---|
| `quest-id` | ERROR | Quest ids non-empty + unique. |
| `quest-chain` | ERROR/WARN | No `NextQuest` cycles; exactly one chain root; no unreachable quests. |
| `localization` | ERROR | Every fixed id consumed by live UI, including `journal.*` and `ending.*` chrome, exists in `Localization._table`. |
| `relationships` | ERROR | Parallel id/delta arrays same length; every id is a bible cast id (catches typos — extras are silently ignored at runtime). |
| `dialogue-lines` | ERROR | Every dialogue has lines; no empty speaker/text. |
| `dialogue-speaker` | WARN | Speaker exists in `DialogueScreen.SpeakerColors` (else silent default-ink fallback). |
| `dialogue-voice` / `dialogue-voice-format` | ERROR | Every line owns the exact index/speaker WAV expected by the pipeline; source VO is 24 kHz mono. |
| `dialogue-voice-manifest` | ERROR | The SHA-256 manifest covers the exact dialogue graph and rejects stale speaker/text/clip mappings. |
| `dialogue-chain` | ERROR | No cycles through the NextDialog + choice-branch graph (a cycle traps the player at timeScale 0). |
| `dialogue-choices` | ERROR/WARN | ≤4 choices; no empty text; ending and dialogue branches are mutually exclusive; ending consequences cannot leak into a loose choice flag. |
| `dialogue-flags/quest` | ERROR/WARN | No empty flag ids; CompleteQuest targets live under Data/Quests. |
| `story-moment-*` | ERROR | Every authored reveal has localized captions, aligned 24 kHz mono VO slots, available image mappings, one valid quest/dialogue owner, and valid localized live-page bounds/reveal beats. Cursive page moments additionally require the pre-baked static Cedarville font atlas. |
| `npc-id` / `npc-entries` | ERROR/WARN/INFO | Ids lowercase/unique/canon; no unconditional entry shadowing later entries (including forage/blocked-flag gates); impossible require+block flag rows and null-dialog entries are flagged; missing repeat dialog is INFO (intentional for quest-window NPCs). |
| `scorehooks` | ERROR | `ScoreHooks.QuestFlags`/`SpeciesFlags` magic-string keys match real asset ids (hand-synced today). |
| `database` | ERROR | Story-card/mushroom databases contain exactly 30/21 entries, with no nulls or duplicate ids. |
| `journal-data` | ERROR | Required Story, mushroom, and Wren profile fields are populated; journal screens cannot silently render structurally blank cards or sections. |
| `journal-art` | ERROR/INFO | All 30 Story images, the Wren hero, and all five character plates exist. A missing mushroom photo is reported as INFO because the Field Guide has an intentional missing-sketch state. |
| `journal-model` / `mushroom-model` | ERROR/INFO | Every species in `MushroomModelManifest.json` has both prefabs assigned; each world prefab owns a `MushroomNode` pointing back to that exact species SO; each SO's preview exposure matches its model manifest value. Wren's profile must hold its dedicated preview prefab, breathing idle, and supported exposure. The report states current mushroom 3D coverage (20/21; Aldermark pending by design). `JournalPreview` must also exist as an isolated camera/light layer. |
| `mushroom-gameplay` | ERROR/INFO | All 21 species have a respawn/economy/progression profile; dangerous/psychoactive stock has no ordinary buyer value; safe stock has a buyer; gated tiers declare flags; cultivable species have world prefabs; at least three cultivation recipes exist. Repeat basket-sale dialogues must name a real buyer rather than silently using flat pricing. |
| `village-request` | ERROR | Unique request/NPC IDs; localized copy + art; 1–4 combined aligned positive safe-species/prepared-product requirements; valid quest gates/outcomes; recurring/story outcome separation; ≥3 rotations per Marra/Edda/Theo; exactly one gathering; runtime database completeness. |
| `restoration` | ERROR | Unique project ids; runtime database completeness; valid reveal/completion quests; localized title/summary/location/prompt/stage/milestone copy; all five visible stages authored; non-empty stage/milestone conditions. |
| `ending-*` | ERROR | Exactly four canonical endings; unique IDs/flags/cards; populated dialogue/art/epilogue/achievement; valid relationship arrays and final-choice gate; choice-only card unlocks; all four wired once into Aldric's meeting; the post-quest/pre-ending save state has an Aldric recovery route blocked after `game_complete`. |
| `build-settings` | ERROR | `Scene_MainMenu` enabled at index 0; `Scene_Hollowfen` enabled. |
| `coverage` | INFO | Counts of assets checked — no silent scope shrinkage. |

## Layer 3 — Play-mode smoke (`tools/agent/smoke_play.py`)

Activates Unity (macOS App Nap freezes the player loop when the app is hidden — `PlayModeBackgroundTicker` can't tick a napped app), enters Play mode, immediately redirects all journal writes to a unique temporary directory, requires ≥240 frames, asserts **no new console errors**, samples quest/clock state, exits, and deletes the isolated fixture. The bridge reader retries transient boolean acknowledgements during script reload instead of mis-parsing them as console counts, and applies a short per-call deadline so a stale SSE response cannot hang the runner for five minutes. Run at the end of every implementation batch and in night-shift wrap-up.

## Gated visual/performance baseline (`tools/agent/capture_visual_baseline.py`)

With the pinned Unity Editor stopped and scenes clean, run `python3 tools/agent/capture_visual_baseline.py`. The Pipeline runner first executes `ProductionBuildGate.ValidateAuditPreflightForAutomation()` and `DataIntegrity.RunAllAsReport()`. It then fixes Game View at 1280×800, stages all 30 Story cards and 21 Field Guide entries in Play Mode memory, and captures main menu, save slots, settings, Story index/detail, Field Guide index/detail, and Wren. `ProductionUIVerifier.VerifyActiveForAutomation()` must return PASS for each settled presentation before Unity may queue its PNG.

Both UI and gameplay phases arm unique temporary save directories and clear their overrides in `finally`; the runner also restores the starting scene and stops Play Mode. Existing evidence is not overwritten unless `--replace` is explicit. The report records exact gate output, dimensions, and performance samples under `Docs/screenshots/batch-NN/`.

The five-stop route records 60 wall-time samples around `EditorApplication.Step()` plus Pipeline triangle, SetPass, and allocated-memory snapshots. This includes Editor overhead and is CPU-side only: it is a regression reference for later batches, **not** a standalone-player, GPU, 60fps, or Steam Deck certification result. Captures are reviewed manually; the harness does not yet claim tolerant pixel-diff automation.

## Native Pipeline command layer

With the pinned Hollowfen Editor open, Unity CLI/Pipeline exposes seven project-owned Editor commands.
They are an invocation layer over the checks in this manifest, not a replacement test framework:

| Command | What it proves or controls |
|---|---|
| `hollowfen_health` | The command is routed to the expected Editor and reports its scene, compilation, Play Mode, bounded console, package-version, and save-override state. |
| `hollowfen_preflight` | The exact audit-build technical preflight and full data-integrity report both pass from a stopped, clean Editor. |
| `hollowfen_verifier_catalog` | The live adapter advertises the 24 hardcoded synchronous verifier names and their minimum Editor/isolation state. |
| `hollowfen_run_verifier` | `dry_run=true` reports blockers without invoking a verifier; `confirm=true` runs only an allowlisted method and accepts only an explicit synchronous PASS report. |
| `hollowfen_begin_save_isolation` / `hollowfen_end_save_isolation` | A confirmed Play Mode session is redirected to a command-owned `Library/HollowfenPipeline/isolated-saves/<id>` fixture, and cleanup proves ownership before clearing/deleting it. |
| `hollowfen_world_audit` | The active loaded scene has a bounded structural report for missing scripts, collider transforms, object/render/material counts, and authored mesh instances. It does not prove runtime framerate. |

Typical non-mutating use:

```bash
unity --format json --no-banner command --project-path "/absolute/path/to/Hollowfen-Unity" hollowfen_preflight
unity --format json --no-banner command --project-path "/absolute/path/to/Hollowfen-Unity" hollowfen_verifier_catalog
unity --format json --no-banner command --project-path "/absolute/path/to/Hollowfen-Unity" hollowfen_run_verifier --name narrative-copy --dry_run true
unity --format json --no-banner command --project-path "/absolute/path/to/Hollowfen-Unity" hollowfen_run_verifier --name narrative-copy --confirm true
```

Pipeline command arguments retain underscores (`dry_run`, `include_inactive`, `max_findings`,
`interval_ms`). Entering or exiting Play Mode causes the expected domain reload and temporary
connection loss; poll the read-only `editor_status` command before continuing. For a mutating
gameplay verifier, dry-run and confirm `hollowfen_begin_save_isolation`, enter Play, wait for real
frames, dry-run and confirm the verifier, exit Play, then dry-run and confirm
`hollowfen_end_save_isolation`. Finish by confirming the health report has no active override.

`PresentationSessionVerifier.Run()` is asynchronous and returns no completion report, so it remains
on the menu/Coplay workflow below rather than allowing the native adapter to infer success. Full
operating and extension rules are in `Docs/systems/agent-tooling.md`.

## Focused save-integrity verifier

Call `SaveIntegrityVerifier.RunAll()` through the editor bridge. It redirects `SaveManager` to a unique directory under the system temp path, restores the prior directory override and active slot in `finally`, and deletes its fixtures; it does **not** touch the tester's real journals.

It proves: historical flat schema-0 loading and upgrade to a schema-1 envelope while retaining the legacy backup; `{}` parseable-corruption fallback; checksum-tamper fallback; higher-revision flushed-temp selection and lower-revision-temp rejection; corrupt primary+backup load isolation; targeted-autosave refusal for a damaged active slot; an authoritative future-schema barrier instead of downgrade to an older backup; timestamp/playtime/currency/transform/parallel-array normalization; non-ASCII/full-payload round-trip including playtime, flags, inventory, and transform; and recovered rewrite to a valid primary while preserving a backup and quarantining the damaged primary.

Expected result: `SAVE INTEGRITY — PASS: legacy upgrade, semantic corruption, checksum, temp/backup revision recovery, future-version barrier, load isolation, normalization, full round-trip, recovered rewrite`.

## Focused inventory-transaction verifier

Call `InventoryTransactionVerifier.RunAll()` through the editor bridge. Like the save-integrity verifier, it redirects save IO to a unique temporary directory, snapshots/restores the active slot plus inventory, purse, scores, requests, quests, and cards, then deletes its fixtures. It does **not** alter real journals.

It proves: mismatched arrays and non-positive amounts are rejected; duplicate species are aggregated before stock validation; rejected batches change no count, event, or disk revision; a successful duplicate batch publishes exactly one event after exactly one higher-revision save contains the staged counts; a successful paid village delivery commits inventory, purse, flags, and request state in exactly one full-slot revision; and a paid delivery against deliberately damaged primary+backup files fails before mutation or artifact writes. Its final fault fixture uses the real `festivalHosted` request after all runtime systems have staged inventory, flags, quest/card, score/relationship, next quest/waypoint, and achievement work, rejects the final full-snapshot write, then proves byte-identical primary/backup/temp artifacts, exact runtime rollback, no leaked transaction, and zero inventory/request/score/coin/quest/card/waypoint/achievement callbacks.

Expected result: `INVENTORY TRANSACTIONS — PASS: strict batch validation, duplicate aggregation, single-revision village commit, damaged-journal isolation, and final-commit rollback with zero quest/card/UI/achievement publication`.

## Focused ending verifier

After backing up `persistentDataPath/saves`, call `EndingEngineVerifier.RunAll()` through the editor bridge. It proves the pragmatic fallback and exact score boundaries, writes/reloads the vulnerable post-quest/pre-ending snapshot and reopens Aldric's fork, verifies the fork disappears after `game_complete`, resolves each of four endings independently, asserts one canonical flag/card/achievement, rejects a second commit, reloads the disk snapshot, and deliberately corrupts the primary to prove `.bak` recovery. Restore the save directory afterward; batch-72 verified all four original slot hashes matched their backups.

## Focused presentation-session verifier

In bare `Scene_Hollowfen` Play Mode with no screen or cinematic already open, run `Tools > Hollowfen > Verify Presentation Session Ownership` or call `PresentationSessionVerifier.Run()`. It acquires nested policies for slow motion, cursor, HUD, and gameplay input, releases owners out of order, double-disposes a lease, and proves minimum time-scale selection plus exact baseline restoration. It also proves the final close remains shortcut-blocked for that frame, clears on the next frame, and leaves zero active owners. It does not write a save.

Expected result: `[PresentationSessionVerifier] PASS - nested leases, minimum time override, idempotent disposal, input/cursor/HUD restoration, same-frame shortcut blocking, and zero-leak end state.`

## Focused gameplay-foundation verifier

In `Scene_Hollowfen` Play Mode, call `GameplayFoundationVerifier.RunAll()` with an isolated save-directory override. It proves: all 63 stable wild nodes, at least one source per species profile, identification before every harvest, the single rest point, non-mutating Marra quotes, Marra/Theo totals and refused-item retention, purse ledger running balances and disk persistence, forage save/respawn, cultivation, and exact `sundown → dawn` event order.

## Focused village-request verifier

After backing up saves, call `VillageRequestVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It proves deterministic dawn rotation across all three NPCs; one claim per NPC/day; atomic raw and prepared requirement consumption and copper; first-only relationship rewards; tracked-order save/expiry; controller action structure and pause restoration; Edda/Theo eligibility; all three sequential apothecary deliveries including rejected final-commit rollback; the stable Lacewig source; and festival ingredient → flags → quest/card/next-quest persistence. Restore the save directory afterward.

## Focused apothecary verifier

Call `ApothecaryPreparationVerifier.RunAll()` with `Scene_Hollowfen` loaded. It checks the owned purchased-building prefab, open door thresholds/player-sized traversal, URP material/collider/renderer/triangle/light budgets, level mill-terrace placement, location marker, three progressively gated four-step recipes, identified ingredient rules, localized safety/use copy, and three save-backed purchased shelf props. Its preparation transaction runs in a unique temporary journal and proves successful stock persistence, exact ingredient consumption, one-revision/event publication, rejected-commit rollback, and historical null-save hydration.

Expected result: `APOTHECARY — PASS: complete purchased laboratory building, open traversal, level mill terrace, identification gates, four-step recipes, isolated atomic commit/rollback, old-save hydration, and persisted shelf stock`.

Call `ApothecaryCaseworkVerifier.RunAll()` in the same Play Mode session for the appointment chapter. It verifies the six sequential cases; two observations and two interview answers per case; three distinct prepared choices; every localized outcome/memory; the purchased open-book interaction; exact one-dawn chaining; prepared-stock consumption; due-day follow-up; case, relationship, bond, hope, and knowledge persistence; and injected final-commit rollback with no revision, memory, or UI-event leakage. `NPCScheduleVerifier` separately proves each patient and Edda occupy distinct physical apothecary marks for intake and return only when follow-up is due.

Expected result: `APOTHECARY CASEWORK — PASS: 6 sequential character cases, 24 evidence beats, 18 reasoned outcomes, physical patient/mentor staging, prepared-stock consumption, delayed follow-ups, durable memories/bonds, and commit-failure rollback`.

## Focused dynamic-weather verifier

Call `WeatherSystemVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It proves deterministic repeatability across 40 days, coverage of all six weather kinds, no day-one storm, bounded profiles, wet growth and wild-respawn hooks, live clear-versus-rain fog/lighting, a camera-local rain rig capped at 720 particles, three 48 kHz mono ambience-routed sources, and the forecast HUD. It restores the original clock/weather/lighting fixture afterward and does not intentionally write a journal.

Expected result: `DYNAMIC WEATHER — PASS: deterministic six-period forecast, smooth state profiles, single-owner sun/sky/fog integration, camera-local rain, shelter exposure, wind, procedural rain/thunder audio, wet-weather growth/respawn, delivery premiums, reduced-motion scaling, and diegetic forecast HUD`.

## Focused Living Restoration verifier

In `Scene_Hollowfen` Play Mode, set `SaveManager.EditorSaveDirectoryOverride` to an isolated temporary directory, then call `RestorationVerifier.RunAll()`. It proves exactly two staged cottage sites, one village board, and one authored cottage reveal; Foraging interaction triggers; two smoke and evening-light rigs; monotonic stage advancement/regression refusal; the visible WorkUnderway switch; a real clock rollover promotes `cottages_reopened_2` and queues exactly one deferred reveal; hydration does not replay that reveal; legacy flag migration; full `SaveCoordinator` save/load round-trip; rehydrated WorkUnderway/Occupied roots and board unlock; and restoration parallel-array/value normalization. Delete the temporary directory afterward; real journals are never opened.

Call `BridgeRestorationVerifier.RunAll()` in the same scene/play mode for the second vertical slice. It creates and removes its own unique temporary save directory and proves cottages/bridge registration in the shared catalogue; bridge/deck alignment; the protected 2.9m foot lane; six-meter reopened deck; Joren/Theo/Pell work rows; exact two-line funding and duplicate refusal; one-revision atomic commits; separate work/reopen dawns; reveal queuing; first-crossing completion; and injected final-save rollback with no coin, flag, stage, revision, or event leakage.

Call `VillageRestorationExpansionVerifier.RunAll()` for the completed roadmap. It owns an isolated journal and proves all seven catalogue rows, five later staged world roots, ten exact contribution transactions, distinct two-dawn promotions, six story-gated crew routes, all five permanent gameplay benefits, exact score rewards, Witch's Path flags, first-use idempotence, rejected-commit rollback, collider safety, three grow beds, and bounded presentation cost (430 authored renderers / 240 worst simultaneous, 18 shared materials, seven particles, four shadowless lights). It restores scores, projects, quests, clock, purse, schedules, slot, and disk override in `finally`.

Expected result: `LIVING RESTORATION — PASS: 2 staged sites + village board, monotonic project stages, legacy flag migration, normalized parallel arrays, and full save/load round-trip`.

Expansion expected result: `VILLAGE RESTORATION EXPANSION — PASS: 7-project catalogue, five staged worksites, story-gated crews, ten atomic supply lines, two dawn beats, five permanent benefits, Witch's Path flags, score rewards, first-use rollback, and bounded world cost`.

## Focused accessibility-presentation verifier

In Play Mode run `Hollowfen > Verify > Accessibility Presentation`. It snapshots the exact presence/value of all three accessibility PlayerPrefs, temporarily selects 115% Interface Size plus Reduced Motion and Caption Backing, advances two frames, and proves every active scale-aware canvas adopted the projected reference resolution. A disabled focus fixture then proves Reduced Motion removes scale travel while retaining glow, standard motion still reaches its authored scale, and caption backing remains set. The original preferences are restored even after a failure.

Expected result: `ACCESSIBILITY PRESENTATION — PASS: 115% scaling reached N runtime canvases; reduced motion preserves a visible, stable focus cue; caption backing persists.`

## Focused day/night-lighting verifier

Call `DayNightLightingVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It samples noon, dusk, and late night and proves a progressive exposure transition; materially darker sky/ambient light; denser night fog; low-intensity cool moonlight; all 18 practicals (six village, four cottages, four bridge, four expansion) on at night and disabled at noon; and restoration of the test's original clock value. It does not write a save.

## Focused NPC-schedule verifier

Call `NPCScheduleVerifier.RunAll()` in `Scene_Hollowfen` Play Mode for all nine derived routines. It proves ordinary day/night movement, restoration crews, apothecary appointments, Theo's relocated east-market anchor and private Pintle offer, Almy and Edda's mill-door arrivals, Calden's mill-to-chapel transition, and Voss's market-to-mill story staging. It uses an isolated temporary journal and restores runtime state.

## Focused story/world alignment verifier

Call `StoryWorldAlignmentVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It proves the exact 26-quest order and every `NextQuest` link; a registered compass destination for every objective; zero asset-integrity errors; all eleven stage-aware route states for Almy's lesson, the Brightspore chain, and Calden's warning; nine principal schedules; Theo/Joren/Almy alignment with their physical sites; and the 235m × 367m whole-map story footprint. It uses an isolated temporary journal and restores quest, score, inventory, key-item, schedule, waypoint, slot, and save-override state.

Expected result: `STORY & WORLD — PASS: all 26 quests form one recoverable chain; every quest has a registered destination; 11 staged objective routes move the compass and copy through Almy, Brightspore, and Calden; nine scheduled principals occupy distinct story sites across the 235m x 367m production route.`

## Focused relationship-memory verifier

Call `RelationshipSystemVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It owns a unique temporary save directory and proves old-save hydration plus disk round-trip, idempotent dated memories, canonical symmetric NPC bonds, monotonic favor stages, quest-priority dialogue routing, two playable personal moments for all six core villagers, complete VO across all 46 relationship conversations, four ending reactions per villager, and seven live recovered-village schedules. It restores scores, quests, relationships, clock, schedules, active slot, and save override in `finally`.

Expected result: `RELATIONSHIP SYSTEM — PASS: save migration + round-trip, idempotent memories, canonical NPC bonds, monotonic six-villager favors, quest-priority routing, 46 voiced relationship conversations, and nine recovered village schedules`.

## Focused world-feedback verifier

Call `WorldFeedbackVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It proves four shared localized regions; six trigger volumes including the southern starting village, clear-cut, and manor; real `RegionChanged` fan-out to ambience/music/toast; four mixer-routed ambience sources; distinct non-clipping 12-second 48 kHz mono day/night samples; noon/night balance; cached regional profile changes; independent Ambience mute; two-bank adaptive music state with regional/night restraint; and non-blocking localized arrival copy. It restores clock, audio preference, manager regions, and toast visibility and does not write a save.

## Focused gameplay-audio verifier

Call `GameplayAudioVerifier.RunAll()` in a menu-booted `Scene_Hollowfen` Play Mode so the persistent mixer/UI roots exist. It builds and inspects all 13 gameplay cues for 48 kHz mono, non-silent, non-clipping sample data; proves SFX mixer routing and delivery dispatch; verifies the complete current dialogue graph (145 assets / 410 exact index-speaker references) at 24 kHz mono; and opens a real Bram conversation to prove the line-show path dispatches its assigned clip. The manifest check independently binds this exact current graph by hash. It closes the test dialogue and does not write a save.

Call `LivingVillageEncounterVerifier.RunAll()` in an isolated `Scene_Hollowfen` Play Mode. It verifies four paired encounter assets and all 20 voiced lines, exact schedule-label routing, the deterministic wet-weather apothecary arrival, two physical participants per location, eight durable memories, four NPC bonds, and immediate one-shot dispersal after the conversation closes.

## Focused production-UI verifier

While the presentation under test is fully open, run `Tools > Hollowfen > Verify Active UI Presentation`
or call `ProductionUIVerifier.VerifyActiveForAutomation()`. It checks the shared accessible canvas
contract projected through the current Interface Size preference, invisible raycast blockers, TMP font/readability/clipping, minimum hit targets, missing UI components, and cutting-shadow containment. It also validates current focus ownership for both `UIManager` screens and standalone modal canvases, and proves the adaptive quest objective remains inside its rounded card. Runtime focus recovery restores the first valid control when mouse input, a destroyed dynamic row, or a presentation transition clears EventSystem selection. Batch 87 exercised the main
menu, all journal pages, save/load/settings/modal flows, gameplay HUD, pause, inventory, inspect,
map, requests, cultivation, NPC dialogue, and the mushroom-cutting kneel at 1280×800; every settled
presentation returned `0 critical / 0 advisory`. This is structural presentation lint, not a pixel-
golden visual test, so screenshots remain part of the handoff evidence.

## Focused economy/progression verifier

Call `ProductionBalanceVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It checks all 21 mushroom price/respawn profiles, Theo's market premium, the opportunity cost of all 12 recurring jobs, first-tax attainability from the first common forage population, the objective's disclosed grace window, every restoration contribution bound, and the full 202c catalogue against a two-cycle common-forage budget.

Expected result: `ECONOMY & PROGRESSION — PASS: 21 species preserve Theo's market premium; 12 recurring jobs never underpay direct-sale opportunity cost; the 144c tax is reachable from first-payment cash plus one common-forage population and discloses a full grace night; seven restorations total 202c, within two common-forage cycles.`

## Focused narrative-copy verifier

Call `NarrativeCopyVerifier.RunAll()` in Edit or Play Mode. It loads every dialogue, quest, request, location, and story moment and rejects blank/whitespace-damaged text, malformed punctuation spacing, common production typos, copy outside authored UI length budgets, oversized choice sets, and accidental unvoiced same-speaker split-clicks. Separate same-speaker beats remain valid when both carry authored VO and therefore represent intentional dramatic cadence.

Expected result: `NARRATIVE COPY — PASS: 145 dialogues, 26 quest cards, 16 village requests, 15 location toasts, and 28 cinematic moments are clean, bounded, and advance-efficient.`

## NOT covered (known gaps — schedule, don't forget)

- **Scene-component config beyond the foundation**: the focused verifier covers wild-node ids and rest/grow-bed presence, but DayFlagScheduler parallel arrays, arbitrary trigger placement, and most scene wiring still need a broader scene-validation pass.
- **Quest completion behavior**: story/world alignment now proves the exact chain, registered destinations, and multi-stage routing, but it does not physically perform every one of the 26 completion interactions. Full completion remains the per-batch stepped play harness.
- **Playtime cadence/lifecycle callbacks**: `SaveIntegrityVerifier` proves playtime normalization and disk round-trip, but it does not wait through the 60-second focused timer or synthesize application pause/quit. Verify those hooks in a Play Mode lifecycle check before release.
- **Derived content translation coverage** — journal fields are routed through stable IDs with English SO fallbacks, but the checker does not yet require an English/Chinese LUT row for every derived Story/mushroom/character key.
- **Full controller traversal** — batch-63 live QA asserts explicit navigation, locked-focus skipping, and return focus for the journal family; a device-driven automated traversal is still a candidate PlayMode test.
- **Visual composition** — batch-120 adds the repeatable gate-checked eight-screen 1280×800 baseline; batch-84 adds the in-world Old Wood arrival title; batch-83 adds day/night Crooked Pintle routine evidence; batch-82 adds fixed-view day/dusk/night village evidence plus a ground-level night-practical shot; batch-71 covers Wren's character study; batches 69–70 cover model lighting/controls/framing. Screenshot judgment remains manual rather than tolerant pixel-diff automation.
- **Hardcoded display strings** — needs the dialogue localization restructure first; the linter can't distinguish display strings from ids reliably today.
- **False-confidence audit**: every ~10 batches, re-verify checks still catch planted faults. The isolated save verifier now owns parseable corruption, checksum tamper, temp/backup revision choice, future-version refusal, and recovered-rewrite quarantine.
