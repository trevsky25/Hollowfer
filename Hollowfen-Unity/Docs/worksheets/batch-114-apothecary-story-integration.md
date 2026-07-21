# Batch 114 — Tobin's Apothecary Story Integration

**Date:** 2026-07-19 · **Status:** complete

## Goal

Turn the installed Alchemy and Magic Lab building into a canonical Hollowfen side arc and repeatable preparation economy. Wren should discover why Tobin kept the workshop, restore its use with people already in the story, learn preparations through identified mushroom knowledge, make physical products, and deliver those products through village relationships without weakening the main Act I–IV plot.

## Story contract

- The apothecary is Tobin's secondary workroom above the mill, not a replacement for the mill or Father's journal.
- The complete structure is an always-visible landmark; restoration changes access, activity, and use rather than spawning a building from nothing.
- Bram carries personal memory, Almy carries safe practice, Pell/Joren carry restoration labor, Edda carries village-care use, Marra carries pantry use, Theo carries trade use, and Hollin carries later Old Wood knowledge.
- No new NPC, mushroom species, location, romance, real medical claim, or ending prerequisite is introduced.
- The arc is additive: it may reward Hope, Knowledge, relationships, prepared stock, and repeat orders, but it does not rewrite the canonical 26-quest chain or four ending gates.

## Implementation plan

- [x] Baseline existing apothecary, restoration, requests, dialogue, quest, save, localization, audio, map, and NPC-schedule contracts.
- [x] Author a data-driven apothecary discovery/tutorial/delivery arc using existing characters and canonical flags.
- [x] Gate recipe knowledge progressively and keep unidentified ingredient names concealed.
- [x] Represent prepared products as durable save-backed stock consumable by story and recurring village deliveries.
- [x] Add physical shelf-stock presentation and improve bench-step feedback without changing purchased architecture.
- [x] Add map/location and interaction copy for undiscovered, restoring, and operating states.
- [x] Add controller/accessibility-safe UI and fictional-world safety copy.
- [x] Add focused atomic rollback, migration, story-route, schedule, and world-presentation verification.
- [x] Run Play Mode from the real gameplay boot path, visually inspect the complete loop, and update touched system docs.

## Decisions

| Decision | Choice | Reason |
|---|---|---|
| Narrative shape | Connected side arc, not new main-quest chain nodes | Preserves canonical quest order and existing ending resolution. |
| Product ownership | Extend `ApothecaryRuntime` stock as the single authority | Avoids duplicating prepared goods in mushroom inventory or key items. |
| Delivery safety | One atomic transaction across product, request/story facts, rewards, and final save | Prevents partial delivery state after a failed disk commit. |
| Medical framing | Fictional village-care preparations only | Supports the story while avoiding real medical guidance. |
| Building art | Purchased architecture and dressing only | Matches Trevor's direction and prevents a second visual language. |

## Delivered route

1. Completing `almyTeach` surveys Tobin's workshop; Almy's first bench lesson opens Field Ink.
2. Identified Wood Ear + Pinecrest become Field Ink through four physical bench steps.
3. Theo's one-shot delivery opens Goldfoot Hearth Broth; Marra's delivery opens Brightspore Shelf Tonic; Edda's delivery settles `apothecary_story_complete`.
4. Each finished preparation is durable shelf stock. Purchased jar/flask renderers visibly appear only when their product is in stock.
5. The three products then enter the normal dawn rotation as Theo, Marra, and Edda repeat orders. Field Ink earns a wet-weather premium.
6. Bram, Joren, Pell, Hollin, Almy, Theo, Marra, and Edda own eight new voiced conversations around memory, labor, method, and use. The arc remains optional and does not add a main-quest node.

## Verification evidence

- `ApothecaryPreparationVerifier.RunAll()` — PASS: purchased building/traversal, three progressive recipes, map marker, shelf stock, atomic preparation, rejected-commit rollback, old-save hydration.
- `VillageRequestVerifier.RunAll()` — PASS: 12 rotations, three sequential preparation deliveries, raw + prepared stock transaction, rewards, rollback, tracking, and festival handoff.
- `VillageRestorationExpansionVerifier.RunAll()` — PASS: seven-project catalogue, `almyTeach` gate, work crews, two-dawn stages, benefits, and world budgets.
- `DataIntegrity.RunAllAsReport()` — `ERRORS=0 WARNINGS=0` across 26 quests, 83 dialogues, 16 requests, and seven restoration projects.
- 360-frame gameplay smoke — zero new console errors.

## Test in Play Mode

For hands-on QA, load a slot after `almyTeach`, speak with Almy at the occupied workshop, identify the four recipe species, prepare Field Ink, and complete the Theo → Marra → Edda delivery chain. Confirm each purchased shelf prop appears with stock and disappears on delivery; save/reload between any two steps. Repeat orders should then enter the relevant NPC's normal daily rotation.
