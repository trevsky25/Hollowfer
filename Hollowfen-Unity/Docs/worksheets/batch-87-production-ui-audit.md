# Batch 87 — Production UI audit

Date: 2026-07-16

## Goal

Run a production-focused technical and visual sweep of every major menu, journal, gameplay HUD,
overlay, modal, and cinematic transition at 1280×800. Remove the blurred black rectangle visible
during Wren's mushroom-cutting kneel, standardize presentation contracts, and leave a repeatable
runtime verifier for future screens.

## Root cause: cutting-cinematic black rectangle

`ForageCuttingHUD.BuildPanel` added its soft shadow as a sibling of the panel. The kneeling shot
faded a `CanvasGroup` attached only to the panel, so the panel disappeared while its blurred shadow
remained fully opaque.

The panel and shadow now live under `CuttingPanelPresentation`, and the presentation owns the
single `CanvasGroup`. During the verified kneeling phase the group measured `alpha=0.00`, with zero
shadow objects outside the presentation. The gameplay HUD and minimap also step aside for the
cutting sequence and restore their exact prior CanvasGroup state afterward.

## Production polish completed

- Standardized active screen and HUD canvases on the shared 1920×1080, match-0.5 scaler contract.
- Preserved the approved 1280×800 save-slot and pause compositions through explicit presentation
  roots while removing legacy scaler behavior.
- Centralized cursor policy in `UIManager`: screen stack open means visible/unlocked; gameplay with
  no screen means hidden/locked.
- Removed the gameplay scene's legacy `StandaloneInputModule`. Normal flow now keeps exactly one
  persistent modern EventSystem; direct gameplay-scene play creates one modern fallback.
- Made hidden gameplay HUD canvases passive (`interactable=false`, `blocksRaycasts=false`) beneath
  dialogue, map, prop focus, endings, and cutting cinematics.
- Increased undersized helper, status, map, request, journal-model badge, Wren dossier, inventory,
  inspect, toast, clock, compass, and quest typography.
- Increased small settings controls and arrow/button hit areas; normalized main-menu navigation
  targets to the production minimum.
- Localized pause copy and hardened focus/cursor restoration across menu-to-game transitions.
- Removed two stale missing-script components and disabled the obsolete legacy door-text canvas in
  `Scene_Hollowfen`.
- Hardened input subscriptions and dialogue animator teardown across editor domain reloads.
- Updated one third-party deprecated object lookup whose compiler warning triggered a Unity 6
  invalid-AssetDatabase-path error; clean recompiles now report zero compiler errors.

## Repeatable verification

`Tools > Hollowfen > Verify Active UI Presentation` runs `ProductionUIVerifier` against the live
presentation. It checks:

- shared root-canvas scaling;
- effective hidden CanvasGroups that can still block pointer input;
- missing TMP fonts, clipped visible copy, and sub-minimum readable text;
- default focus and current EventSystem focus on interactive stack screens;
- undersized selectable hit targets;
- missing UI components;
- cutting-panel shadow containment.

Non-interactive loading/cinematic screens are intentionally exempt from the default-focus rule.

## Runtime matrix

All rows passed with `0 critical / 0 advisory` at 1280×800 after transitions settled.

| Presentation | Result |
|---|---|
| Main menu, save slots, loading, confirm modal | PASS |
| Settings: Audio, Graphics, Controls, Credits | PASS |
| Story index, Field Guide, Wren, model detail | PASS |
| Gameplay HUD and pause | PASS |
| Inventory and mushroom inspect | PASS |
| Map and location presentation | PASS |
| Village request and cultivation | PASS |
| Live NPC dialogue | PASS |
| Mushroom cutting: kneeling and cutting presentation | PASS |

Additional runtime assertions:

- normal menu → gameplay: one active EventSystem, modern input module, persistent UIManager;
- direct `Scene_Hollowfen` play: one active EventSystem, modern input module, no UIManager required;
- pause: `timeScale=0`, cursor unlocked/visible; resume restores gameplay policy;
- cutting kneel: `panelAlpha=0.00`, `outsideShadows=0`, gameplay HUD/minimap alpha `0.0`;
- save data was backed up before stateful tests and restored afterward.

## Evidence

The reviewed 1280×800 captures live in `Docs/screenshots/batch-87-ui-audit/`. The polished save-slot,
pause, loading, confirm, and cutting captures supplement the complete screen set recorded at the
start of the sweep.

## Deferred, outside UI scope

The village scene still emits third-party world-asset diagnostics for negative-scale BoxColliders,
two terrain tree prefabs without determinable bounds, and an unsupported TerrainCollider setup.
They do not come from UI and did not affect this pass, but they should be owned by the later world
dressing/collision cleanup before claiming the entire game scene is console-clean.
