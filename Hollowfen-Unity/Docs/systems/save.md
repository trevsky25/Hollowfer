# Save System
One JSON file per slot (`persistentDataPath/saves/slot{N}.json`, 4 slots, JsonUtility of a single flat `SaveSlotMeta`); `SaveManager` (static) owns slot IO + targeted `AutoSave*` writers, `SaveCoordinator` (static) orchestrates full save/load across all game systems, `PlayerSpawnRestorer` restores Wren's transform on scene load.
Key scripts: `Assets/_Hollowfen/Scripts/Save/` — SaveManager, SaveSlotMeta, SaveCoordinator, PlayerSpawnRestorer (namespace `Hollowfen.Save`); slot-picker UI: `Assets/_Hollowfen/Scripts/UI/SaveSlotScreen.cs`.
Save triggers: pause-menu Save, story-beat checkpoints, natural dawn, confirmed rest, and request delivery — all `SaveAllWithPlayer()`; plus per-change targeted autosaves from 11 systems (including village requests).
Persistence split: ALL per-playthrough state in slot JSON; PlayerPrefs holds only device settings + `forage.firstHarvestSeen` (`forage.discoveredIds` is legacy — migrated into the slot then deleted).
Biggest gotchas: `AutoSave*` writes target `ActiveSlot`, NOT slot 0; `CurrentQuest` remains cached display text for the slot row even though `CurrentQuestId` now carries the stable ID; `SaveAllWithPlayer` finds Wren via `GameObject.Find("PlayerArmature")` and silently degrades if renamed.
Status: ending state, purse transaction history, wild-forage lifecycle round-trip, and crash-safe atomic replacement verified through 2026-07-16, including corrupt-primary recovery and byte-for-byte restoration of the tester's four real slots.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## SaveManager (static) — API

Slot layout: `TotalSlots = 4`, `AutosaveSlot = 0` (naming is misleading — see gotchas). `ActiveSlot` (default 0) is the slot the session reads/writes; New Game / Load switch it via `SetActiveSlot`. Format: `JsonUtility.ToJson(meta, prettyPrint: true)` — whole slot is one `SaveSlotMeta` JSON file at `Application.persistentDataPath/saves/slot{N}.json`.

| Member | Behavior |
|---|---|
| `SetActiveSlot(int)` | Clamps to `[0,3]`, sets `ActiveSlot`. |
| `SlotHasData(int)` / `GetSlotMeta(int)` / `DeleteSlot(int)` | Primary-or-backup exists / deserialize primary then recover `.bak` on missing/corrupt / delete primary, backup, and temp. |
| `WritePlaceholderToSlot(int)` | Fresh meta (`CurrentQuest="Act I — Arrival"`, `CurrentAct=1`) so New Game shows a slot row immediately. |
| `WriteSlot(int, meta)` | Full write; forces `SlotNumber`; backfills `TimestampUnix` only if 0. Used by `SaveCoordinator.SaveAll`. |
| `AutoSaveInventory / AutoSaveCoins / AutoSaveQuestState / AutoSaveKeyItems / AutoSaveScores / AutoSaveDiscoveredLocations / AutoSaveGrowBeds / AutoSaveForageNodes / AutoSaveVillageRequests / AutoSaveDiscovery / AutoSaveIntroSeen` | All follow the same recipe: read `ActiveSlot` meta (or default) → overwrite one field → refresh timestamp → write back. `AutoSaveCoins` writes both total copper and its newest-first ledger snapshot; `AutoSaveScores` pulls values itself via `GameScores.WriteTo(meta)`. |
| `ResetOnLoad` (private, `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`) | Resets `ActiveSlot=0` on domain load — guards Enter-Play-Mode-without-domain-reload staleness. |

Every full or targeted writer funnels through `WriteJsonAtomically`: write + flush `slotN.json.tmp`, replace the primary while retaining `slotN.json.bak`, then clean the temp in `finally`. A platform without `File.Replace` uses a copy/delete/move fallback. The previous complete snapshot is therefore recoverable after an interrupted/corrupt primary.

## Save-slot picker

Main Menu `Forage` pushes `screenId="save-slot"` over `main-menu`. The four scene-authored journal rows are refreshed on every open; selecting a populated row loads it and selecting an empty row starts a new game. The shared top-right close control and `UI/Cancel` both call `UIManager.Back()`, revealing Main Menu rather than replacing or reloading it. Explicit navigation links `CloseButton → Slot0 → Slot1 → Slot2 → Slot3`, with Up from `Slot0` returning to Close.

## SaveSlotMeta — full schema (flat, JsonUtility)

