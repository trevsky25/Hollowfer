import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/examples/jsm/loaders/DRACOLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { VILLAGE_LAYOUT, INTERIORS } from './villageLayout.js';

const PREFAB_BASE = '/world/prefabs/';
const WALL_THICKNESS = 0.4;
const COLLIDER_TOP_FALLBACK = 8;

export class Village {
  constructor({ scene, terrainHeightAt, backdrop = null, spawn = null }) {
    this.scene = scene;
    this.terrainHeightAt = terrainHeightAt;
    this.group = new THREE.Group();
    this.group.name = 'Village';
    this.scene.add(this.group);

    // backdrop: { path } loads a single pre-baked village GLB instead of placing
    // individual prefabs. Used for the Magic Pig Games Demo 1 export which is
    // already a complete medieval town. When set, the per-prefab placement loop
    // is skipped entirely. Collision data is not derived from the backdrop yet
    // (Phase 2 follow-up).
    this.backdrop = backdrop;

    this.spawn = spawn || VILLAGE_LAYOUT.spawn;
    this.colliders = [];
    this.placedById = new Map();
    this.cache = new Map();

    const draco = new DRACOLoader();
    draco.setDecoderPath('/draco/gltf/');
    this.loader = new GLTFLoader();
    this.loader.setDRACOLoader(draco);
    this.loader.setMeshoptDecoder(MeshoptDecoder);
  }

  async load() {
    if (this.backdrop) {
      await this._loadBackdrop(this.backdrop.path);
      return { spawn: this.spawn, colliders: this.colliders };
    }

    const allEntries = [
      ...VILLAGE_LAYOUT.buildings,
      ...VILLAGE_LAYOUT.props
    ];

    const interiorAssets = new Set();
    for (const entry of VILLAGE_LAYOUT.buildings) {
      if (!entry.interior) continue;
      const interior = INTERIORS[entry.interior];
      if (!interior) continue;
      for (const item of interior.items) interiorAssets.add(item.asset);
    }

    const uniqueAssets = new Set([
      ...allEntries.map((e) => e.asset),
      ...interiorAssets
    ]);

    const loadPromises = Array.from(uniqueAssets).map((asset) => this._loadAsset(asset));
    const results = await Promise.allSettled(loadPromises);

    let failed = 0;
    Array.from(uniqueAssets).forEach((asset, i) => {
      if (results[i].status === 'rejected') {
        console.warn(`[Village] failed to load ${asset}:`, results[i].reason?.message || results[i].reason);
        failed += 1;
      }
    });
    if (failed > 0) console.warn(`[Village] ${failed}/${uniqueAssets.size} prefabs failed to load`);

    for (const entry of VILLAGE_LAYOUT.buildings) {
      this._placeBuilding(entry);
    }
    for (const entry of VILLAGE_LAYOUT.props) {
      this._placeProp(entry);
    }

    return { spawn: this.spawn, colliders: this.colliders };
  }

  async _loadBackdrop(path) {
    const gltf = await this.loader.loadAsync(path);
    gltf.scene.name = 'VillageBackdrop';

    // Walk meshes once to set shadow/cull flags. We deliberately don't try
    // to auto-center here: the GLB ships a baked TerrainMesh that shares a
    // coordinate system with the buildings, so any centering offset has to
    // apply to BOTH or nothing. Easiest "both" is "nothing" — load at world
    // origin and let the user nudge via backdrop.offset if needed.
    gltf.scene.traverse((obj) => {
      if (!obj.isMesh && !obj.isInstancedMesh) return;
      obj.castShadow = true;
      obj.receiveShadow = true;
      obj.frustumCulled = true;
    });

    const off = this.backdrop.offset || {};
    gltf.scene.position.set(off.x || 0, off.y || 0, off.z || 0);

    // Detect the baked Unity TerrainMesh. When present it becomes the only
    // ground in the village area — the player's ground-snap raycasts against
    // it via getBackdropHeightAt(), and main.js skips creating the procedural
    // sculpted plane.
    this.terrainMesh = null;
    gltf.scene.traverse((o) => {
      if (o.name === 'TerrainMesh' && (o.isMesh || o.isInstancedMesh)) {
        this.terrainMesh = o;
      }
    });
    if (this.terrainMesh) {
      this._terrainRaycaster = new THREE.Raycaster();
      this._terrainRayDir = new THREE.Vector3(0, -1, 0);
      this._applyTerrainMaterial();
    }

    this.group.add(gltf.scene);
    this.backdropScene = gltf.scene;

    // Phase A collision: outer perimeter walls + tower bases only. Skips
    // building interiors so Wren can still walk around freely inside the
    // village. Per-building shells are Phase B.
    this._buildPerimeterColliders();

    console.info(
      `[Village] backdrop loaded at`, gltf.scene.position.toArray(),
      `terrainMesh=${!!this.terrainMesh}`
    );
  }

