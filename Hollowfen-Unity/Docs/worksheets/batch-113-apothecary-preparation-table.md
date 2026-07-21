# Batch 113 — Tobin's complete apothecary building

**Date:** 2026-07-19 · **Status:** complete

## Goal

Install the complete authored Alchemy and Magic Lab showcase as Tobin's apothecary on the wooded terrace above Father's Mill, then connect its preparation table to mushroom identification, inventory, restoration progression, and persistent saves.

## Art direction

- Preserve the package's four connected authored rooms—entrance, hall, open laboratory, and cage room—as one intact building.
- Keep the package's own architecture, furniture, materials, dressing, animation, and atmosphere. Do not add a custom low-poly roof, awning, or replacement workbench.
- Keep the architecture visible from a fresh game and every restoration stage; progression restores access and use rather than spawning the landmark from nothing.
- Place the whole building on a bounded, levelled terrace northwest of the mill, with the entrance facing the mill approach.
- Open both authored doorways permanently so the player can traverse the building without the package's demo controller.
- Render the purchased inward-authored wall and vault surfaces from both sides so the original showcase cutaway reads as a complete exterior in Hollowfen.
- Remove the package's two static closed-door blocker colliders while retaining its wall, floor, open door-leaf, furniture, and prop collision.
- Strip vendor demo scripts, duplicate audio, rigidbodies, trigger volumes, and baked-lightmap bindings while retaining solid architectural colliders.
- Replace showcase lighting with four restrained realtime interior lamps and preserve the existing URP material conversion.

## Player loop

1. Discover Tobin's closed apothecary above the mill, then restore it through the living-restoration board and reach its occupied stage.
2. Identify the required mushroom species in the field journal.
3. Forage the recorded specimens and enter Tobin's apothecary above the mill.
4. Select a known preparation in **The Working Ledger**.
5. Complete its four physical bench marks in order; the matching world prop responds at each step.
6. Consume the exact ingredient measure atomically and place the finished preparation on the persistent workshop shelf.

## Initial preparations

| Preparation | Ingredients | Method | Output |
| --- | --- | --- | --- |
| Tobin's Field Ink | Wood Ear ×1, Pinecrest ×1 | weigh, grind, fold, bottle | field ink |
| Goldfoot Hearth Broth | Goldfoot ×1, Wood Ear ×2 | weigh, slice, steep, bottle | hearth broth |
| Brightspore Shelf Tonic | Brightspore ×1, Wood Ear ×1 | weigh, grind, strain, bottle | shelf tonic |

All ingredients must be identified before their names or recipe action becomes available. The screen includes an explicit fictional-world safety notice and does not present real mushroom or medical guidance.

## Persistence and failure safety

- Prepared item quantities and practiced recipe IDs are serialized in `ApothecarySnapshot`.
- Old saves hydrate a valid empty preparation shelf.
- Ingredient removal, output addition, first-craft knowledge, completion flags, and save write are one transaction.
- A failed final commit rolls back every mutated value and emits no success event.

## Verification

- [x] `ApothecaryPreparationVerifier`: complete owned building, two-sided exterior shell, player-sized traversal through both open doors, bounded textures/lights/colliders, identification gates, four-step recipes, commit/rollback, old-save hydration, and stock persistence.
- [x] `GameplayFoundationVerifier`: mushroom identification, forage lifecycle, economy, cultivation, and clock boundaries.
- [x] `RestorationVerifier`: staged restoration state, migration, and save/load round trip.
- [x] `VillageRestorationExpansionVerifier`: all seven projects, supply lines, benefits, rollback, and world-cost bounds.
- [x] `NPCScheduleVerifier`: story overrides and time-based routines, including the mill-door and restoration roles.
- [x] `InventoryTransactionVerifier`, `SaveIntegrityVerifier`, and data-integrity audit.
- [x] UI reviewed in Play Mode at 1280 × 720; exterior approach and interior placement reviewed on the restored mill terrace.

## Unity test route

- Load `Scene_Hollowfen` and enter Play Mode.
- In a progressed save, complete **A Workshop at Home** through the occupied stage.
- Follow the uphill path northwest of the mill, enter the open vaulted apothecary, and interact with **Tobin's preparation table**.
- A recipe with unidentified ingredients must hide the unknown names and remain blocked.
- A fully identified but understocked recipe must list the missing measure without advancing.
- With the required specimens, complete all four marks and confirm the shelf count survives save and reload.

## Follow-on slice

Prepared stock is now a stable domain system. The next vertical slice can consume it through village requests, remedy delivery scenes, or restoration work orders without changing the bench transaction model.
