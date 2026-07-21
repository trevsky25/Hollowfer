# Batch 97 — HUD shape and alignment

**Date:** 2026-07-17 · **Status:** complete

## Goal
Clean up the normal gameplay HUD by removing the oversized offset disc behind the circular mini-map, centring the localized day-period pill under that map, and restoring the compass's intended rounded silhouette instead of letting rectangular fade overlays square off its ends.

## Plan
- [x] Inspect the authored mini-map, clock, and compass hierarchy and preserve unrelated dirty work.
- [x] Reproduce each visual issue in Play mode and isolate its source by toggling the suspect UI layers.
- [x] Disable the mini-map shadow and compass edge-fade overlays; anchor the clock directly to the mini-map panel.
- [x] Compile and repeat Play-mode visual verification.
- [x] Update the map/UI system docs and finalize this worksheet.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Mini-map depth | Disable the 336px `Shadow` image, retain the centred 312px ink frame. | The offset shadow is the unwanted second disc; the even frame still separates the map from the world. |
| Clock alignment | Build `ClockPill` under `MiniMapPanel`, bottom-centred at `(0, -40)`. | Shared coordinates remove the stale hard-coded X offset and remain correct when resolution, scaler, or map size changes. |
| Compass silhouette | Leave `FadeL` and `FadeR` authored but inactive. | Play-mode isolation proved those rectangular gradient quads created the black bar; the underlying compass fill and hairline are already rounded. |
| Scope | Change only HUD presentation, not compass bearing, waypoint, minimap crop, or time logic. | The report is visual and the existing navigation/game-state behavior is correct. |

## Verification evidence

- Baseline 3840×2160 Play-mode capture reproduced the 336px offset mini-map shadow, the clock's 30px reference-space left drift, and the squared compass overlay.
- Live hierarchy inspection identified `_MiniMapCanvas/MiniMapPanel/Shadow` at 336×336 and offset `(0, -6)`, plus `_HUDCanvas/Compass/FadeL` and `FadeR` as 70×34 gradient quads above the rounded background.
- Runtime A/B isolation showed that disabling the fade quads reveals the original rounded compass fill/hairline while leaving direction marks and the center notch intact.
- Runtime layout preview parented the 220×38 clock pill to `MiniMapPanel`; its X center matched the map center exactly and the 40px vertical gap remained unchanged.
- Unity refreshed and compiled the final `ClockHUD` change with zero console errors.
- Fresh serialized-scene Play-mode verification measured the map and clock at the same screen-space centre (`3504px`, `deltaX=0`) at 3840×2160; the rect-to-rect vertical gap was 80 physical pixels (40 reference pixels).
- The same runtime assertion confirmed `Shadow=False`, `FadeL=False`, `FadeR=False`, with the compass marks container and gold hairline still active.
- The final capture showed the mini-map with an even frame and no offset disc, the day-period pill centred below it, and a continuous rounded compass silhouette with its direction labels and notch intact.
- `ProductionUIVerifier`: `PASS · active UI presentation · 0 critical · 0 advisory`.
- `Scene_Hollowfen` and `Scene_MainMenu`: zero missing scripts, broken prefabs, or other scene-validation issues; final Unity console read returned zero errors.
- `lint_hollowfen.py`: `ERRORS=0 WARNINGS=0` (one existing waiver).
- `run_integrity.py`: `ERRORS=0 WARNINGS=0` across 26 quests, 75 dialogues, 11 NPCs, 14 locations, 21 mushrooms, 30 story cards, two story moments, 31 character profiles, four endings, and ten village requests.
- `git diff --check`: PASS for the batch files.

## Docs updated

- `Docs/systems/map.md` — corrected the baked mini-map description and documented mini-map, clock, and compass layout contracts.
- `Docs/systems/ui-framework.md` — recorded the `ClockHUD` anchoring contract and inactive compass edge fades.

## Unfinished / handoff

No work remains for this request. The scene and all touched scripts already contained unrelated in-progress changes; only the three HUD object active flags and the `ClockHUD` layout block belong to this batch, and those existing changes were preserved. Temporary Unity screenshot assets were removed after review; the final full-frame evidence was copied to `/private/tmp/Hollowfen_batch97_hud_final.png`.

## Feedback to Trevor

Both problems came from duplicated coordinate/visual layers rather than navigation logic. Parenting related elements to one layout root and letting a single rounded surface own the silhouette makes the HUD more resistant to resolution changes and later polish passes.
