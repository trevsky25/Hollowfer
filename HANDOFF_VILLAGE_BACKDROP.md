# Demo 1 Village Backdrop — Status

## What's working

The full pre-built **Magic Pig Games "Medieval Environment - Demo 1"** village now loads as a single 62 MB GLB backdrop in the game scene. Concretely:

- **836 unique meshes / 54,096 mesh instances** rendering — stone walls, gatehouses, timber-framed houses, market wagon, food crates, well, fences, props
- **Auto-grounding via percentile statistics** — the loader walks every instance, samples its world-Y, takes the 5th percentile as "village floor" and shifts so foundations land on Y=0. Robust to outlier helper meshes (Unity terrain placeholder at Y=-103, sky stand-in at Y=+174) that previously broke naive bbox grounding.
- **Auto-centering** — same approach for X and Z (50th percentile = village center → shifted to world origin).
- **Terrain flattened** in the village area: edge-rise pushed out from `smoothstep(58,128)` to `smoothstep(195,230)` so the 360m-wide village doesn't sink into rising terrain.
- **Foliage exclusion** updated from `±75x78` to `±180x180` — trees won't spawn inside the village.
- **Wren spawns** at `(10, -20, facing -π/2)` — open ground at the south-west exterior of the main village wall. From there, walking north / east enters the town.
- **Zero console errors** beyond the pre-existing `THREE.FBXLoader: Vertex has more than 4 skinning weights` warnings (those are from the Wren character model, not the village).

## What was changed

| File | Change |
|---|---|
| [src/world/Village.js](src/world/Village.js) | Added `backdrop` mode + `_loadBackdrop()` with percentile-based auto-ground / auto-center. Existing per-prefab placement code untouched and still functional via the no-`backdrop` constructor path. |
| [src/main.js](src/main.js) | `loadVillage()` now passes `backdrop: { path, offset }` and an explicit `spawn`. Also flattened `terrainHeightAt()` over the village footprint and exposed `window.__hf` (scene/renderer/camera/player) for debug. |
| [src/world/Foliage.js](src/world/Foliage.js) | `VILLAGE_HALF_X` 75 → 180, `VILLAGE_HALF_Z` 78 → 180, `FOREST_OUTER` 200 → 220. |

The cleaned + compressed backdrop file lives at:
```
public/world/demo3/demo1_village_min.glb   (62 MB, the version we ship)
public/world/demo3/demo1_village_quality.glb   (143 MB, kept as comparison)
public/world/demo3/demo1_village_clean.glb   (1.84 GB source-of-truth)
public/world/demo3/demo1_village.glb   (1.84 GB original Unity export)
```

## Tuning knobs (in `src/main.js` `loadVillage()`)

```js
backdrop: {
  path: '/world/demo3/demo1_village_min.glb',
  offset: { x: 0, y: 0, z: 0 }   // ← adds on TOP of auto-centering
},
spawn: { x: 10, z: -20, facing: -Math.PI / 2 }
```

- **`backdrop.offset.y`** — positive = lift the village up. If buildings appear sunk into terrain, try `+2` to `+5`. If they float, try `-2` to `-5`.
- **`backdrop.offset.x` / `.z`** — shift the village laterally. Useful if you want Wren to spawn relative to a specific landmark.
- **`spawn.{x,z}`** — Wren's starting position. The village's actual building mass occupies roughly **X ∈ [-100, 60], Z ∈ [-140, 0]** in world space (from a density heatmap). Spawn outside this box for an "approach the village" feel; inside for a "you're in the town" feel.
- **`spawn.facing`** — radians, Three.js Y-axis convention. `0` = looking down -Z (north), `π/2` = looking down -X (west), `π` = south, `-π/2` = east.

## Known limitations (next iteration)

1. **No collision yet** — Wren can walk through walls. The Demo 1 backdrop is a single mesh blob; we'd need to either (a) compute AABBs from the visible meshes at load time and push into `worldState.colliders`, or (b) use raycasting per-frame for collision queries. Not blocking for visual review, but is the next priority.
2. **No enterable building interiors** — the Phase 2 "hybrid" plan (overlay the existing enterable mill / inn / chapel prefabs at matching coordinates inside the demo1 village, hiding the corresponding GLB meshes) is unimplemented. Today every building is decorative.
3. **Slight Y drift on hilly buildings** — Demo 1 was authored on Unity's hilly terrain. Some buildings sit a bit above / below their ideal Y because we're rendering them on flat-ish ground. Cosmetic only.
4. **Foliage spawn rules need revisiting** — the exclusion zone now matches the village footprint, but the `FOREST_OUTER` ring (220m) is tight against the terrain edge (±180-220m). May see thinner forest density at corners.
5. **Trees from the original prompt video are NOT loaded** — those came from the *Forest Environment - Dynamic Nature* asset pack which isn't owned. KayKit Forest Pack is the recommended free alternative for the next iteration.

## How to test

1. **Dev server**: it's running on `http://127.0.0.1:5175/` (or whatever port Vite picked — check with `lsof -ti:5175,5176,5177,5178`). Run `npm run dev` if it's stopped.
2. **Click "Begin Foraging"** on the main menu.
3. Should see Wren standing on grass with the village's stone wall to her right and a wooden fence line ahead.
4. **WASD** to walk around. **Right-mouse-drag** to rotate camera. Wren can walk through walls (no collision yet — see above).
5. The HUD's "current objective" pointer (top-left) shows the direction + distance to the next story-card landmark, which is now derived from the village backdrop's actual layout.

## Debug helpers

In the browser DevTools console:
```js
// Inspect everything
window.__hf
window.__hf.scene.traverse(o => o.name === 'VillageBackdrop' && console.log(o));

// Move the camera anywhere (one-frame; the follow camera will reclaim it)
window.__hf.camera.position.set(0, 100, 50);
window.__hf.camera.lookAt(0, 0, 0);
```

The `[Village] backdrop:` log line on page load reports the auto-placement values it computed.
