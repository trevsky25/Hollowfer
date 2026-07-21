# Batch 92 — Current-location map marker

**Date:** 2026-07-16 · **Status:** verified; awaiting commit/tag

## Goal

Make Wren's current position immediately recognizable whenever the full map opens, without confusing it with
landmark or waypoint pins.

## Implementation

- Replaced the full map's tiny standalone heading triangle with a 38px current-location marker composed of a
  dark circular plate, cream directional arrow, cream rim, and gently pulsing sage outer ring.
- Added an upright `YOU ARE HERE` chip below the marker, localized through `map.current_location`.
- Kept the current-location object after `POIRoot` in the procedural hierarchy so it consistently renders above
  landmark dots and their labels.
- Extended `PlayerHeadingArrow.Configure` with optional heading-glyph and pulse-ring children. On the full map,
  only the arrow rotates with Wren and the pulse uses unscaled time; the marker plate and label remain readable.
  Existing mini-map behavior is unchanged when those optional children are omitted.
- Preserved world-space map projection and off-map hiding, so panning/zooming cannot leave a false marker pinned
  to the screen edge.

## Verification evidence

- Unity C# refresh/compile: zero errors in `MapScreen.cs` and `PlayerHeadingArrow.cs`.
- Play-mode visual check at the 4K Game-view target: the marker is visually distinct from POI pins, the label is
  legible, the pulse remains subtle, and the arrow reports Wren's facing direction.
- Closed and reopened the full map in Play mode: the marker rebuilt/stayed visible and returned to Wren's live
  projected position.
- Cycled focused POIs while the map was open: focus and the side card still work, and POI labels do not obscure
  the current-location treatment.
- No new C# exceptions were emitted during the map interaction pass; remaining terrain/collider warnings are
  pre-existing scene/vendor warnings.

## Files changed

- `Assets/_Hollowfen/Scripts/Localization.cs`
- `Assets/_Hollowfen/Scripts/Map/MapScreen.cs`
- `Assets/_Hollowfen/Scripts/Map/PlayerHeadingArrow.cs`
- `Docs/systems/map.md`
- `Docs/worksheets/batch-92-current-location-map-marker.md`
