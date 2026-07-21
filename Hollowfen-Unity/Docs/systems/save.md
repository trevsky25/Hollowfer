# Save System
One versioned, checksummed JSON envelope per slot (`persistentDataPath/saves/slot{N}.json`, 4 slots), with `.tmp` and `.bak` recovery candidates; its `PayloadJson` is the flat `SaveSlotMeta` serialized by `JsonUtility`.
Key scripts: `Assets/_Hollowfen/Scripts/Save/` — SaveFileFormat, SaveManager, SaveSlotMeta, SaveCoordinator, PlayerSpawnRestorer (namespace `Hollowfen.Save`); slot-picker UI: `Assets/_Hollowfen/Scripts/UI/SaveSlotScreen.cs`.
Save triggers: pause-menu Save, story-beat checkpoints, natural dawn, confirmed rest, and request delivery call `SaveAllWithPlayer()`; owning systems write targeted snapshots, while TimeManager writes clock + focused playtime every 60 seconds and on application pause/quit.
Persistence split: ALL per-playthrough state lives in the slot envelope, including Living Restoration project stages; PlayerPrefs holds only device settings + `forage.firstHarvestSeen` (`forage.discoveredIds` is legacy — migrated into the slot then deleted).
Biggest gotchas: `AutoSave*` targets `ActiveSlot`, NOT slot 0; corrupt/incompatible journals are never treated as empty or overwritten by targeted autosave; `CurrentQuest` is a read-only compatibility field for id-less historical journals, while all current identity/rendering comes from `CurrentQuestId`; `SaveAllWithPlayer` still finds Wren by `GameObject.Find("PlayerArmature")`.
Status: schema-0 migration, schema-1 recovery, authoritative quest-ID presentation, future-version refusal, normalization, quarantine, full round-trip/rewrite, cross-system village-delivery rollback, per-species field notes, restoration-project migration/round-trip, and atomic restoration funding rollback are covered through 2026-07-21.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## SaveManager (static) — API

Slot layout: `TotalSlots = 4`, `AutosaveSlot = 0` (naming is misleading — see gotchas). `ActiveSlot` (default 0) is the slot the session reads/writes; New Game / successful Load switch it via `SetActiveSlot`.

| Member | Behavior |
|---|---|
| `SetActiveSlot(int)` | Clamps to `[0,3]`, sets `ActiveSlot`. |
| `InspectSlot(int)` | Reads primary, flushed temp, and backup; validates format/checksum/semantics; selects the highest valid revision (tie order: primary → temp → backup); returns `Empty`, `Ready`, `Recovered`, `Corrupt`, or `IncompatibleNewerVersion`. Only Ready/Recovered can load. |
| `SlotHasData(int)` / `GetSlotMeta(int)` | Reports any slot artifacts, including corrupt/incompatible data / returns only a loadable inspected payload and logs a recovered-source warning once. A damaged file is therefore not mistaken for an empty New Game slot. |
| `DeleteSlot(int)` | Deletes primary, backup, temp, and that slot's `.corrupt-*` quarantine files. This is the canonical reset path. |
| `WritePlaceholderToSlot(int)` | Fresh meta (`CurrentQuestId="arrive"`, `CurrentAct=1`) so New Game shows a slot row immediately without persisting localized display copy. |
| `WriteSlot(int, meta)` | Full write; centrally forces the path's `SlotNumber`, current UTC timestamp, next monotonic revision, and quest-identity normalization before encoding the schema-1 envelope. A non-empty `CurrentQuestId` strips the obsolete display cache. Used by `SaveCoordinator.SaveAll`. During an active atomic transaction it deep-clones/stages the latest full payload instead of touching disk. |
| Targeted `AutoSave*` writers | Inspect `ActiveSlot`, mutate one field on a valid payload, then atomically rewrite it. They refuse Empty, Corrupt, or Incompatible slots instead of synthesizing a blank save. `AutoSaveCoins` includes its ledger; `AutoSaveScores` calls `GameScores.WriteTo`; `AutoSaveRestorationProjects` stores monotonic project stages/day records; `AutoSaveClockAndPlaytime` stores day/hour/playtime. |
| Internal atomic transaction API | `TryBeginAtomicTransaction` requires a loadable active slot; targeted writes are suppressed and `PublishAfterAtomicCommit` queues outward callbacks. `TryCommitAtomicTransaction` writes the staged full payload and verifies its canonical decoded payload + higher revision before releasing callbacks. False leaves the transaction active so its owner can rollback silently; `CancelAtomicTransaction` discards callbacks. Village deliveries and restoration contributions both use this boundary so inventory/coins, flags, project stages, revisions, and outward events either commit together or roll back together. |
| Editor isolation/fault hooks | `EditorSaveDirectoryOverride` keeps verifiers away from real journals. `EditorRejectNextAtomicCommit` rejects the final staged write before disk IO; the verifier-only cancel hook prevents leaked test state. None ship as player behavior. |
| `ResetOnLoad` (private, `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`) | Resets `ActiveSlot=0`, recovery-notice de-duplication, and the editor directory override — guards domain-reload-off staleness. |