  /** Swap the TerrainMesh's default white/gray Standard material for one
   * that uses the Magic Pig Games VillageGround02 texture set (resized to
   * 1K and saved under public/textures/ground/). The dirt is tinted mossy-
   * green via the material color so the hill looks like grass + dirt
   * rather than bare cobble. */
  _applyTerrainMaterial() {
    if (!this.terrainMesh) return;

    // gltf-transform's `optimize --palette` flag (default-on) remapped the
    // TerrainMesh's UVs to a tiny region of a shared palette texture. The
    // geometry's POSITION attribute also got quantized to int16 (the
    // dequantization scale lives in the node's matrixWorld). Fix: pull the
    // actual WORLD positions of each vertex (post-dequantization), then
    // build planar UVs from those — bypassing both the palette UV remap
    // and the position quantization.
    const geo = this.terrainMesh.geometry;
    const pos = geo.attributes.position;
    const tileSize = 10;  // metres per texture tile
    const uvs = new Float32Array(pos.count * 2);
    this.terrainMesh.updateMatrixWorld(true);
    const m = this.terrainMesh.matrixWorld;
    const v = new THREE.Vector3();
    for (let i = 0; i < pos.count; i++) {
      v.fromBufferAttribute(pos, i).applyMatrix4(m);
      uvs[i * 2]     = v.x / tileSize;
      uvs[i * 2 + 1] = v.z / tileSize;
    }
    geo.setAttribute('uv', new THREE.BufferAttribute(uvs, 2));
    console.info('[Village] regenerated TerrainMesh UVs from world position; vertex count:', pos.count);

    const texLoader = new THREE.TextureLoader();
    // aerial_grass_rock from Poly Haven (CC0). Natural grass with
    // scattered rocks/dirt patches — reads as a real medieval village
    // ground without needing a tint hack. The Unity demo's exact textures
    // come from Forest Environment - Dynamic Nature (paid asset, ~$35);
    // these free CC0 ones are visually equivalent for our purposes.
    const albedo = texLoader.load('/textures/ground/grass_diff.jpg');
    const normal = texLoader.load('/textures/ground/grass_nor.jpg');
    albedo.colorSpace = THREE.SRGBColorSpace;
    for (const tex of [albedo, normal]) {
      tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
      tex.anisotropy = 16;
      // No .repeat set — the geometry's UV step (1 unit per tileSize metres)
      // already handles tiling. Setting repeat would compound it.
    }

    this.terrainMesh.material = new THREE.MeshStandardMaterial({
      map: albedo,
      normalMap: normal,
      color: 0xffffff,  // texture is already real grass — no tint needed
      roughness: 0.95,
      metalness: 0
    });
    this.terrainMesh.receiveShadow = true;
  }

