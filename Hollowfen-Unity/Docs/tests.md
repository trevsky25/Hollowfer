# Tests & Verification Manifest
What every automated check in this project actually proves, how to run it, and what is NOT covered. Three layers: gotcha lint (filesystem, no Unity), data integrity (asset layer, via the editor), play-mode smoke (runtime boot health).
Run: `tools/agent/lint_hollowfen.py` (always) · `tools/agent/run_integrity.py` or menu "Hollowfen/Data Integrity Report" (Unity open) · `tools/agent/smoke_play.py` (Unity open + visible). Pre-commit runs lint always and integrity when the bridge is up (`git config core.hooksPath .githooks`, set per clone).
Checker code: `Assets/_Hollowfen/Scripts/Editor/DataIntegrity.cs` — an editor utility, NOT a Unity Test Framework assembly, because game code compiles into Assembly-CSharp (coupled to no-asmdef third-party sources) and test assemblies can't reference it.
Philosophy: checks target failures that are SILENT at runtime (Localization.Get returns the raw id on a miss; PickDialog skips null entries; extra relationship ids are ignored). Loud failures don't need tests — the console already catches them.
Waiver policy: lint waivers in `tools/agent/lint_waivers.txt`, each pointing at the TODOS item that owns the fix. A waiver is a debt marker, not a dismissal.
Status: all three layers verified with negative tests 2026-07-11 (planted faults caught, then restored).

> Self-healing doc: adding a check? Document it here. Hitting a new failure class? Add a check AND a row here in the same batch.

---

## Layer 1 — Gotcha lint (`tools/agent/lint_hollowfen.py`)

No Unity required; runs in every pre-commit. Scans `Assets/_Hollowfen` only (never third-party).

| Rule | Severity | Proves |
|---|---|---|
| `legacy-input` | ERROR | No `UnityEngine.Input.*` / `Input.GetKey…` — new Input System only (Steam Deck cert). |
| `datapath-save` | ERROR | No `Application.dataPath` — saves must be persistentDataPath (Steam Cloud). |
| `emoji` | ERROR | No emoji in dialogue/story-card assets (canon voice rule). |
| `missing-meta` | ERROR | Every file/folder has its `.meta`, no orphans — prevents broken GUID refs on other machines. |
| `public-field` | WARN | No public mutable fields on MonoBehaviour/ScriptableObject classes (data structs/DTOs exempt). Heuristic — treat as a nudge. |

## Layer 2 — Data integrity (`DataIntegrity.RunAll`, asset layer)

Runs inside the editor (menu / bridge / batchmode `-executeMethod …DataIntegrity.RunCLI`). Categories:

| Category | Severity | Proves |
|---|---|---|
| `quest-id` | ERROR | Quest ids non-empty + unique. |
| `quest-chain` | ERROR/WARN | No `NextQuest` cycles; exactly one chain root; no unreachable quests. |
| `localization` | ERROR | Every id CONSUMED by live UI (quest name/objective, NPC names, location name/desc, prompt verbs) exists in `Localization._table` — misses render the raw id silently. |
| `relationships` | ERROR | Parallel id/delta arrays same length; every id is a bible cast id (catches typos — extras are silently ignored at runtime). |
| `dialogue-lines` | ERROR | Every dialogue has lines; no empty speaker/text. |
| `dialogue-speaker` | WARN | Speaker exists in `DialogueScreen.SpeakerColors` (else silent default-ink fallback). |
| `dialogue-chain` | ERROR | No `NextDialog` cycles (a cycle traps the player at timeScale 0). |
| `dialogue-flags/quest` | ERROR/WARN | No empty flag ids; CompleteQuest targets live under Data/Quests. |
| `npc-id` / `npc-entries` | ERROR/WARN/INFO | Ids lowercase/unique/canon; no unconditional entry shadowing later entries (PickDialog is first-match-wins); null-dialog entries flagged; missing repeat dialog is INFO (intentional for quest-window NPCs). |
| `scorehooks` | ERROR | `ScoreHooks.QuestFlags`/`SpeciesFlags` magic-string keys match real asset ids (hand-synced today). |
| `database` | ERROR | Story-card/mushroom databases: no null entries, no duplicate ids. |
| `build-settings` | ERROR | `Scene_MainMenu` enabled at index 0; `Scene_Hollowfen` enabled. |
| `coverage` | INFO | Counts of assets checked — no silent scope shrinkage. |

## Layer 3 — Play-mode smoke (`tools/agent/smoke_play.py`)

Activates Unity (macOS App Nap freezes the player loop when the app is hidden — `PlayModeBackgroundTicker` can't tick a napped app), enters Play mode, requires ≥240 frames, asserts **no new console errors**, samples quest/clock state, exits. Run at the end of every implementation batch and in night-shift wrap-up.

## NOT covered (known gaps — schedule, don't forget)

- **Scene-component config**: `DayFlagScheduler` parallel arrays, trigger placement, missing scene wiring — asset-layer checks can't see scene state. Candidate: scene-validation pass via the bridge (Phase 3+).
- **Quest-flow behavior**: the smoke test proves boot health, not that quests COMPLETE. Full-chain verification remains the per-batch play harness (stepped `EditorApplication.Step()` driving).
- **Story-card `unlockAt` semantics** (incl. the known shared `unlockAt: 26` on ending cards 27–30) — owned by the ending-engine TODOS item.
- **Stamped-but-unwired menu localization ids** (story/mushroom pages) — becomes a check when the localization wiring pass lands.
- **Hardcoded display strings** — needs the dialogue localization restructure first; the linter can't distinguish display strings from ids reliably today.
- **False-confidence audit**: every ~10 batches, re-verify checks still catch planted faults (the corrupt→detect→restore drill from 2026-07-11).
