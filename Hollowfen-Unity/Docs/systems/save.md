# Save System
One JSON file per slot (`persistentDataPath/saves/slot{N}.json`, 4 slots, JsonUtility of a single flat `SaveSlotMeta`); `SaveManager` (static) owns slot IO + targeted `AutoSave*` writers, `SaveCoordinator` (static) orchestrates full save/load across all game systems, `PlayerSpawnRestorer` restores Wren's transform on scene load.
Key scripts: `Assets/_Hollowfen/Scripts/Save/` — SaveManager, SaveSlotMeta, SaveCoordinator, PlayerSpawnRestorer (namespace `Hollowfen.Save`).
Save triggers: pause-menu Save, story-beat checkpoints (`StoryBeats`), day-boundary (`TimeManager`) — all `SaveAllWithPlayer()`; plus per-change targeted autosaves from 9 systems (inventory, coins, quests, key items, scores, grow beds, discovery, locations, intro flag).
Persistence split: ALL per-playthrough state in slot JSON; PlayerPrefs holds only device settings + `forage.firstHarvestSeen` (`forage.discoveredIds` is legacy — migrated into the slot then deleted).
Biggest gotchas: `AutoSave*` writes target `ActiveSlot`, NOT slot 0 (slot 0 is just the default); no atomic writes (crash mid-write corrupts the slot); `SaveAllWithPlayer` finds Wren via `GameObject.Find("PlayerArmature")` and silently degrades if renamed.
Status: shipped with Act I (c2b9405), extended Batch 11 (grow beds, clock). Doc verified against code 2026-07-11.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## SaveManager (static) — API

Slot layout: `TotalSlots = 4`, `AutosaveSlot = 0` (naming is misleading — see gotchas). `ActiveSlot` (default 0) is the slot the session reads/writes; New Game / Load switch it via `SetActiveSlot`. Format: `JsonUtility.ToJson(meta, prettyPrint: true)` — whole slot is one `SaveSlotMeta` JSON file at `Application.persistentDataPath/saves/slot{N}.json`.

| Member | Behavior |
|---|---|
| `SetActiveSlot(int)` | Clamps to `[0,3]`, sets `ActiveSlot`. |
| `SlotHasData(int)` / `GetSlotMeta(int)` / `DeleteSlot(int)` | File exists / read+deserialize (null on missing/corrupt, logs) / delete. |
| `WritePlaceholderToSlot(int)` | Fresh meta (`CurrentQuest="Act I — Arrival"`, `CurrentAct=1`) so New Game shows a slot row immediately. |
| `WriteSlot(int, meta)` | Full write; forces `SlotNumber`; backfills `TimestampUnix` only if 0. Used by `SaveCoordinator.SaveAll`. |
| `AutoSaveInventory / AutoSaveCoins / AutoSaveQuestState / AutoSaveKeyItems / AutoSaveScores / AutoSaveDiscoveredLocations / AutoSaveGrowBeds / AutoSaveDiscovery / AutoSaveIntroSeen` | All follow the same recipe: read `ActiveSlot` meta (or default) → overwrite one field → refresh timestamp → write back. `AutoSaveScores` pulls values itself via `GameScores.WriteTo(meta)`. |
| `ResetOnLoad` (private, `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`) | Resets `ActiveSlot=0` on domain load — guards Enter-Play-Mode-without-domain-reload staleness. |

## SaveSlotMeta — full schema (flat, JsonUtility)

- **Slot meta**: `SlotNumber`, `TimestampUnix` (UTC), `CurrentQuest` (⚠️ localized display string, not an ID), `CurrentAct`, `TotalPlayTimeSeconds`.
- **Game state**: `Inventory` (`InventorySnapshot{Ids[],Counts[]}`), `KeyItemIds[]` (e.g. `"item.mill_key"`), `CompletedQuestIds[]`, `UnlockedStoryCardIds[]`, `CoinsCopper` (12 copper = 1 silver), `HomecomingIntroSeen`, `DiscoveredMushroomIds[]`, `DiscoveredLocationIds[]`.
- **Player transform**: `HasPlayerTransform`, `PlayerPosX/Y/Z`, `PlayerYaw` (pitch/roll discarded).
- **Scores (ending gates)**: `VillageHope`, `Knowledge`, `RelationshipNpcIds[]`+`RelationshipValues[]`, `GameFlagIds[]`.
- **Clock**: `GameDay` (0 = legacy save → treated as day 1), `GameHour`.
- **Cultivation**: `GrowBeds` (`GrowBedSnapshot{Ids[],SpeciesIds[],PlantedDays[],PlantedHours[],Remaining[]}`) — growth derives from planted day/hour vs clock; **no timer state saved**.