- **Slot meta**: `SlotNumber`, `TimestampUnix` (UTC), `CurrentQuest` (cached display string), `CurrentQuestId` (stable ID; null on legacy saves), `CurrentAct`, `TotalPlayTimeSeconds`. A completed ending writes `game_complete`, Act 4, and localized completed copy.
- **Game state**: `Inventory` (`InventorySnapshot{Ids[],Counts[]}`), `KeyItemIds[]` (e.g. `"item.mill_key"`), `CompletedQuestIds[]`, `UnlockedStoryCardIds[]`, `CoinsCopper` (12 copper = 1 silver), `CoinLedger` (`CoinLedgerSnapshot{AmountsCopper[],BalancesAfterCopper[],ReasonIds[]}`, newest first, max 8), `HomecomingIntroSeen`, `DiscoveredMushroomIds[]`, `DiscoveredLocationIds[]`.
- **Player transform**: `HasPlayerTransform`, `PlayerPosX/Y/Z`, `PlayerYaw` (pitch/roll discarded).
- **Scores (ending gates)**: `VillageHope`, `Knowledge`, `RelationshipNpcIds[]`+`RelationshipValues[]`, `GameFlagIds[]`.
- **Clock**: `GameDay` (0 = legacy save → treated as day 1), `GameHour`.
- **Cultivation**: `GrowBeds` (`GrowBedSnapshot{Ids[],SpeciesIds[],PlantedDays[],PlantedHours[],Remaining[]}`) — growth derives from planted day/hour vs clock; **no timer state saved**.
- **Wild forage ecology**: `ForageNodes` (`ForageNodeSnapshot{Ids[],HarvestedDays[]}`) — only harvested scene-node ids and cut days are stored; availability derives from the current day and each species' respawn profile.
- **Village requests**: `VillageRequests` (`VillageRequestSnapshot`) — completed one-shot IDs; daily NPC/request/day parallel arrays; tracked request ID/day. First-completion relationship guards live in `GameFlagIds`.

Parallel arrays everywhere because JsonUtility can't do dictionaries — keep them index-aligned.

## SaveCoordinator (static) — orchestration

- **`StartNewGame(slot)`**: SetActiveSlot → DeleteSlot → reset/null-hydrate every store, including wild forage and VillageRequests → WritePlaceholderToSlot.
- **`LoadSlot(slot)`**: SetActiveSlot → GetSlotMeta → QuestManager reset+hydrate FIRST (deliberate) → hydrate the other stores, including coin balance/history, wild forage, and VillageRequests (all null-tolerant). ⚠️ TimeManager is NOT hydrated here — it self-hydrates on scene load (asymmetric with SaveAll).
- **`MostRecentSlot()`**: max `TimestampUnix` across slots, `-1` if none. Backs "Continue".
- **`SaveAll(playerPosition?, playerYaw)`**: read-modify-write on ActiveSlot — captures all stores, scores, and clock; records display copy + stable active quest ID, or `game_complete`/Act 4 after an ending; preserves the old transform when none is supplied; atomically writes the slot.
- **`SaveAllWithPlayer()`**: `GameObject.Find("PlayerArmature")` → SaveAll with position + yaw; falls back to `SaveAll()` if not found.

**Save triggers**: pause menu (`PauseScreen`), story-beat checkpoints (`StoryBeats`), natural day boundary, `RestSpot` completion, and successful village delivery → full `SaveAllWithPlayer`. Targeted autosaves also fire from the owning stores, including `VillageRequests` for tracking/claims.

## PlayerSpawnRestorer

On `PlayerArmature`, `Start()`: reads `GetSlotMeta(ActiveSlot)`; bails without `HasPlayerTransform` (new game → authored spawn). Otherwise **disables `CharacterController`**, sets position + yaw, re-enables — the CC toggle is mandatory (CC overrides transform writes while enabled). Implicit contract: `LoadSlot` must run (menu-side) before the gameplay scene loads; the restorer only reads the file.

## Persistence split

**Slot JSON**: all per-playthrough state (schema above).
**PlayerPrefs** (device-level only): `audio.master/music/sfx`, `graphics.fullscreen/resolutionIndex/qualityIndex`, `controls.lookSensitivity`, `forage.firstHarvestSeen` (one-shot tutorial, deliberately per-device). `forage.discoveredIds` is LEGACY — read once, migrated into `DiscoveredMushroomIds`, then deleted.

Rule: game state → slot JSON via SaveCoordinator; device/user prefs → PlayerPrefs. Every new piece of persistent state gets: a SaveSlotMeta field + capture in `SaveAll` + hydrate in `LoadSlot` + reset in `StartNewGame` + (optionally) a targeted `AutoSave*`, then a save→reload round-trip verification.

## Gotchas

- **"Autosave slot" drift**: after loading slot 2, autosaves land in slot 2 — `AutosaveSlot = 0` is only the default. UI copy should say "3 manual + default".
- **`CurrentQuest` display cache remains localized at save time** — the new `CurrentQuestId` stops new logic from depending on that text, but SaveSlotScreen still renders the cached text until the localization migration.
- **`GameObject.Find("PlayerArmature")`** — rename the armature and saves silently lose the transform (Continue spawns at authored position, no error).
- **Read-modify-write, last-write-wins per file** — safe for fields (each writer re-reads from disk) but an autosave storm = N full file IO cycles; throttling is the caller's job.
- **Editor**: `ResetOnLoad` guards disabled-domain-reload staleness in Play mode.
- Dev reset: delete `saves/slot0.json` (or all slots) + `PlayerPrefs.DeleteAll()`.