## Schema-1 envelope, validation, and recovery

The current file is a `SaveFileEnvelope`: `FormatId="hollowfen.save"`, `SchemaVersion=1`, positive `Revision`, `WrittenUnixMilliseconds`, escaped `PayloadJson`, and `IntegritySha256`. The checksum covers schema version, revision, written time, and the exact payload. Files over 16 MiB, empty/malformed payloads, bad checksums, invalid envelope metadata, and unrecognized formats are invalid candidates.

Historical flat `SaveSlotMeta` JSON is schema 0. It remains loadable when at least two original anchors are recognizable (`SlotNumber`, `TimestampUnix`, `CurrentQuest`, `CurrentAct`); parseable garbage such as `{}` is rejected. Inspection does not rewrite it. Its first successful save emits schema 1 and rotates the historical primary to `.bak`.

Inspection considers `slotN.json`, `slotN.json.tmp`, and `slotN.json.bak`. The highest valid revision wins, with primary preferred over temp and backup at equal revision. Revision—not the wall clock—decides crash recovery, so a fully flushed temp can recover an interrupted replacement. If the authoritative candidate uses a newer schema, the slot is `IncompatibleNewerVersion`: it is shown but neither loaded nor downgraded onto an older backup.

Every writer funnels through `WriteJsonAtomically`: encode the next revision, write and force-flush `.tmp`, then replace the primary while retaining `.bak`; platforms without `File.Replace` use the copy/delete/move fallback. Temp is deleted only after a successful commit, so a failed/interrupted replacement leaves it as a valid candidate. When an explicit rewrite recovers around an invalid primary, that primary is preserved as `slotN.json.corrupt-YYYYMMDD-HHMMSS[-N]` rather than rotated over the good backup.

Village delivery is the first cross-store transaction. While it is open, inventory, requests, purse, scores, quest/card state, waypoint changes, and Steam-style achievement fan-out mutate/stage synchronously but cannot publish or write intermediate revisions. `SaveCoordinator.SaveAllWithPlayer` supplies the final payload; commit requires the exact normalized staged payload to decode as a higher-revision winner. A late replacement exception is still success when the force-flushed temp proves that payload won. A true failure restores every mutated runtime store before canceling the queued callbacks, so neither UI notifications nor irreversible achievement dispatch can escape a rejected commit.

Load-time normalization treats repairable metadata as recoverable rather than discarding the journal: it repairs implausible timestamps from file time, clamps act/playtime/non-negative counters/day/hour, rejects non-finite player transforms by clearing `HasPlayerTransform`, aligns parallel arrays to their shared length, and caps collection rows at 4,096. Total playtime is capped at ten years. Narrative IDs are preserved; the format layer does not invent story-state repairs.

## Save-slot picker

Main Menu `Forage` pushes `screenId="save-slot"` over `main-menu`. The four scene-authored journal rows inspect their slot on every open. `SaveQuestIdentity` resolves canonical quest IDs through `quest.<id>.name`, handles the two ending states plus the Act-I-complete sentinel explicitly, and shows `Unknown chapter` for a non-empty unrecognized ID instead of trusting stale text. Only an id-less schema-zero journal may render its historical `CurrentQuest` cache. Empty starts New Game; Ready/Recovered loads; Corrupt reports `Damaged journal`; Incompatible reports `Journal requires a newer game version`. Selecting either blocked state shows `Journal Unavailable`, leaves the active session/menu untouched, and never opens gameplay; Delete remains the explicit removal path. Out-of-range dates render `Unknown date` instead of throwing. The shared top-right close control and `UI/Cancel` both call `UIManager.Back()`, revealing Main Menu rather than replacing or reloading it.

