# Batch 71 — Wren 3D Living Character Study

**Date:** 2026-07-15  
**Status:** implemented; compile, integrity, lint, runtime, and visual verification complete

## Goal

Put Wren's real 3D character model on the main-menu Wren page with the same tactile inspection affordance as the mushroom studies, then redesign the page so the model feels authored into the journal rather than dropped into a generic viewport.

## Delivered

- [x] Reusable Blender optimization pipeline that preserves Wren's humanoid armature while reducing the source from 542,469 to 89,999 triangles.
- [x] Dedicated Unity-owned journal model, 2K albedo/normal, 1K emission, URP material, and visual-only prefab.
- [x] `CharacterProfileData` references for the journal prefab, breathing idle, and preview exposure; idempotent editor importer wires them.
- [x] Isolated animated presenter with alpha compositing, a four-light portrait rig, runtime-only material clones, deterministic resource cleanup, drag/right-stick orbit, wheel/trigger zoom, and reset.
- [x] New two-column “living character study” top spread at 1280×800; original painting retained as an atmospheric layer.
- [x] Existing Background/Perspective dossier, kit, pull-quote, five study plates, explicit controller navigation, and Back behavior retained.

## Decisions

| Decision | Choice | Reason |
|---|---|---|
| Runtime asset | Dedicated optimized journal derivative | The existing gameplay source is 542k triangles and includes no menu-specific budget or presentation contract. |
| Preview object | Visual-only prefab, not `PlayerArmature` | Prevents player input, movement, camera-follow, collision, and audio systems from ever entering the menu. |
| Animation | `AnimationClipPlayable` bound to the model's humanoid Animator | Keeps the breathing idle without adding a controller or gameplay state machine. |
| Lighting | Four short-range point lights + low preview-only albedo emission | Stable portrait lighting on transparent black journal paper without touching Wren's gameplay material. |
| Page composition | Model-led atelier with biography facing it | Makes the new interactive artifact the visual focus while retaining the established dossier and supplied artwork. |

## Verification evidence

- Generated model: 89,999 triangles, 61,849 vertices, 33 skinned bones, valid humanoid avatar; source: 542,469 triangles / 304,206 vertices.
- Default runtime: one Wren preview rig, one renderer, 896×768 alpha RenderTexture, breathing playable active, no colliders or player/controller components.
- Interaction: rotation and zoom values change through the public pointer/gamepad path; reset restores the authored view.
- Close/reopen: preview rig deactivates off-page and resumes; runtime materials, graph, RenderTexture, and rig are destroyed with the presenter.
- Screenshots: `Docs/screenshots/batch-71/wren-living-study-1280x800.png` and `wren-inspected-1280x800.png`.
- Gates: compile clean; `DataIntegrity` 0 errors / 0 warnings; gotcha lint 0 errors / 0 warnings (one existing waiver); play smoke PASS.