  /** Generate AABB colliders for every "solid" mesh in the loaded backdrop
   * — walls, towers, building shells, fences, larger props. Phase B-lite:
   * blocks Wren from walking through anything substantial. Trade-off:
   * doorways are also blocked (she can't enter buildings yet) — that's
   * the future "enterable buildings" task.
   *
   * Heuristic — a mesh's AABB becomes a collider when:
   *   - taller than 1.0m (excludes ground decals, small debris, mushrooms)
   *   - shorter than 80m (excludes any sky/helper outliers)
   *   - has at least one horizontal dimension >= 1.0m (excludes tiny posts)
   * Per-instance AABBs are computed for InstancedMesh so each placed wall
   * segment / building / cart becomes its own collider.
   */
  _buildPerimeterColliders() {
    if (!this.backdropScene) return;

    this.backdropScene.updateMatrixWorld(true);

    const _localBox = new THREE.Box3();
    const _tmp = new THREE.Matrix4();
    const _inst = new THREE.Matrix4();

    let added = 0;
    let scanned = 0;

    this.backdropScene.traverse((obj) => {
      if (obj === this.terrainMesh) return;
      if (!obj.isMesh && !obj.isInstancedMesh) return;

      // Build the list of world-space AABBs for this object (one per
      // instance for InstancedMesh, else just the mesh's own AABB).
      const aabbs = [];
      if (obj.isInstancedMesh) {
        if (!obj.geometry.boundingBox) obj.geometry.computeBoundingBox();
        _localBox.copy(obj.geometry.boundingBox);
        for (let i = 0; i < obj.count; i++) {
          obj.getMatrixAt(i, _tmp);
          _inst.multiplyMatrices(obj.matrixWorld, _tmp);
          aabbs.push(_localBox.clone().applyMatrix4(_inst));
        }
      } else {
        aabbs.push(new THREE.Box3().setFromObject(obj));
      }

      for (const box of aabbs) {
        scanned++;
        if (box.isEmpty()) continue;
        const sx = box.max.x - box.min.x;
        const sy = box.max.y - box.min.y;
        const sz = box.max.z - box.min.z;
        if (sy < 1.0 || sy > 80) continue;
        if (Math.max(sx, sz) < 1.0) continue;
        // Cap upper bound: real wall sections / towers / buildings max out
        // around 30m. Anything bigger is a `gltf-transform optimize --join`
        // artifact (many small meshes merged into one giant mesh whose AABB
        // spans the whole village). Skip — those colliders are useless.
        if (Math.max(sx, sz) > 30) continue;

        this.colliders.push({
          minX: box.min.x,
          maxX: box.max.x,
          minZ: box.min.z,
          maxZ: box.max.z,
          top: box.max.y,
          bottom: box.min.y,  // for "walk-under" check (overhangs, archways)
          name: `wall:${obj.name || 'mesh'}`
        });
        added++;
      }
    });

    console.info(
      `[Village] collision: scanned ${scanned} AABBs, added ${added} colliders.`
    );
  }

  /** Raycast straight down at (x, z) onto the backdrop's TerrainMesh ONLY.
   * Use for foliage placement and other "I want the natural ground here,
   * ignoring buildings/stairs/roofs" callers. Returns world Y or null. */
  getBackdropHeightAt(x, z) {
    if (!this.terrainMesh) return null;
    this.terrainMesh.updateMatrixWorld(true);
    this._terrainRaycaster.set(
      new THREE.Vector3(x, 1000, z),
      this._terrainRayDir
    );
    this._terrainRaycaster.far = 2000;
    const hits = this._terrainRaycaster.intersectObject(this.terrainMesh, false);
    return hits.length ? hits[0].point.y : null;
  }

  /** Raycast straight down at (x, z) onto the ENTIRE backdrop scene —
   * terrain, building floors, porches, stair tops, etc. The ray starts
   * from `fromY` and casts downward; pass `fromY = playerY + 2` so the
   * ray doesn't hit roofs Wren is standing under. Returns the highest
   * surface beneath the ray origin (world Y), or null if nothing hit.
   *
   * This is the "what surface is Wren actually standing on?" query that
   * makes stairs / decks / ramps work — Wren's Y rises naturally as she
   * walks up. */
  getStandableHeightAt(x, z, fromY = 1000) {
    if (!this.backdropScene) return null;
    this.backdropScene.updateMatrixWorld(true);
    this._terrainRaycaster.set(
      new THREE.Vector3(x, fromY, z),
      this._terrainRayDir
    );
    this._terrainRaycaster.far = fromY + 200;
    const hits = this._terrainRaycaster.intersectObject(this.backdropScene, true);
    return hits.length ? hits[0].point.y : null;
  }

