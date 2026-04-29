import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/examples/jsm/loaders/DRACOLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { VILLAGE_LAYOUT, INTERIORS } from './villageLayout.js';

const PREFAB_BASE = '/world/prefabs/';
const WALL_THICKNESS = 0.4;
const COLLIDER_TOP_FALLBACK = 8;

export class Village {
  constructor({ scene, terrainHeightAt }) {
    this.scene = scene;
    this.terrainHeightAt = terrainHeightAt;
    this.group = new THREE.Group();
    this.group.name = 'Village';
    this.scene.add(this.group);

    this.spawn = VILLAGE_LAYOUT.spawn;
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
