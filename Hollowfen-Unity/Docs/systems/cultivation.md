# Cultivation System
Grow beds (Almy's garden, Act II): choose an eligible known species from the basket → consume one specimen as spawn → grow a data-authored flush on the game clock → harvest mature nodes through the normal inspect/cutting flow. Growth re-derives from persisted planting time, so save/load and rest both advance crops correctly.
Key scripts: `Assets/_Hollowfen/Scripts/Cultivation/` — GrowBed, GrowBeds, CultivationScreen. Shared policy: `Foraging/MushroomRules.cs`; recipe data: `MushroomFieldGuideData`.
Unlock gating: Almy's lesson completed (or its active Act-II teaching window), species discovered, optional species unlock flag satisfied, bed empty, and at least one specimen in inventory.
Recipes live on species data: `Cultivable`, `CultivationHours`, `CultivationYield`, `CultivationUnlockFlagId`, and `WorldPrefab`. Current foundation recipes: Wood Ear, Lacewig, Brightspore, and Oyster.
Biggest gotchas: no `TimeManager` means instantly mature; scene fields `_species/_mushroomPrefab/_matureGameHours/_yield` remain legacy fallbacks for old saves/components, not the primary recipe source; scaled spawn anchors compound specimen scale.
Status: generalized four-recipe picker, planting, clock growth, partial-flush persistence, and collider safety play-mode verified 2026-07-16.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## State flow

**Empty** (`GrowBeds.Get(_bedId) == null`) → **Recipe picker** → **Growing** (`GrowthFactor < 1`, visible but not interactive) → **Mature** (authored interaction colliders restored) → **Empty** when the last node is harvested.

- `GrowBed.Interact` opens the shared `CultivationScreen`; every bed reads the same eligible recipe list.
- `GrowBed.Plant(species)` validates `MushroomRules.CanCultivate`, consumes one inventory item, persists species/day/hour/yield, and spawns the selected species' world prefab.
- First planting still completes `almyTeach` when that quest is active.
- `GrowthFactor = Clamp01(((Day - PlantedDay) * 24 + Hour - PlantedHour) / species.CultivationHours)`.
- Mature child `MushroomNode`s are configured as cultivated: they do not enter the wild-node respawn store. Their authored collider defaults are cached; growing disables interaction without enabling dormant physics/helper colliders at maturity.
- `MushroomNode.IsHarvested` drives the remaining-flush count. The lifecycle host stays active while its renderers/colliders hide, so visual deactivation cannot be mistaken for an unrelated destroyed object.

## CultivationScreen

Runtime-built parchment recipe picker, created lazily on first bed interaction. It loads `Resources/MushroomFieldGuideDatabase`, filters to known/unlocked/cultivable species with basket stock and a world prefab, sorts by forage tier/name, and shows basket count, growth hours, and yield. Mouse, keyboard, and explicit controller navigation are supported; opening pauses time and player input and closing restores the previous state.

Adding a recipe requires no UI or bed code:

1. Set the species SO's cultivation fields and ensure `WorldPrefab` is assigned.
2. Choose an optional progression flag; discovery and Almy's lesson remain global requirements.
3. Run `Hollowfen/Gameplay Foundation/Apply Profiles and Scene Wiring` if the profile belongs in the canonical importer.
4. Run data integrity, then verify plant → rest/save → partial harvest → reload → clear bed.

## GrowBeds persistence

`Dictionary<string, BedRecord>` keyed by stable scene `_bedId`; each record stores `SpeciesId`, `PlantedDay`, `PlantedHour`, and `Remaining`. Every mutation targets the active slot through `AutoSaveGrowBeds`; `SaveCoordinator` also captures/hydrates the full snapshot. Json shape: parallel `Ids/SpeciesIds/PlantedDays/PlantedHours/Remaining` arrays.

## Gotchas

- No `TimeManager` returns growth 1 immediately; useful for isolated tests, dangerous in a gameplay scene.
- Each partial-flush count change writes the active slot; current scale is tiny, but many future beds may need batching.
- Keep `_bedId` stable and unique forever after shipping a save.
- Keep `_spawnAnchor` at unit scale.
- The recipe picker currently shows at most six options; paginate before adding a seventh simultaneous recipe.