  async _loadAsset(asset) {
    if (this.cache.has(asset)) return this.cache.get(asset);
    const promise = this.loader.loadAsync(PREFAB_BASE + asset).then((gltf) => {
      gltf.scene.traverse((obj) => {
        if (!obj.isMesh) return;
        obj.castShadow = true;
        obj.receiveShadow = true;
        obj.frustumCulled = true;
        const materials = Array.isArray(obj.material) ? obj.material : [obj.material];
        for (const mat of materials) {
          if (!mat) continue;
          mat.envMapIntensity = 1.0;
          if (mat.map) mat.map.anisotropy = 8;
        }
      });

      // Wrap the loaded scene in a parent group and shift the inner scene so
      // the parent's pivot is at (centerX=0, centerZ=0, floorY=0). This way
      // every clone, when positioned at (x, terrainY, z), lands with its
      // building center at (x, z) and floor sitting on the terrain.
      const wrapper = new THREE.Group();
      wrapper.name = `Prefab:${asset}`;
      gltf.scene.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(gltf.scene);
      if (!box.isEmpty()) {
        const center = box.getCenter(new THREE.Vector3());
        gltf.scene.position.set(-center.x, -box.min.y, -center.z);
      }
      wrapper.add(gltf.scene);
      wrapper.userData.box = box;
      return wrapper;
    });
    this.cache.set(asset, promise);
    return promise;
  }

  async _instance(asset) {
    const cached = await this._loadAsset(asset);
    return cached.clone(true);
  }

  _placeBuilding(entry) {
    const cached = this.cache.get(entry.asset);
    if (!cached) return;
    cached.then((scene) => {
      const obj = scene.clone(true);
      obj.name = `Building:${entry.id}`;
      const ground = this.terrainHeightAt(entry.x, entry.z);
      obj.position.set(entry.x, ground, entry.z);
      obj.rotation.y = entry.rotY || 0;
      this.group.add(obj);
      this.placedById.set(entry.id, obj);

      obj.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(obj);
      if (box.isEmpty()) return;

      this._addBuildingColliders(entry, box);

      if (entry.interior && INTERIORS[entry.interior]) {
        this._placeInterior(obj, INTERIORS[entry.interior]);
      }
    });
  }

  _placeProp(entry) {
    const cached = this.cache.get(entry.asset);
    if (!cached) return;
    cached.then((scene) => {
      const obj = scene.clone(true);
      obj.name = `Prop:${entry.id}`;
      const ground = this.terrainHeightAt(entry.x, entry.z);
      obj.position.set(entry.x, ground + (entry.yOffset || 0), entry.z);
      obj.rotation.y = entry.rotY || 0;
      this.group.add(obj);
      this.placedById.set(entry.id, obj);

      // Skip colliders for signs / hanging things (they sit above head height
      // and players shouldn't bump into them) and very small props.
      if ((entry.yOffset || 0) > 1.2) return;

      obj.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(obj);
      if (box.isEmpty()) return;
      const size = box.getSize(new THREE.Vector3());
      if (size.y < 0.4) return;

      this.colliders.push({
        minX: box.min.x,
        maxX: box.max.x,
        minZ: box.min.z,
        maxZ: box.max.z,
        top: box.max.y,
        name: `prop:${entry.id}`
      });
    });
  }

  _placeInterior(buildingObj, interior) {
    for (const item of interior.items) {
      const cached = this.cache.get(item.asset);
      if (!cached) continue;
      cached.then((scene) => {
        const obj = scene.clone(true);
        obj.name = `Interior:${item.asset}`;
        obj.position.set(item.localX || 0, item.localY || 0, item.localZ || 0);
        obj.rotation.y = item.rotY || 0;
        buildingObj.add(obj);
      });
    }
  }

