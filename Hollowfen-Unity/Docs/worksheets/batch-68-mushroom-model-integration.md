# Batch 68 — Mushroom Model Integration

**Date:** 2026-07-14 · **Status:** verified

## Goal
Integrate Trevor's delivered Meshy mushroom set into the active Unity game and Field Guide without shipping the source-resolution meshes directly. The batch adds a repeatable optimization/import pipeline, species-correct gameplay prefabs, dedicated journal preview prefabs, and data wiring for every represented canonical entry.

## Plan
- [x] Inventory the delivered files and map them to the canonical 21-entry database.
- [x] Generate world-budget and journal-budget meshes from each unique source model.
- [x] Build normalized URP materials plus gameplay and journal prefabs through one idempotent importer.
- [x] Wire `WorldPrefab` and `JournalPreviewPrefab` references, including the T4 folk-name aliases.
- [x] Bound Field Guide preview work to visible cards so full model coverage remains Deck-safe.
- [x] Play-mode verification.
- [x] Docs updated + worksheet finalized.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Active target | Unity project, not the retired web prototype | The repository router marks `Hollowfen-Unity/` as the shipping game and root `src/`/`public/` as reference-only. |
| Source handling | Preserve the original FBX drops in `public/`; generate optimized Unity-owned derivatives | The delivered set is about 1 GB and individual meshes reach more than one million vertices, which is unsuitable for repeated world nodes and simultaneous journal cards. |
| Preview architecture | Use the existing dedicated `_journalPreviewPrefab` field | This keeps high-detail journal presentation separate from lower-cost gameplay models and completes the journal redesign's intended seam. |
| Species aliases | Reuse geometry but create species-specific world prefabs for Moonring, Hollowheart, Wendlight, Lacewig, and Oyster | Shared geometry is correct for the same real fungus, while each harvestable prefab must retain its own `MushroomFieldGuideData` reference. |
| Missing delivery | Leave Aldermark pending | Aldermark is Hen of the Woods / Maitake; substituting a different supplied species would break the educational/canon rule. |

## Verification evidence
- Blender optimization completed for all 16 unique source models. Journal derivatives are capped at 60k–75k triangles and gameplay derivatives at 12k–16k triangles; the largest delivered mesh dropped from 2,057,965 triangles to 75,000 / 16,000.
- The Unity importer was run again after implementation and reported: `Built 16 source model sets and wired 20 species. Aldermark remains pending its Maitake model.`
- Data Integrity: `ERRORS=0 WARNINGS=0`, including 20 dedicated journal models and exact world-prefab data-reference checks.
- Project lint: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- Unity recompiled the editor scripts and the post-rebuild console contained zero errors.
- Field Guide at the 1280×800 Deck target showed the top, middle, bottom, and Brightspore detail states cleanly. With all entries exposed for the test, only 6 visible model rigs were active rather than all 20.
- `Scene_Hollowfen` survived 300 stepped Play-mode frames with 21/21 mushroom nodes rendered and 0 missing data references.
- Temporary discovery overrides were removed and the editor was restored to `Scene_MainMenu`.

## Docs updated
- `Docs/systems/foraging.md`
- `Docs/systems/menu-pages.md`
- `Docs/asset-dropoff.md`
- `Docs/steam-constraints.md`
- `Docs/tests.md`
- `Docs/review/performance.md`
- `Docs/meshy-mixamo-worklist.md`
- `TODOS.md`

## Unfinished / handoff
The source drop contains 17 folders but 16 unique models: `Chantrelle` and `Golden Chantrelles` are byte-identical. Those 16 models cover entries 01–20 after same-species aliases. Aldermark remains the only pending entry because no Maitake model was delivered. No commit was created because the current worktree already contains Trevor's in-progress batches 63–67 and related uncommitted UI/foraging changes.

## Feedback to Trevor
The source models are visually rich but far above runtime budgets (existing examples range from ~339k to 1.14m vertices each). Keeping originals as source art and generating separate world/journal derivatives makes future replacements repeatable and avoids silently turning Field Guide coverage into a performance regression.