## SaveSlotMeta — schema-1 payload (flat JsonUtility inside the envelope)

- **Slot meta**: `SlotNumber`, `TimestampUnix` (UTC seconds, stamped centrally), `CurrentQuestId` (authoritative stable ID), `CurrentAct`, `TotalPlayTimeSeconds`, plus `CurrentQuest` only as a read-only compatibility field for id-less historical journals. Current writers clear that cache as soon as an ID exists. A completed ending writes `game_complete` / Act 4; the recoverable pre-choice fork writes `final_choice_available` / Act 4.
- **Game state**: `Inventory` (`InventorySnapshot{Ids[],Counts[]}`), `KeyItemIds[]` (e.g. `"item.mill_key"`), `CompletedQuestIds[]`, `UnlockedStoryCardIds[]`, `CoinsCopper` (12 copper = 1 silver), `CoinLedger` (`CoinLedgerSnapshot{AmountsCopper[],BalancesAfterCopper[],ReasonIds[]}`, newest first, max 8), `HomecomingIntroSeen`, `DiscoveredMushroomIds[]`, `DiscoveredLocationIds[]`.
- **Player transform**: `HasPlayerTransform`, `PlayerPosX/Y/Z`, `PlayerYaw` (pitch/roll discarded).
- **Scores (ending gates + compact facts)**: `VillageHope`, `Knowledge`, `RelationshipNpcIds[]`+`RelationshipValues[]`, `GameFlagIds[]`. Mushroom field records encode their stable first-verification context as `mushroom_field_note|<speciesId>|<day>|<regionId>|<locationId>`; display text is resolved later and is never saved.
- **Clock**: `GameDay` (0 = legacy save → treated as day 1), `GameHour`.
- **Cultivation**: `GrowBeds` (`GrowBedSnapshot{Ids[],SpeciesIds[],PlantedDays[],PlantedHours[],Remaining[]}`) — growth derives from planted day/hour vs clock; **no timer state saved**.
- **Wild forage ecology**: `ForageNodes` (`ForageNodeSnapshot{Ids[],HarvestedDays[]}`) — only harvested scene-node ids and cut days are stored; availability derives from the current day and each species' respawn profile.
- **Village requests**: `VillageRequests` (`VillageRequestSnapshot`) — completed one-shot IDs; daily NPC/request/day parallel arrays; tracked request ID/day. First-completion relationship guards live in `GameFlagIds`.
- **Living restoration**: `RestorationProjects` (`RestorationSnapshot{ProjectIds[],Stages[],StartedDays[],ChangedDays[]}`) — one monotonic row per authored project. Quest/flag rules migrate legacy saves upward; load normalization clamps rows to `Unavailable..Occupied` and non-negative days.

Parallel arrays everywhere because JsonUtility can't do dictionaries — keep them index-aligned.

## SaveCoordinator (static) — orchestration

- **`StartNewGame(slot)`**: SetActiveSlot → DeleteSlot → reset/null-hydrate every store, including wild forage, VillageRequests, and RestorationProjects → WritePlaceholderToSlot.
- **`TryLoadSlot(slot, out inspection)`**: inspect first; if blocked, return false without changing `ActiveSlot` or any in-memory store. If loadable, SetActiveSlot → QuestManager reset+hydrate FIRST (deliberate) → hydrate the other stores, including coin balance/history, wild forage, VillageRequests, and RestorationProjects after GameScores. The old `LoadSlot` entry point delegates here and logs refusal. ⚠️ TimeManager is NOT hydrated here — it self-hydrates on scene load.
- **`MostRecentSlot()`**: max normalized `TimestampUnix` across loadable inspections only, `-1` if none. Backs "Continue".
- **`SaveAll(playerPosition?, playerYaw)`**: read-modify-write on ActiveSlot — captures all stores (including restoration stage/day records), scores, and clock; records only the stable active quest ID, or `game_complete`/Act 4 after an ending; preserves the old transform when none is supplied; atomically writes the slot.
- **`SaveAllWithPlayer()`**: `GameObject.Find("PlayerArmature")` → SaveAll with position + yaw; falls back to `SaveAll()` if not found.

