# Batch 112 — Full Village Restoration and Production Audit

**Date:** 2026-07-18 · **Status:** verified (uncommitted)

## Goal

Complete every post-bridge Living Restoration phase, make the seven-project village arc mechanically and visually meaningful, then run and remediate a full copy, story/plot, UI/UX, accessibility, persistence, performance, and production-readiness audit while preserving the existing game and user saves.

## Plan

- [x] Extend the catalogue from cottages/bridge to five later story-gated projects.
- [x] Author five-stage world presentation, funding, crews, dawn reveals, first use, rewards, and benefits.
- [x] Integrate benefits with foraging, cultivation, village requests, endings, and persistence.
- [x] Polish and visually inspect the seven-row Restoration Ledger.
- [x] Add readable interface scale, reduced motion, and caption backing.
- [x] Survey and correct every final restoration camera/world composition.
- [x] Run copy/story/performance/UI/save/data regression audits and repair findings.
- [x] Update systems documentation and production audit.
- [x] Remove temporary QA captures and return Unity to a clean gameplay edit state.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Progression model | Reuse the same six monotonic stages and two-dawn scheduler for all projects. | One persistence/narrative vocabulary is easier to reason about and migrate. |
| Funding | Two exact copper contributions per later project. | Meaningful player choice without an inventory-crafting detour; atomic save boundary already proven. |
| Lasting value | Derive five benefits from Occupied stages through `RestorationBenefits`. | Avoid duplicate flags/schema and make restored spaces mechanically relevant. |
| Witch's Path | Set the two established ending facts on first use. | Integrates with canon instead of creating a second ending route. |
| Reveal art | Real world transformations, no new story raster cards. | The new physical work is the stronger, context-specific reveal; no missing art justified generation. |
| Pintle presentation | Move public dressing beyond the measured east-wall collider. | The first authored furniture existed inside the vendor mesh and could not be seen. |
| Mill presentation | Eye-level awning/workshop, remove roof-patch planes. | Roof cubes floated above the real shingle pitch and read as a rendering bug. |
| Accessibility scaling | Project the shared reference resolution to 100/108/115%. | Reaches existing and future code-built canvases without rewriting every layout. |
| Reduced motion | Each animation owner supplies a stable alternative. | Preserves final composition, VO, callbacks, input, and duration instead of globally changing time. |
| Performance | Hard authored and simultaneous-stage caps in the expansion verifier. | Prevents future scene dressing from silently compounding cost. |

## Verification evidence

- Compile: zero project errors. New obsolete API warning removed; remaining warnings are third-party Infinity PBR code.
- `lint_hollowfen.py`: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- Data Integrity: `ERRORS=0 WARNINGS=0` after accessibility IDs and five projects.
- Full expansion: PASS — seven-project catalogue, five worksites, ten supply lines, two dawn beats, five benefits, Witch's Path flags, rewards, first-use rollback, bounded world cost.
- Foundation/bridge, gameplay foundation, inventory transactions, save recovery, village requests, endings, schedules, mill door, Bram/Marra, world feedback, music, day/night, gameplay audio, and presentation ownership: PASS.
- Content: 12 real NPC models; 12 destinations over 235m × 367m; 41 nodes/7 habitats; 21/21 mushroom spreads; 28 story cards; 93 HD paintings; Marra 3 voiced 4K paintings/live Goldfoot; Almy 4 voiced beats/3 paintings.
- Gameplay audio: 13 cues and 249/249 voice clips across 75 dialogues.
- Accessibility: 115% reached 17 menu-booted runtime canvases; reduced motion preserved glow without scale; caption preference persisted; exact PlayerPrefs state restored.
- UI lint: gameplay HUD and largest Settings both `0 critical / 0 advisory`.
- Visual QA: standard/largest Settings, all seven Ledger rows, five world reveals, active/night restoration dressing. Caught/fixed ledger crowding, focus ambiguity, canopy/roof occlusion, Pintle collider placement, and floating mill panels. Temporary screenshots removed.
- Performance: about 89.6K total all-stage authored triangles; <=430 authored renderers, <=240 worst simultaneous, <=18 shared materials, <=7 particles, four shadowless lights, three beds; settled SetPass remained 129 with projects active/disabled at the audited camera; Editor main thread about 3.36–4.55 ms.
- Final smoke: first run exposed teardown-only MusicManager/DialogueSpeakerAnimator errors; added null/initialization guards. Repeat reached 388 frames, returned to Edit Mode, and left zero console errors.

## Docs updated

- `Docs/systems/restoration.md` — seven-project domain, projects, benefits, reveals, crews, verifier budgets.
- `Docs/systems/settings.md` — accessibility inventory/policy and reduced-motion ownership.
- `Docs/systems/ui-framework.md` — five-tab Settings, accessibility canvas contract, Ledger/UI status.
- `Docs/systems/{foraging,cultivation,village-requests,npcs,save}.md` — derived benefit/schedule/persistence integration.
- `Docs/tests.md` — expansion and accessibility verifiers, current audio/light scopes.
- `Docs/review/production-audit-2026-07-18-restoration-expansion.md` — full findings, performance evidence, regression matrix, external blockers.

## Unfinished / handoff

No implementation or editor-verification work remains for this batch. The worktree contains substantial earlier user/session changes and remains deliberately uncommitted; do not stage the whole tree without reviewing scope. No standalone was built. The external release blockers and hardware tests are listed in the audit. Unity is to be left in `Scene_Hollowfen` Edit Mode with no temporary save directory, forced project state, test preference, or QA screenshot remaining.

## Feedback to Trevor

The highest-value improvement was treating world reveals as camera-dependent gameplay content. The automated system was correct while three reveals were visually wrong; fixed-position screenshots exposed it immediately. Keeping hard renderer/material/light limits inside the verifier also prevented the visible Pintle fix from quietly exceeding its simultaneous-stage budget.
