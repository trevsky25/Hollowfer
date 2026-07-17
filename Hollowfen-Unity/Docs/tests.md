# Tests & Verification Manifest
What every automated check in this project actually proves, how to run it, and what is NOT covered. Three layers: gotcha lint (filesystem, no Unity), data integrity (asset layer, via the editor), play-mode smoke (runtime boot health).
Run: `tools/agent/lint_hollowfen.py` (always) · `tools/agent/run_integrity.py` or menu "Hollowfen/Data Integrity Report" (Unity open) · `tools/agent/smoke_play.py` (Unity open + visible). Pre-commit runs lint always and integrity when the bridge is up (`git config core.hooksPath .githooks`, set per clone).
Checker code: `Assets/_Hollowfen/Scripts/Editor/DataIntegrity.cs` — an editor utility, NOT a Unity Test Framework assembly, because game code compiles into Assembly-CSharp (coupled to no-asmdef third-party sources) and test assemblies can't reference it.
Philosophy: checks target failures that are SILENT at runtime (Localization.Get returns the raw id on a miss; PickDialog skips null entries; extra relationship ids are ignored). Loud failures don't need tests — the console already catches them.
Waiver policy: lint waivers in `tools/agent/lint_waivers.txt`, each pointing at the TODOS item that owns the fix. A waiver is a debt marker, not a dismissal.
Status: all three layers verified 2026-07-16. Focused verifiers cover the ending engine, repeatable gameplay foundation plus purse quote/ledger persistence, village requests, day/night lighting, time-aware NPC schedules, regional world feedback, complete gameplay audio/voice coverage, and the active production UI presentation; save-mutating runs back up and byte-verify restoration of the tester's real save slots.

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
| `npc-id` / `npc-entries` | ERROR/WARN/INFO | Ids lowercase/unique/canon; no unconditional entry shadowing later entries (PickDialog is first-match-wins); null-dialog entries flagged; missing repeat dialog is INFO (intentional for quest-window NPCs). |
| `scorehooks` | ERROR | `ScoreHooks.QuestFlags`/`SpeciesFlags` magic-string keys match real asset ids (hand-synced today). |
| `database` | ERROR | Story-card/mushroom databases contain exactly 30/21 entries, with no nulls or duplicate ids. |
| `journal-data` | ERROR | Required Story, mushroom, and Wren profile fields are populated; journal screens cannot silently render structurally blank cards or sections. |
| `journal-art` | ERROR/INFO | All 30 Story images, the Wren hero, and all five character plates exist. A missing mushroom photo is reported as INFO because the Field Guide has an intentional missing-sketch state. |
| `journal-model` / `mushroom-model` | ERROR/INFO | Every species in `MushroomModelManifest.json` has both prefabs assigned; each world prefab owns a `MushroomNode` pointing back to that exact species SO; each SO's preview exposure matches its model manifest value. Wren's profile must hold its dedicated preview prefab, breathing idle, and supported exposure. The report states current mushroom 3D coverage (20/21; Aldermark pending by design). `JournalPreview` must also exist as an isolated camera/light layer. |
| `mushroom-gameplay` | ERROR/INFO | All 21 species have a respawn/economy/progression profile; dangerous/psychoactive stock has no ordinary buyer value; safe stock has a buyer; gated tiers declare flags; cultivable species have world prefabs; at least three cultivation recipes exist. Repeat basket-sale dialogues must name a real buyer rather than silently using flat pricing. |
| `village-request` | ERROR | Unique request/NPC IDs; localized copy + art; 1–4 aligned positive safe-species requirements; valid quest gates/outcomes; recurring/story outcome separation; ≥3 rotations per Marra/Edda/Theo; exactly one gathering; runtime database completeness. |
| `ending-*` | ERROR | Exactly four canonical endings; unique IDs/flags/cards; populated dialogue/art/epilogue/achievement; valid relationship arrays and final-choice gate; choice-only card unlocks; all four wired once into Aldric's meeting. |
| `build-settings` | ERROR | `Scene_MainMenu` enabled at index 0; `Scene_Hollowfen` enabled. |
| `coverage` | INFO | Counts of assets checked — no silent scope shrinkage. |

## Layer 3 — Play-mode smoke (`tools/agent/smoke_play.py`)

