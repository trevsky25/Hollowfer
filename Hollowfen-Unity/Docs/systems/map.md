# Map System
Full-bleed full-screen map + mini-map corner widget + compass strip, all rendering the real 3D world via ortho cameras → RenderTextures → UI. Full map supports pan, two zoom presets, clamped framing, POI discovery/waypoints, region display, and a live labeled current-location marker with Wren's heading.
Key scripts: `Assets/_Hollowfen/Scripts/Map/` — MapScreen, MapCamera, MapInputBridge, LocationData, LocationMarker, LocationMarkerOverlay, LocationRegistry, RegionCatalog, RegionTrigger, MiniMapCamera, MiniMapWidget, PlayerHeadingArrow; `UI/RegionArrivalToast`.
Data: `Data/Locations/LocationData_*.asset` (14 POIs incl. `clear_cut` + `manor`; only FathersMill `_discoveredByDefault`); discovery key = `_id`, names/descriptions via localization keys (`loc.<id>.name/.desc`). Regions: `village`/`wend`/`old_wood`/`manor` — `_regionId` renders via MapScreen `LocalizeRegion` (add a `case` per new region or it shows the raw id).
Entry points: `Player/OpenMap` (M / Select / touchpad) toggles, `UI/Cancel` closes (MapInputBridge); inside: arrows/D-pad cycle POIs, Enter/A toggles waypoint, Tab/RB zoom preset, F/R3 recenter.
Biggest gotchas: `BuildIfNeeded` DESTROYS all children of the map canvas on first open; `MapCamera.Awake` creates a runtime 2048×1024 RT that orphans any inspector-assigned RT asset; the current-location marker must remain after `POIRoot` in sibling order so POI labels cannot cover it.
Status: current-location marker, map reopen, POI focus, and regional routing verified in Play mode 2026-07-16.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## MapScreen (full-bleed, all procedural)

Built on first `Open()` (`BuildIfNeeded` → `BuildUI`), 1920×1080 reference. **Destroys all existing canvas children first** (captures the RT beforehand).

- **MapZone**: full-stretch minus TopBar (64px) / BottomBar (56px) → 1920×960 = the RT's 2:1 aspect, pixel-true. Contains the RawImage, soft edge vignettes (gradient sprites), `POIRoot` + `LocationMarkerOverlay` (icon 18f, labels on, undiscovered included), a live `CurrentLocationMarker`, and a single "N" chip top-center (the 8 compass pills are gone — map always renders +Z up). The player marker uses a dark circular plate, cream direction glyph, sage pulse, and localized `YOU ARE HERE` chip; only the glyph rotates, keeping the label upright.
- **TopBar** (ink glass + gold hairline): serif "Hollowfen", eyebrow "FIELD JOURNAL · CARTOGRAPHY", RegionChip + ZoomChip ("VILLAGE"/"REGIONAL").
- **BottomBar**: keycap-pill hints via HorizontalLayoutGroup + ContentSizeFitter (TMP preferred sizes unreliable on creation frame — sizing left to layout system).
- **SideCard** (380×640, right-anchored): rounded parchment card — eyebrow/title/body (via `Localization.Get`), DISTANCE + REGION stats, gold Waypoint button.
- **Input** (`Update()` polls devices directly, unscaled time): left-stick/WASD pan (speed scaled by `orthoSize/ZoomRegional` so it feels constant across zoom) · middle-mouse drag pan (`worldPerPx = ortho*2 / zoneWidth`) · arrows/D-pad = directional POI focus cycling (viewport-space scan, ~60° cone, `forwardDist + 2*perpDist` scoring) · mouse hover/click hit-tests via `overlay.HitTestScreenPoint` · Enter/Space/A waypoint toggle (set requires discovered; clear always) · Tab/=/−/RB/LB/scroll = `ToggleZoomPreset()` (⚠️ scroll is a toggle, not incremental) · Home/F/R3 recenter.
- **Open**: hide mini-map + HUD (`GameObject.Find("_HUDCanvas")` + CanvasGroup alpha 0), snap `CenterOnPlayer()` + regional zoom, `AutoFocusClosestMarker()` (discovered-in-frame → any visible → any) so pad users always have a target. **Close**: reverse + recenter + clear focus. `_freezeTimeWhileOpen` (default true) = timeScale 0 with restore.
- ⚠️ `Awake` only deactivates when `!_isOpen` — guards against the scene-saved-inactive canvas firing Awake during `Open()`'s SetActive.

## MapCamera