Parallel arrays everywhere because JsonUtility can't do dictionaries — keep them index-aligned.

## SaveCoordinator (static) — orchestration

- **`StartNewGame(slot)`**: SetActiveSlot → DeleteSlot → reset/null-hydrate every store (`QuestManager.ResetForSlotSwitch`, then HydrateFrom(null) on InventoryRuntime, KeyItems, CoinPurse, MushroomDiscovery, GameScores, GrowBeds, LocationRegistry) → WritePlaceholderToSlot.
- **`LoadSlot(slot)`**: SetActiveSlot → GetSlotMeta → QuestManager reset+hydrate FIRST (deliberate) → hydrate the other stores (all null-tolerant). ⚠️ TimeManager is NOT hydrated here — it self-hydrates on scene load (asymmetric with SaveAll).
- **`MostRecentSlot()`**: max `TimestampUnix` across slots, `-1` if none. Backs "Continue".
- **`SaveAll(playerPosition?, playerYaw)`**: read-modify-write on ActiveSlot — captures snapshots from all stores, `GameScores.WriteTo`, `TimeManager.Instance?.WriteTo` (clock silently skipped in menu context), `CurrentQuest = Localization.Get(ActiveQuest.DisplayNameId)` + act (or literal `"Act I complete"`), player transform only when position passed (menu saves pass null → previous transform preserved), timestamp, `WriteSlot`.
- **`SaveAllWithPlayer()`**: `GameObject.Find("PlayerArmature")` → SaveAll with position + yaw; falls back to `SaveAll()` if not found.

**Save triggers**: pause menu (`PauseScreen`), story-beat checkpoints (`StoryBeats`), day boundary (`TimeManager`) → full `SaveAllWithPlayer`. Targeted autosaves fire on every change from: InventoryRuntime, CoinPurse, QuestManager, KeyItems, GameScores, GrowBeds, MushroomDiscovery, LocationRegistry, StoryBeats(intro).

## PlayerSpawnRestorer

On `PlayerArmature`, `Start()`: reads `GetSlotMeta(ActiveSlot)`; bails without `HasPlayerTransform` (new game → authored spawn). Otherwise **disables `CharacterController`**, sets position + yaw, re-enables — the CC toggle is mandatory (CC overrides transform writes while enabled). Implicit contract: `LoadSlot` must run (menu-side) before the gameplay scene loads; the restorer only reads the file.

## Persistence split

**Slot JSON**: all per-playthrough state (schema above).
**PlayerPrefs** (device-level only): `audio.master/music/sfx`, `graphics.fullscreen/resolutionIndex/qualityIndex`, `controls.lookSensitivity`, `forage.firstHarvestSeen` (one-shot tutorial, deliberately per-device). `forage.discoveredIds` is LEGACY — read once, migrated into `DiscoveredMushroomIds`, then deleted.

Rule: game state → slot JSON via SaveCoordinator; device/user prefs → PlayerPrefs. Every new piece of persistent state gets: a SaveSlotMeta field + capture in `SaveAll` + hydrate in `LoadSlot` + reset in `StartNewGame` + (optionally) a targeted `AutoSave*`, then a save→reload round-trip verification.

## Gotchas

- **No atomic writes** — plain `File.WriteAllText`; crash mid-write corrupts the slot and `GetSlotMeta` reads it as "empty". Hardening item in TODOS.md (temp-file + rename).
- **"Autosave slot" drift**: after loading slot 2, autosaves land in slot 2 — `AutosaveSlot = 0` is only the default. UI copy should say "3 manual + default".
- **`CurrentQuest` is a localized string at save time** — switch language and old slot rows show the old language. Convention violation, plus the default literal is copy-pasted ~10× in SaveManager.
- **`GameObject.Find("PlayerArmature")`** — rename the armature and saves silently lose the transform (Continue spawns at authored position, no error).
- **Read-modify-write, last-write-wins per file** — safe for fields (each writer re-reads from disk) but an autosave storm = N full file IO cycles; throttling is the caller's job.
- **Editor**: `ResetOnLoad` guards disabled-domain-reload staleness in Play mode.
- Dev reset: delete `saves/slot0.json` (or all slots) + `PlayerPrefs.DeleteAll()`.