Activates Unity (macOS App Nap freezes the player loop when the app is hidden — `PlayModeBackgroundTicker` can't tick a napped app), enters Play mode, requires ≥240 frames, asserts **no new console errors**, samples quest/clock state, exits. The bridge reader retries transient boolean acknowledgements during script reload instead of mis-parsing them as console counts, and applies a short per-call deadline so a stale SSE response cannot hang the runner for five minutes. Run at the end of every implementation batch and in night-shift wrap-up.

## Focused ending verifier

After backing up `persistentDataPath/saves`, call `EndingEngineVerifier.RunAll()` through the editor bridge. It proves the pragmatic fallback and exact score boundaries, resolves each of four endings independently, asserts one canonical flag/card/achievement, rejects a second commit, reloads the disk snapshot, and deliberately corrupts the primary to prove `.bak` recovery. Restore the save directory afterward; batch-72 verified all four original slot hashes matched their backups.

## Focused gameplay-foundation verifier

In `Scene_Hollowfen` Play Mode, call `GameplayFoundationVerifier.RunAll()` after backing up the save directory. It proves: at least one unique stable authored wild node per species profile (22 after the festival Lacewig source); the single rest point; non-mutating Marra quotes; Marra/Theo totals and refused-item retention; purse ledger running balances and disk persistence; forage save/respawn; cultivation; and exact `sundown → dawn` event order. Restore saves afterward.

## Focused village-request verifier

After backing up saves, call `VillageRequestVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It proves deterministic dawn rotation across all three NPCs; one claim per NPC/day; atomic requirement consumption and copper; first-only relationship rewards; tracked-order save/expiry; controller action structure and pause restoration; Edda/Theo eligibility; the stable Lacewig source; and festival ingredient → flags → quest/card/next-quest persistence. Restore the save directory afterward.

## Focused day/night-lighting verifier

Call `DayNightLightingVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It samples noon, dusk, and late night and proves a progressive exposure transition; materially darker sky/ambient light; denser night fog; low-intensity cool moonlight; six warm practical lights on at night and disabled at noon; and restoration of the test's original clock value. It does not write a save.

## Focused NPC-schedule verifier

Call `NPCScheduleVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It proves exactly four unique schedule/actor pairs; Theo's hidden, wagon, evening-Pintle, and active-Capital-offer states; the corrected Capital waypoint; story-gated evening unlocks for Joren/Bram/Pell; the exact 18:30 and 07:00 boundaries across midnight; and deferred relocation while the player stands near a destination. It snapshots and restores score, quest, clock, waypoint, and player state and does not write a save.

## Focused world-feedback verifier

Call `WorldFeedbackVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It proves four shared localized regions; six trigger volumes including the southern starting village, clear-cut, and manor; real `RegionChanged` fan-out to ambience/music/toast; four mixer-routed ambience sources; distinct non-clipping 12-second 48 kHz mono day/night samples; noon/night balance; cached regional profile changes; independent Ambience mute; two-bank adaptive music state with regional/night restraint; and non-blocking localized arrival copy. It restores clock, audio preference, manager regions, and toast visibility and does not write a save.

## Focused gameplay-audio verifier

Call `GameplayAudioVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It builds and inspects all 13 gameplay cues for 48 kHz mono, non-silent, non-clipping sample data; proves SFX mixer routing and delivery dispatch; verifies 75 dialogues and all 267 exact index/speaker voice references (107 Wren + 160 supporting cast) at 24 kHz mono; and opens a real Bram conversation to prove the line-show path dispatches its assigned clip. It closes the test dialogue and does not write a save.

## Focused production-UI verifier

While the presentation under test is fully open, run `Tools > Hollowfen > Verify Active UI Presentation`
or call `ProductionUIVerifier.VerifyActiveForAutomation()`. It checks the shared 1920×1080 canvas
contract, invisible raycast blockers, TMP font/readability/clipping, interactive-screen focus, minimum
hit targets, missing UI components, and cutting-shadow containment. Batch 87 exercised the main
menu, all journal pages, save/load/settings/modal flows, gameplay HUD, pause, inventory, inspect,
map, requests, cultivation, NPC dialogue, and the mushroom-cutting kneel at 1280×800; every settled
presentation returned `0 critical / 0 advisory`. This is structural presentation lint, not a pixel-
golden visual test, so screenshots remain part of the handoff evidence.

## NOT covered (known gaps — schedule, don't forget)

- **Scene-component config beyond the foundation**: the focused verifier covers wild-node ids and rest/grow-bed presence, but DayFlagScheduler parallel arrays, arbitrary trigger placement, and most scene wiring still need a broader scene-validation pass.
- **Quest-flow behavior**: the smoke test proves boot health, not that quests COMPLETE. Full-chain verification remains the per-batch play harness (stepped `EditorApplication.Step()` driving).
- **Derived content translation coverage** — journal fields are routed through stable IDs with English SO fallbacks, but the checker does not yet require an English/Chinese LUT row for every derived Story/mushroom/character key.
- **Full controller traversal** — batch-63 live QA asserts explicit navigation, locked-focus skipping, and return focus for the journal family; a device-driven automated traversal is still a candidate PlayMode test.
- **Visual composition** — batch-84 adds the in-world Old Wood arrival title; batch-83 adds day/night Crooked Pintle routine evidence; batch-82 adds fixed-view day/dusk/night village evidence plus a ground-level night-practical shot; batch-71 covers Wren's character study; batches 69–70 cover model lighting/controls/framing. Screenshot judgment remains manual rather than pixel-golden automation.
- **Hardcoded display strings** — needs the dialogue localization restructure first; the linter can't distinguish display strings from ids reliably today.
- **False-confidence audit**: every ~10 batches, re-verify checks still catch planted faults (the corrupt→detect→restore drill from 2026-07-11).
