# Batch 80 — Repeatable game-loop foundation

**Date:** 2026-07-16 · **Status:** DONE in working tree (compile, integrity, lint, and focused Play Mode verifier green)
**Directive:** Start the recommended engine pass before cast models and world dressing: make Hollowfen's clock, forage ecology, trade, and cultivation form one repeatable, save-safe loop without disturbing the existing 26-quest story spine.

## Player loop

1. Explore and inspect a mushroom; its species tier decides whether Wren has the knowledge/tool to cut it.
2. Complete the cinematic cutting challenge; inventory, discovery, and that exact wild node's cut day persist.
3. Sell accepted species to Marra or Theo at their individual values; refused specimens remain in the basket.
4. After Almy's lesson, plant any eligible known recipe from the shared grow-bed picker.
5. Rest at the mill hearth to cross the same sundown/dawn events as natural time. Crops advance and wild nodes return on their authored dawns.

## Implementation

- Added `MushroomRules`, the single policy boundary for harvest access, cultivation access, and buyer values.
- Expanded all 21 `MushroomFieldGuideData` profiles with forage tier, optional story flag, wild respawn days, Marra/Theo copper values, cultivation eligibility/time/yield, and optional cultivation flag.
- Added `ForageNodeStates` plus `SaveSlotMeta.ForageNodes`. The 21 currently placed wild scene nodes have unique stable ids and remain active as lifecycle hosts while harvested visuals/colliders hide. Snapshots sort ids deterministically, and the importer preserves valid existing ids when later world dressing moves or adds specimens.
- Added `TimeManager.AdvanceTo`, which reconstructs every crossed sundown and dawn in order. The mill-hearth `RestSpot` uses it behind a confirm/fade and full checkpoint save. Static clock events now reset safely between plays.
- Added `InventoryRuntime.SellTo(MushroomBuyer)`. Marra and Theo repeat-sale dialogues use species values and preserve refused stock; the authored first-pot payment retains its legacy flat story amount.
- Replaced the Wood-Ear-only bed interaction with `CultivationScreen`, a controller/mouse/keyboard recipe picker driven from species data. Wood Ear, Lacewig, Brightspore, and Oyster are live recipes. Grow beds persist the chosen species and restore only authored interaction colliders at maturity.
- Locked mushroom tiers remain inspectable in the cinematic journal, but the Forage action is disabled with a localized knowledge note.
- Added the idempotent `MushroomGameplayFoundationImporter`, integrity rules, and a focused state-mutating verifier.

## Verification

- C# compilation: zero errors.
- Data Integrity: `ERRORS=0 WARNINGS=0`; coverage = 26 quests, 74 dialogues, 11 NPCs, 14 locations, 21 mushrooms, 30 story cards, 2 moments, 1 character, 4 endings; 4 cultivation recipes reported.
- Gotcha lint: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- `GameplayFoundationVerifier.RunAll`: PASS — 21 unique authored wild-node ids + one rest point; Marra/Theo pricing and dangerous-item refusal; forage cooldown before/on respawn dawn plus disk round-trip; four recipe gates and real Wood Ear planting/consumption; exact sundown then dawn event order.
- Runtime after 241 stepped frames: zero console errors. The apparent frame freeze is macOS App Nap; synchronous stepping is the existing smoke fallback.
- Save hygiene: copied the complete real save directory before verification, restored it afterward, and matched every file SHA exactly. The mutated verification copy remains under `/tmp` only.
- `git diff --check`: clean for the batch scope.

## Intentional follow-on

- This is engine foundation, not final balance. Tune prices, respawn cadence, and grow yields after a full traversal playtest.
- The scene has 21 authored wild nodes across nine currently placed species. All 21 species are profiled; placing the remaining species belongs to the planned world-dressing pass.
- The rest point deliberately reuses the journal/hearth interaction area without a bespoke bed/hearth prop; dress it when the mill interior gets its final art pass.
- A later objectives pass can consume this loop for rotating delivery requests, festival gathering, Edda remedies, Theo market notes, and weekly wagon demand.

## Primary files

- `Assets/_Hollowfen/Scripts/Data/MushroomFieldGuideData.cs`
- `Assets/_Hollowfen/Scripts/Foraging/{MushroomRules,ForageNodeStates,MushroomNode,InventoryRuntime,InspectScreen}.cs`
- `Assets/_Hollowfen/Scripts/Time/{TimeManager,RestSpot}.cs`
- `Assets/_Hollowfen/Scripts/Cultivation/{GrowBed,CultivationScreen}.cs`
- `Assets/_Hollowfen/Scripts/Dialogue/{DialogueData,DialogueScreen}.cs`
- `Assets/_Hollowfen/Scripts/Save/{SaveSlotMeta,SaveManager,SaveCoordinator}.cs`
- `Assets/_Hollowfen/Scripts/Editor/{MushroomGameplayFoundationImporter,GameplayFoundationVerifier,DataIntegrity}.cs`
- `Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity` and all 21 mushroom assets