**Save triggers**: pause menu (`PauseScreen`), story-beat checkpoints (`StoryBeats`), natural day boundary, `RestSpot` completion, and successful village delivery → full `SaveAllWithPlayer`. Targeted autosaves also fire from the owning stores, including `VillageRequests` for tracking/claims.

## Clock and playtime

`TimeManager` hydrates `TotalPlayTimeSeconds` with the game clock on scene load. While the application is focused, `Update` accumulates clamped `Time.unscaledDeltaTime` (maximum one second per frame), so reading dialogue and pause-menu time count while background/suspend spikes do not. It calls `AutoSaveClockAndPlaytime` every 60 accumulated seconds and on application pause/quit; every full `SaveAll` also captures the value through `TimeManager.WriteTo`. A blocked/corrupt journal still refuses this targeted write.

## PlayerSpawnRestorer

On `PlayerArmature`, `Start()`: reads `GetSlotMeta(ActiveSlot)`; bails without `HasPlayerTransform` (new game → authored spawn). Otherwise **disables `CharacterController`**, sets position + yaw, re-enables — the CC toggle is mandatory (CC overrides transform writes while enabled). Implicit contract: `TryLoadSlot` must succeed menu-side before the gameplay scene loads; the restorer only reads the file.

## Persistence split

**Slot envelope**: all per-playthrough state (schema above).
**PlayerPrefs** (device-level only): `audio.master/music/sfx`, `graphics.fullscreen/resolutionIndex/qualityIndex`, `controls.lookSensitivity`, `accessibility.interfaceScale/reducedMotion/captionBacking`, and `forage.firstHarvestSeen` (one-shot tutorial, deliberately per-device). `forage.discoveredIds` is LEGACY — read once, migrated into `DiscoveredMushroomIds`, then deleted.

Rule: game state → slot payload via SaveCoordinator; device/user prefs → PlayerPrefs. Every new piece of persistent state gets: a SaveSlotMeta field + capture in `SaveAll` + hydrate after `TryLoadSlot` + reset in `StartNewGame` + (optionally) a targeted `AutoSave*`, then a save→reload round-trip verification. Any incompatible payload change also requires a schema-version/migration decision; an older executable must never write a newer envelope.

## Gotchas

- **"Autosave slot" drift**: after loading slot 2, autosaves land in slot 2 — `AutosaveSlot = 0` is only the default. UI copy should say "3 manual + default".
- **Artifacts are not emptiness**: primary/temp/backup can represent a damaged journal. Only `InspectSlot(...).Status == Empty` authorizes New Game; `.corrupt-*` files are retained as support evidence but are not recovery candidates. Use `DeleteSlot` for an intentional reset and cleanup.
- **Historical `CurrentQuest` is compatibility-only** — do not write or branch on it. Id-less schema-zero journals may display it until gameplay performs a full save; any non-empty `CurrentQuestId` is authoritative and clears the cache on write.
- **`GameObject.Find("PlayerArmature")`** — rename the armature and saves silently lose the transform (Continue spawns at authored position, no error).
- **Read-modify-write, last-write-wins per slot** — each targeted writer re-inspects disk and refuses blocked state, but an autosave storm still means N full envelope IO cycles; throttling is the caller's job.
- **Revision is per journal**: it orders primary/temp/backup recovery for one slot; `TimestampUnix` remains the cross-slot recency/display field.
- **Editor**: `ResetOnLoad` guards disabled-domain-reload staleness in Play mode.
- **Encoded field-note flags are append-only save facts.** `MushroomFieldNotes` records at most one per species, tolerates missing context, and treats a verified species without one as a legacy record. Do not overwrite it on repeat inspection or replace stable IDs with localized place names.
- Dev reset: call `SaveManager.DeleteSlot(slot)` or remove that slot's `.json`, `.tmp`, `.bak`, and `.corrupt-*` files together; deleting only the primary intentionally no longer empties the slot. Clear PlayerPrefs separately only when device settings/tutorial state must also reset.
