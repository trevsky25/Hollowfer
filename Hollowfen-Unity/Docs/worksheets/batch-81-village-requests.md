# Batch 81 — Village requests, remedies, and the first festival gathering

**Date:** 2026-07-16 · **Status:** DONE in working tree (compile, integrity, focused Play Mode verifier, and regression smoke green)
**Directive:** Continue the game-engine pass with rotating NPC orders, medicinal deliveries, and gatherings that reuse Hollowfen's illustrated story language without weakening the 26-quest spine.

## Player loop

1. Talk to Marra, Edda, or Theo after their relationship/system unlocks.
2. Read an illustrated request card; pin the requirements beneath the current story objective.
3. Forage or cultivate the named species. The card and tracker update from the live basket.
4. Deliver the full basket atomically for copper. Each authored request's first completion can deepen a relationship/knowledge; repeats pay coin only.
5. Rest to dawn. Claimed work and tracked ordinary orders expire, and each NPC's next deterministic order rotates in.

Story work uses the same surface but does not expire. The festival now begins with Marra/Bram/Pell's original coordination lines, asks for Goldfoot, Lacewig, Field Cap, and Brightspore, safely commits the quest and flags, then plays Wren/Edda's original bowls finale.

## Implementation

- Added `VillageRequestData` + `VillageRequestDatabase`: requirements, requester, illustration, reward, eligibility gates, first-only progression, and optional story outcomes are fully data-authored.
- Added `VillageRequests`: stable day/NPC rotation, story priority, one delivery per NPC/day, tracking, save hydration, and completion transaction.
- Added `InventoryRuntime.TryRemoveBatch`: aggregate + validate all species before one mutation and one autosave.
- Added the controller-first `VillageRequestScreen` and compact `VillageRequestTrackerHUD`. Ordinary orders expose **Talk instead** so trade/dialogue remains reachable.
- `NPCData.PickActiveQuestDialog` + `NPCInteractable` now compose story dialogue with request routing. Unrelated active story beats still win.
- Added 3 Marra kitchen baskets, 3 Edda remedies, 3 Theo crates, and 1 festival gathering. Existing StoryCard art supplies the image-only request illustrations.
- Split `Dialogue_Act3_Festival` into kickoff and finale; request delivery now owns the canonical `festivalHosted` completion.
- Added one stable Lacewig wild source so every festival ingredient is obtainable.
- Added `VillageRequestSnapshot`, targeted autosave, full coordinator reset/hydrate/gather, Data Integrity coverage, an idempotent content importer, and a focused verifier.

## Verification

- C# compilation: zero errors.
- Data Integrity: `ERRORS=0 WARNINGS=0`; 10 village requests, 75 dialogues, and all existing content covered.
- `VillageRequestVerifier.RunAll`: PASS — 9 deterministic rotating orders, one-per-day claims, basket/payment persistence, first-only relationships, tracker expiry, controller card structure, Edda/Theo eligibility, and festival quest handoff.
- Visual QA: `Docs/screenshots/batch-81/village-request-card-1280x800.png` — full-height story painting crop, readable progress/reward hierarchy, and tracker coexistence with the story quest HUD.
- Runtime verifier console: zero errors (`0|47|7`; 47 known vendor/scene warnings and 7 logs).
- General smoke: PASS after ≥240 frames with zero new console errors.
- Gotcha lint: clean apart from the one existing documented waiver.
- Save hygiene: complete real save directory backed up before state-mutating tests and restored with matching SHA-256 for every original file.
- `git diff --check`: clean for the batch scope.

## Intentional follow-on

- Balance the nine ingredient/reward tables after a full traversal; the current values make early orders useful without eclipsing ordinary buyer prices.
- This is the gathering/system pass, not the festival world-dressing pass. Lanterns, tables, crowd silhouettes, and a square location trigger remain with world dressing.
- Requests use existing story art intentionally. Bespoke order-board paintings or short NPC VO can be added asset-by-asset without changing runtime code.
- Theo's requested “inn cellar” crate is trade flavor, not the still-pending dual-placement of Theo at the inn for his Capital-offer scene.

## Primary files

- `Assets/_Hollowfen/Scripts/Requests/*.cs`
- `Assets/_Hollowfen/Data/Requests/*.asset`
- `Assets/_Hollowfen/Resources/VillageRequestDatabase.asset`
- `Assets/_Hollowfen/Scripts/{NPCs,Foraging,Save}/`
- `Assets/_Hollowfen/Scripts/Editor/{VillageRequestContentImporter,VillageRequestVerifier,DataIntegrity}.cs`
- `Assets/_Hollowfen/Data/Dialogue/Dialogue_Act3_Festival*.asset`
- `Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity`