- Runtime RT in `Awake` (2048×1024, ARGB32, 4×MSAA, 24-bit depth, named `MapViewRT_Runtime`) → `_cam.targetTexture`; inspector RT assets intentionally orphaned. Public `RenderTexture` getter.
- **Modes**: `FollowPlayer` (default; re-anchors to `"Player"` tag at height 80) ⇄ `Free` (any `Pan()` flips to Free; `CenterOnPlayer()` flips back). Ortho, rotation locked (90,0,0), solid moss clear color as "unmapped land" backstop. Culls the `Foraging` layer (player renders magenta top-down otherwise).
- **Zoom**: two presets — `_zoomClose` 60 / `_zoomRegional` 150 ortho; exponential lerp (`_zoomLerpSpeed` 8, unscaled), re-clamps after every change.
- **Clamping**: clamps the visible FRAME, not the center — `half = ortho × aspect` per axis, center ∈ `[min+half, max−half]`, pins to world middle if the frame exceeds the world. `_worldBounds` auto-tightens in Awake to `Terrain.activeTerrain` position+size (default Rect 0,0,500,500 without terrain). Awake also caps all ortho values to `min(boundsW/(2·aspect), boundsH/2)` — the fix for the old "black void" bug.
- **Fog**: `beginCameraRendering` saves fog+skybox state, disables fog, restores after (URP fog-at-altitude gotcha).

## Locations (data → pins → discovery)

- **LocationData** SO: `_id` (persistence key) · `_displayNameId`/`_shortDescriptionId` (localization) · `_mapIcon` (⚠️ vestigial — pins are procedural, all 8 assets null) · `_discoveredByDefault` · `_regionId` (`village`/`old_wood`/`wend`).
- **LocationMarker** (scene): holds the SO + `_discoverRadius` (20m default, 0 = never). Registers with LocationRegistry OnEnable (⚠️ must STAY enabled after discovery — disabling unregisters). Discovery check throttled to 1/0.5s per marker, XZ distance vs the `"Player"` tag, on unscaled time.
- **LocationMarkerOverlay** (on MapImage; mini-map reuses it with labels off): per-LateUpdate `WorldToViewportPoint` projection into a center-anchored container; pooled procedural pins (waypoint = pulsing gold halo; discovered = gold dot + name chip; undiscovered = 0.4-alpha dot + "?" chip only while focused; focused = GoldGlow + 1.45× scale; waypoint always shows its real name even undiscovered — "a quest pointed Wren there by name"). Chip width re-measured every frame (TMP trap). `SetFocusedId/ClearFocus/HitTestScreenPoint` API.
- **PlayerHeadingArrow**: resolves the tagged player, projects her position into the full-map container each `LateUpdate`, and hides the marker if it leaves the visible camera frame. Full-map configuration supplies separate heading/pulse children so the arrow rotates and the ring pulses on unscaled time while the plate and label stay upright; mini-map callers that omit those children retain legacy whole-marker rotation.
- **LocationRegistry** (static): markers + `_discovered` set + regions + `ActiveWaypoint`. Events: `LocationDiscovered(id)`, `RegionChanged(regionId)`, `WaypointChanged(marker)`. Persistence: `SaveSlotMeta.DiscoveredLocationIds` — lazy hydrate + immediate `AutoSaveDiscoveredLocations` on discovery; `HydrateFromSave` re-applies defaults. Waypoint NOT persisted; auto-clears when its marker unregisters. `ResetOnLoad` clears everything incl. events (domain-reload-off safe). Quest waypoints: `QuestManager.StartQuest` matches `QuestData.WaypointLocation` → `SetWaypoint`.
- **RegionTrigger**: BoxCollider trigger push/pops on player enter/exit; highest-`Priority` active trigger wins. Disabling a trigger pops it, preventing story swaps from pinning stale state. Six volumes cover northern/southern village, Wend crossing/clear-cut, Old Wood, and manor.
- **RegionCatalog**: four stable ids (`village`, `wend`, `old_wood`, `manor`) → localized display name/subtitle with English fallback. Map chrome, audio, and arrival presentation share it.
- **RegionArrivalToast**: code-built order-14 overlay below map/dialogue. Fades/slides at the top center using unscaled time, captures no input, pauses nothing, and ignores null/unknown regions.

## POI placement workflow (now trivial)

1. `LocationData` asset in `Data/Locations/` (+ `loc.<id>.name`/`.desc` in the Localization table).
2. Empty GameObject at the world spot in `Scene_Hollowfen` + `LocationMarker` + assign asset + tune radius.
3. Done — registration/rendering automatic on both maps. No prefab, no manual list.

## Gotchas

- Stale header comment + dead `BuildFrame()` in MapScreen.cs — parchment-era leftovers.
- Hardcoded display strings still bypass localization for "VILLAGE"/"REGIONAL", "HOLLOWFEN", "N", "?", "FIELD JOURNAL · CARTOGRAPHY", and bottom-bar hints. Region names/subtitles are now localized through `RegionCatalog`.
- Find-by-name/tag coupling: `_HUDCanvas` by name; `"Player"` tag in MapScreen, MapCamera, LocationMarker.
- Everything animates on unscaled time (timeScale 0 while open); discovery also ticks on unscaled time.
- Scene without an active terrain → default 500×500 clamp rect at origin (wrong for any real world).
- Region volumes are broad authored boxes rather than a full terrain partition; traversal QA should tune boundaries if final roads/world dressing move.
