# Batch 63 — Menu journal redesign

**Date:** 2026-07-14 · **Status:** implementation complete; final validation recorded below

## Goal

Redesign the Wren profile, Mushroom Field Guide, and fullscreen Story cards as a cohesive, controller-first field-journal family. Correct progression leaks, image distortion, localization gaps, and focus restoration first; add shared journal presentation/navigation primitives; then migrate Story, Field Guide, and Wren in verified vertical slices.

## Plan

- [x] Establish compile, lint, data, and live-UI baselines
- [x] Fix progression, aspect-fit, locked-focus, and localization correctness
- [x] Add shared journal chrome, art presenter, pager, and focus-restoration primitives
- [x] Redesign fullscreen Story reader
- [x] Redesign Field Guide index and specimen detail
- [x] Restructure Wren profile and field-study gallery
- [x] Verify 1280×800 controller, long-copy, locked, and missing-art states
- [x] Update system docs and finalize worksheet

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Architecture | Keep `UIManager` and the existing content SOs; share small presentation/navigation adapters | Gives the three pages one grammar without rewriting routing or canonical data |
| Progression | One availability predicate per content family, reused by cells, open guards, paging, counts, and focus | Prevents locked art/title leaks and controller focus landing on unavailable content |
| Art | `JournalArtPresenter` owns cover/contain policy | Fullscreen Story art may crop; specimen photos and Wren plates must never stretch |
| Localization | Fixed chrome keys plus derived content keys with canonical English fallbacks | Makes every path translation-ready without first duplicating all English SO prose in the LUT |
| Mushroom specimens | Use only dedicated authored journal models, with photo/pending fallbacks | Fly Agaric, Field Mushroom, and Oyster have real model assets; recolored gameplay stand-ins should not masquerade as unique studies |
| Existing canon | Preserve all Story/mushroom data and all five Wren plate sprites | Redesign presentation without replacing or dropping established content |

## Delivered

- Shared `JournalChrome`, `JournalArtPresenter`, `JournalMushroomModelPresenter`, `JournalNavigation`, and `JournalText` helpers.
- Public `FocusHighlight.Configure(...)` for safe code-built focus visuals.
- Story: three-column act index, opaque locked cards, unlocked-only paging, cinematic reader leaf, annotations that consume Back before exit, and originating-card return focus.
- Field Guide: three-column square specimen index, discovered-only focus/paging, rotating 3D cards for the three authored models, photographic fallback cards, large left-leaf 3D studies, field photos beside the right-leaf description, scroll waypoints, intentional missing/pending states, and originating-card return focus.
- Wren: shorter dossier hierarchy, retained full-bleed drifting hero, Background/Perspective/kit/pull-quote sections, and an interactive aspect-contained five-plate gallery.
- `DataIntegrity` coverage for 30 Story cards, 21 mushrooms, one profile, required journal fields, Story art, Wren hero/plates, and fixed journal localization IDs.

## Verification evidence

| Check | Result |
|---|---|
| Unity compile / console | Clean after implementation and after the final localization-key patch |
| Gotcha lint | `0 errors, 0 warnings, 1 documented waiver` |
| Data integrity | `ERRORS=0 WARNINGS=0`; coverage reports 30 Story cards, 21 mushrooms, one profile |
| Steam Deck Game View | Custom `Steam Deck (1280x800)` preset, target render size confirmed 1280×800; all evidence PNGs are 1280×800 |
| Story state | Current save: 8/30 unlocked; 22 locked buttons inert; first unlocked focus selected; annotations show on A and hide on first B while `story-detail` remains top |
| Field Guide state | Current save: 6/21 discovered; 15 locked buttons inert; first discovered focus selected; detail Back restored `Card_flyAgaric` |
| Missing art | Temporarily hydrated Oyster in memory only: `photo=False`, missing label active, discovery snapshot restored to 6 before Play mode ended |
| 3D model coverage | Fly Agaric, Field Mushroom, and Oyster rendered through isolated rotating rigs; unmodeled Chanterelle verified with the pending-model state and its field photo retained on the right |
| 3D runtime isolation | Model instances disable gameplay MonoBehaviours/colliders; hidden index/detail cameras and lights deactivate with their UI surface; `JournalPreview` uses a dedicated render layer |
| Wren navigation | Five plate buttons interactable with explicit left/right/up/down links; pull-quote included; scroll content height 2240 reference px |
| Play smoke | PASS after 3D follow-up: 242 synchronously stepped frames (minimum 240), zero pre-play or in-play console errors |

Visual evidence: `Docs/screenshots/batch-63/`

- `story-index-1280x800.png`
- `story-detail-1280x800.png`
- `story-detail-annotations-1280x800.png`
- `field-guide-1280x800.png`
- `mushroom-detail-1280x800.png`
- `mushroom-missing-photo-1280x800.png`
- `field-guide-3d-1280x800.png`
- `mushroom-detail-3d-1280x800.png`
- `mushroom-detail-field-3d-1280x800.png`
- `mushroom-detail-oyster-3d-1280x800.png`
- `wren-top-1280x800.png`
- `wren-gallery-1280x800.png`

## Docs updated

- `Docs/systems/menu-pages.md`
- `Docs/systems/ui-framework.md`
- `Docs/systems/localization.md`
- `Docs/tests.md`

## Unfinished / handoff

- Dedicated journal-model coverage is 3/21. To add another species, assign its authored prefab to `_journalPreviewPrefab` and extend `DataImporter.JournalModelPath`; do not rely on `_worldPrefab`, because several world prefabs are recolored gameplay stand-ins.
- Routing is localization-ready; Simplified Chinese and the full derived content LUT still need a content/translation pass.
- The explicit EventSystem navigation graph was exercised live, but one final hands-on physical controller feel pass is still recommended before a public Deck build.
- Pre-existing unrelated worktree changes in `Docs/dashboard.html`, `src/story-page.js`, and local picture-book output folders were preserved and not reverted.

## Suggested Play-mode test (5 steps)

1. Set Game View to `Steam Deck (1280x800)`, enter Play mode, and open Story. Confirm locked memories cannot receive focus; open an unlocked card, toggle annotations, then press Back twice to verify annotations close before the reader.
2. Open Field Guide. Move through discovered cards, open one, use Up/Down through the reading sections and Left/Right between specimens, then Back; focus should return to the originating card.
3. Open Fly Agaric, Field Mushroom, and Oyster: confirm each card and left detail leaf rotates the correct model. Oyster should keep its model left while the right field-photo block says `No field sketch recorded`; an unmodeled species should show the pending-model leaf and retain its field photo on the right.
4. Open Wren. Move down through identity, both dossier cards, kit, pull-quote, and gallery; choose each of the five plates and confirm none stretches or crops.
5. Return to the main menu and watch the Console for errors; exit Play mode without modifying the QA save.

## Feedback to Trevor

The most reusable result is the separation between content availability and presentation. A screen no longer decides “locked” independently in four places: one predicate feeds render state, controller graph, paging, and open guards, while the art/text adapters handle crop policy and localization. Future journal sections should build on those seams instead of cloning another monolithic screen.
