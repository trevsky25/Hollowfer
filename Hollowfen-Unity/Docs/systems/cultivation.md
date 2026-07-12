# Cultivation System
Grow beds (Almy's garden, Act II): plant a mushroom from inventory as spawn → nodes grow on the game clock (scale-lerped, non-harvestable) → at maturity the normal foraging path takes over (nodes are standard MushroomNodes). State store `GrowBeds` (static dict keyed by `_bedId`) persists planted day/hour + remaining count — growth RE-DERIVES from the clock, no timers serialized, so in-progress grows survive save/load and time skips fast-forward them automatically.
Key scripts: `Assets/_Hollowfen/Scripts/Cultivation/` — GrowBed (scene component, IInteractable), GrowBeds (static store). Namespace `Hollowfen.Cultivation`.
Unlock gating (in `GrowBed.CanInteract`): `almyTeach` completed, OR active + flag `act2_started` (lesson dialogue must precede planting); plus bed empty + species in inventory. First planting completes `almyTeach`.
Grow recipe lives ON THE SCENE COMPONENT (not the species SO): `_bedId`, `_species`, `_mushroomPrefab`, `_matureGameHours` (6), `_yield` (3), `_clusterRadius`, `_matureScale` (2.2). No recipe asset yet — Tier-1 Wood Ear only for v1.
Biggest gotchas: `GrowthFactor` returns 1 (instantly mature) when no TimeManager in scene; harvest sync is per-frame `activeSelf` POLLING of spawned nodes (anything else deactivating a node reads as a harvest); unlock gating logic is duplicated here, not shared with quest content.
Status: Batch 11, verified against code 2026-07-11.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## GrowBed (scene component)

`[DisallowMultipleComponent]`, implements `IInteractable`. States are DERIVED, no enum:
**Empty** (`GrowBeds.Get(_bedId) == null`, plantable) → **Growing** (record exists, `GrowthFactor < 1`; nodes visible but scaled down, trigger colliders disabled) → **Mature** (`GrowthFactor >= 1`; node triggers enabled → normal inspect/forage flow) → back to **Empty** when the last node is picked.

- **Prompts**: `PromptVerb => "prompt.plant.verb"`, `PromptTarget => Localization.Get("growbed.name")`.
- **Interact** (plant): `InventoryRuntime.Remove(_species, 1)` (one mushroom = spawn) → `GrowBeds.Plant(bedId, speciesId, TimeManager.Day, TimeManager.Hour, _yield)` (fallback day1/12h without TimeManager) → spawn nodes at growth 0 → if `almyTeach` active, `CompleteQuest("almyTeach")` (first planting is the lesson climax).
- **Growth math**: `GrowthFactor = Clamp01(((Day - PlantedDay)*24 + (Hour - PlantedHour)) / _matureGameHours)` — recomputed from the clock each Update; `SetTime` skips mature beds instantly.
- **Visuals**: nodes in a ring (`_clusterRadius` 0.38m, staggered yaw) under `_spawnAnchor`; `localScale = prefabScale × Lerp(0.3, 1, growth) × _matureScale`; re-applied only when growth delta > 0.01.
- **Harvest**: NOT handled here. Mature nodes are standard `MushroomNode`s (Foraging-layer trigger) — inspect → forage → `Harvest()` deactivates the node. GrowBed's Update polls `activeSelf` across `_nodes`, mirrors the live count via `GrowBeds.SetRemaining`, and `ClearBed()` at 0.
- **Restore**: `Start()` reads the store record, respawns `Remaining` nodes; growth re-derives from clock.

## GrowBeds (static store)

`Dictionary<string, BedRecord>` — `BedRecord { SpeciesId, PlantedDay, PlantedHour, Remaining }`. Same persistence recipe as InventoryRuntime/KeyItems: lazy `EnsureHydrated()` from active slot; `Persist()` (→ `SaveManager.AutoSaveGrowBeds`) on EVERY mutation; `HydrateFrom(snapshot)` / `ToSnapshot()` for SaveCoordinator; `OnChanged(bedId)` event; `ResetOnLoad` (SubsystemRegistration) clears dict + event — domain-reload-off safe.

Save shape: `SaveSlotMeta.GrowBeds` = `GrowBedSnapshot` parallel arrays (`Ids/SpeciesIds/PlantedDays/PlantedHours/Remaining`).

## Adding a new grow bed / species (checklist)

1. Scene: GameObject with `GrowBed`, unique `_bedId` (stable save key, e.g. `millyard_bed_2`), species SO + its world prefab, tune `_matureGameHours`/`_yield`.
2. Species must be obtainable (inventory is the seed source).
3. Verify: plant → save → reload → growth continues from clock; harvest each node → bed returns to Empty; count survives partial-harvest reload.
4. If future species need different rates per-species, consider promoting the recipe to an SO before duplicating beds (noted design debt).

## Gotchas

- **No TimeManager = instantly mature** (`GrowthFactor` → 1f) — convenient in tests, surprising anywhere else.
- **Polling harvest detection**: culling or scripts deactivating a node counts as harvested.
- **3-node harvest = 3 slot-file writes** (Persist on every SetRemaining change) — fine at scale, noted.
- **Per-frame `GrowBeds.Get(_bedId)`** per bed — fine now, revisit for many-bed futures.
- **Scaled `_spawnAnchor` compounds node scale** — keep anchors unit-scale.
- Unlock gating duplicated in `CultivationUnlocked()` (quest + `act2_started` flag) — keep in sync with quest content.