  // Synthesize 4 wall colliders for a building's footprint, with an optional
  // doorway gap on one side. The doorway is specified in the building's local
  // pre-rotation frame ('+x', '-x', '+z', '-z'); we rotate the gap into world
  // space using rotY.
  _addBuildingColliders(entry, box) {
    const minX = box.min.x;
    const maxX = box.max.x;
    const minZ = box.min.z;
    const maxZ = box.max.z;
    const top = box.max.y || COLLIDER_TOP_FALLBACK;
    const t = WALL_THICKNESS;

    const sides = [
      { side: '-z', minX,           maxX,           minZ: minZ,         maxZ: minZ + t,    axis: 'x' },
      { side: '+z', minX,           maxX,           minZ: maxZ - t,     maxZ: maxZ,        axis: 'x' },
      { side: '-x', minX,           maxX: minX + t, minZ,               maxZ,              axis: 'z' },
      { side: '+x', minX: maxX - t, maxX,           minZ,               maxZ,              axis: 'z' }
    ];

    const doorwaySide = entry.doorway ? this._worldDoorwaySide(entry.doorway.side, entry.rotY || 0) : null;

    for (const wall of sides) {
      const isDoorwayWall = doorwaySide === wall.side;
      if (!isDoorwayWall) {
        this.colliders.push({
          minX: wall.minX,
          maxX: wall.maxX,
          minZ: wall.minZ,
          maxZ: wall.maxZ,
          top,
          name: `wall:${entry.id}:${wall.side}`
        });
        continue;
      }

      // Punch a gap. doorway.center is along the wall's primary axis (x for
      // ±z walls, z for ±x walls). doorway.width is the gap size.
      const gapWidth = entry.doorway.width;
      const gapCenter = entry.doorway.center;

      if (wall.axis === 'x') {
        const wallCenterX = (wall.minX + wall.maxX) / 2;
        const gapMinX = wallCenterX + gapCenter - gapWidth / 2;
        const gapMaxX = wallCenterX + gapCenter + gapWidth / 2;
        if (wall.minX < gapMinX) {
          this.colliders.push({
            minX: wall.minX, maxX: gapMinX, minZ: wall.minZ, maxZ: wall.maxZ, top,
            name: `wall:${entry.id}:${wall.side}:left`
          });
        }
        if (gapMaxX < wall.maxX) {
          this.colliders.push({
            minX: gapMaxX, maxX: wall.maxX, minZ: wall.minZ, maxZ: wall.maxZ, top,
            name: `wall:${entry.id}:${wall.side}:right`
          });
        }
      } else {
        const wallCenterZ = (wall.minZ + wall.maxZ) / 2;
        const gapMinZ = wallCenterZ + gapCenter - gapWidth / 2;
        const gapMaxZ = wallCenterZ + gapCenter + gapWidth / 2;
        if (wall.minZ < gapMinZ) {
          this.colliders.push({
            minX: wall.minX, maxX: wall.maxX, minZ: wall.minZ, maxZ: gapMinZ, top,
            name: `wall:${entry.id}:${wall.side}:near`
          });
        }
        if (gapMaxZ < wall.maxZ) {
          this.colliders.push({
            minX: wall.minX, maxX: wall.maxX, minZ: gapMaxZ, maxZ: wall.maxZ, top,
            name: `wall:${entry.id}:${wall.side}:far`
          });
        }
      }
    }
  }

  // Map a local doorway side ('+x' etc.) through the building's rotY to a
  // world-space side. Snaps to the nearest cardinal direction.
  _worldDoorwaySide(localSide, rotY) {
    const baseDir = { '+x': 0, '+z': Math.PI / 2, '-x': Math.PI, '-z': -Math.PI / 2 }[localSide];
    if (baseDir === undefined) return null;
    const worldAngle = baseDir + rotY;
    const norm = Math.atan2(Math.sin(worldAngle), Math.cos(worldAngle));
    if (norm > -Math.PI / 4 && norm <= Math.PI / 4) return '+x';
    if (norm > Math.PI / 4 && norm <= (3 * Math.PI) / 4) return '+z';
    if (norm > (3 * Math.PI) / 4 || norm <= -(3 * Math.PI) / 4) return '-x';
    return '-z';
  }
}
