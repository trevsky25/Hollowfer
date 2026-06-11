import * as THREE from 'three';
import {
  createShrubGeometry,
  createGrassClumpGeometry,
  createRockGeometry,
  createMushroomClumpGeometry,
  createFlowerClumpGeometry,
  createTreeStumpGeometry,
  createFallenLogGeometry
} from './VegetationGeometry.js';
import { loadPBRTreeTemplates } from './PBRTrees.js';

// Default fallback footprint when the village hasn't loaded a backdrop with
// real bounds. Dropped values for X/Z half-extents and forest outer radius.
// When a backdrop GLB is loaded, the actual world-space village bounding box
// is passed in via createFoliage({ villageBounds }) and used instead.
const DEFAULT_VILLAGE_HALF_X = 180;
const DEFAULT_VILLAGE_HALF_Z = 180;
const FOREST_INNER_PAD = 6;
const FOREST_OUTER_RING = 280;  // distance forest extends past the village edge
const TREE_SAFE_RADIUS = 1.8;
const PLACEMENT_ATTEMPTS_PER_SLOT = 14;

function rand(seed) {
  const x = Math.sin(seed * 91.2 + 17.7) * 43758.5453;
  return x - Math.floor(x);
}

function aabbOverlapsCollider(x, z, radius, colliders) {
  for (const collider of colliders) {
    const nearestX = Math.max(collider.minX, Math.min(x, collider.maxX));
    const nearestZ = Math.max(collider.minZ, Math.min(z, collider.maxZ));
    const dx = x - nearestX;
    const dz = z - nearestZ;
    if (dx * dx + dz * dz < radius * radius) return true;
  }
  return false;
}

export async function createFoliage({ scene, terrainHeightAt, colliders, villageBounds = null }) {
  const foliage = new Foliage({ scene, terrainHeightAt, colliders, villageBounds });
  await foliage.load();
  return foliage;
}

class Foliage {
  constructor({ scene, terrainHeightAt, colliders, villageBounds }) {
    this.scene = scene;
    // villageBounds: optional { minX, maxX, minZ, maxZ, centerX, centerZ, halfX, halfZ }
    // computed from the loaded backdrop. When provided, foliage placement uses
    // these as the exclusion zone + forest ring center. Otherwise we fall back
    // to the origin-centered defaults.
    if (villageBounds) {
      this.bounds = villageBounds;
    } else {
      this.bounds = {
        minX: -DEFAULT_VILLAGE_HALF_X, maxX: DEFAULT_VILLAGE_HALF_X,
        minZ: -DEFAULT_VILLAGE_HALF_Z, maxZ: DEFAULT_VILLAGE_HALF_Z,
        centerX: 0, centerZ: 0,
        halfX: DEFAULT_VILLAGE_HALF_X, halfZ: DEFAULT_VILLAGE_HALF_Z
      };
    }
    this.terrainHeightAt = terrainHeightAt;
    this.colliders = colliders || [];
    this.group = new THREE.Group();
    this.group.name = 'Foliage';
    this.scene.add(this.group);

    // Largest radius from the village center the forest ring should reach.
    // = larger village half-extent + the ring depth. Foliage is randomized
    // around the village center within this radius.
    this.forestRadius = Math.max(this.bounds.halfX, this.bounds.halfZ) + FOREST_OUTER_RING;

    this.shrubMaterial = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.95,
      metalness: 0.0,
      envMapIntensity: 0.55
    });
    this.rockMaterial = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.92,
      metalness: 0.0,
      envMapIntensity: 0.7
    });
    this.stats = { trees: 0, shrubs: 0, grass: 0, rocks: 0 };
  }

  /** Returns true if (x, z) sits outside the village bounding box (with
   * an optional padding margin). Foliage in the forest ring should not
   * spawn inside this box. */
  outsideVillage(x, z, pad = FOREST_INNER_PAD) {
    const b = this.bounds;
    return x < b.minX - pad || x > b.maxX + pad || z < b.minZ - pad || z > b.maxZ + pad;
  }

  /** Random (x, z) inside the village box (used for placing decorative
   * items that are SUPPOSED to be inside the village area). */
  randomInsideVillage(seed, inset = 4) {
    const b = this.bounds;
    return {
      x: b.minX + inset + rand(seed) * (b.maxX - b.minX - inset * 2),
      z: b.minZ + inset + rand(seed * 2.1) * (b.maxZ - b.minZ - inset * 2)
    };
  }

  /** Random (x, z) somewhere within the forest ring around the village. */
  randomInForestRing(seed) {
    const r = this.forestRadius;
    return {
      x: this.bounds.centerX + (rand(seed) - 0.5) * (r * 2),
      z: this.bounds.centerZ + (rand(seed * 2.3) - 0.5) * (r * 1.7)
    };
  }

  /** Distance from a point to the village center. */
  distFromVillageCenter(x, z) {
    return Math.hypot(x - this.bounds.centerX, z - this.bounds.centerZ);
  }

  async load() {
    const templates = await loadPBRTreeTemplates();
    if (templates.length === 0) {
      console.warn('[Foliage] No PBR tree templates loaded');
    } else {
      this._placeTrees(templates, 540);
    }
    this._placeGroundCover();
    this._placeInnerVillage();
  }

  // Scatter small decorative elements (mushrooms, flowers, stumps, logs, grass
  // tufts, small rocks) INSIDE the village area but avoiding all building
  // footprints. These add the "lived-in" texture between buildings.
  _placeInnerVillage() {
    const dummy = new THREE.Object3D();

    const placeInsideVillage = (config) => {
      const { name, geometry, material, count, radius, scaleRange, yOffset = 0 } = config;
      const mesh = new THREE.InstancedMesh(geometry, material, count);
      mesh.castShadow = config.castShadow ?? false;
      mesh.receiveShadow = true;
      mesh.name = `Foliage:inner_${name}`;

      let placed = 0;
      let attempt = 0;
      const maxAttempts = count * PLACEMENT_ATTEMPTS_PER_SLOT;
      while (placed < count && attempt < maxAttempts) {
        attempt += 1;
        const seed = name.charCodeAt(0) * 13 + attempt * 0.97;
        const { x, z } = this.randomInsideVillage(seed, 4);
        if (aabbOverlapsCollider(x, z, radius, this.colliders)) continue;
        const scale = scaleRange[0] + rand(seed * 3.7) * (scaleRange[1] - scaleRange[0]);
        const rotation = rand(seed * 5.9) * Math.PI * 2;
        const groundY = this.terrainHeightAt(x, z);
        dummy.position.set(x, groundY + yOffset, z);
        dummy.rotation.set(0, rotation, 0);
        dummy.scale.set(scale, scale * (0.9 + rand(seed * 9.1) * 0.2), scale);
        dummy.updateMatrix();
        mesh.setMatrixAt(placed, dummy.matrix);
        placed += 1;
      }
      mesh.count = placed;
      mesh.instanceMatrix.needsUpdate = true;
      this.group.add(mesh);
      return placed;
    };

    const grassInner = placeInsideVillage({
      name: 'innergrass', geometry: createGrassClumpGeometry(),
      material: this.shrubMaterial, count: 220, radius: 0.5,
      scaleRange: [0.6, 1.1], yOffset: -0.04
    });
    const flowerInner = placeInsideVillage({
      name: 'innerflower', geometry: createFlowerClumpGeometry(1),
      material: this.shrubMaterial, count: 60, radius: 0.6,
      scaleRange: [0.7, 1.3], yOffset: 0
    });
    const flower2Inner = placeInsideVillage({
      name: 'innerflower2', geometry: createFlowerClumpGeometry(3),
      material: this.shrubMaterial, count: 50, radius: 0.6,
      scaleRange: [0.7, 1.3], yOffset: 0
    });
    const mushroomInner = placeInsideVillage({
      name: 'innermushroom', geometry: createMushroomClumpGeometry(),
      material: this.shrubMaterial, count: 28, radius: 0.6,
      scaleRange: [0.8, 1.4], yOffset: 0
    });
    const stumpInner = placeInsideVillage({
      name: 'innerstump', geometry: createTreeStumpGeometry(),
      material: this.rockMaterial, count: 8, radius: 0.9,
      scaleRange: [0.9, 1.3], yOffset: 0, castShadow: true
    });
    const logInner = placeInsideVillage({
      name: 'innerlog', geometry: createFallenLogGeometry(),
      material: this.rockMaterial, count: 5, radius: 1.4,
      scaleRange: [0.9, 1.2], yOffset: 0, castShadow: true
    });
    const rockInner = placeInsideVillage({
      name: 'innerrock', geometry: createRockGeometry(),
      material: this.rockMaterial, count: 35, radius: 0.7,
      scaleRange: [0.5, 1.0], yOffset: -0.05
    });

    this.stats.innerGrass = grassInner;
    this.stats.innerFlowers = flowerInner + flower2Inner;
    this.stats.innerMushrooms = mushroomInner;
    this.stats.innerStumps = stumpInner;
    this.stats.innerLogs = logInner;
    this.stats.innerRocks = rockInner;
  }


  _placeTrees(templates, totalCount) {
    const placementsPerTemplate = templates.map(() => []);
    let placed = 0;
    let attempt = 0;
    const maxAttempts = totalCount * PLACEMENT_ATTEMPTS_PER_SLOT;

    while (placed < totalCount && attempt < maxAttempts) {
      attempt += 1;
      const seed = 53 + attempt * 1.31;
      const { x, z } = this.randomInForestRing(seed);
      if (this.distFromVillageCenter(x, z) > this.forestRadius) continue;
      if (!this.outsideVillage(x, z)) continue;
      if (aabbOverlapsCollider(x, z, TREE_SAFE_RADIUS, this.colliders)) continue;

      const templateIndex = Math.floor(rand(seed * 5.7) * templates.length);
      const scale = 0.78 + rand(seed * 3.1) * 0.72;
      const rotation = rand(seed * 7.9) * Math.PI * 2;
      const groundY = this.terrainHeightAt(x, z);

      placementsPerTemplate[templateIndex].push({
        x,
        y: groundY - 0.05,
        z,
        scale,
        scaleY: scale * (0.92 + rand(seed * 11.3) * 0.18),
        rotation
      });
      placed += 1;
    }

    const dummy = new THREE.Object3D();
    for (let i = 0; i < templates.length; i++) {
      const placements = placementsPerTemplate[i];
      if (placements.length === 0) continue;
      const template = templates[i];
      for (const part of template.parts) {
        const mesh = new THREE.InstancedMesh(part.geometry, part.material, placements.length);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        mesh.name = `Foliage:tree_${i}`;
        for (let j = 0; j < placements.length; j++) {
          const placement = placements[j];
          dummy.position.set(placement.x, placement.y, placement.z);
          dummy.rotation.set(0, placement.rotation, 0);
          dummy.scale.set(placement.scale, placement.scaleY, placement.scale);
          dummy.updateMatrix();
          mesh.setMatrixAt(j, dummy.matrix);
        }
        mesh.instanceMatrix.needsUpdate = true;
        this.group.add(mesh);
      }
    }
    this.stats.trees = placed;
  }

  _placeGroundCover() {
    const dummy = new THREE.Object3D();

    const shrubMesh = new THREE.InstancedMesh(createShrubGeometry(), this.shrubMaterial, 240);
    shrubMesh.castShadow = true;
    shrubMesh.receiveShadow = true;
    shrubMesh.name = 'Foliage:shrub';
    let shrubPlaced = 0;
    let shrubAttempt = 0;
    while (shrubPlaced < 240 && shrubAttempt < 240 * PLACEMENT_ATTEMPTS_PER_SLOT) {
      shrubAttempt += 1;
      const seed = 191 + shrubAttempt * 0.91;
      const { x, z } = this.randomInForestRing(seed);
      if (this.distFromVillageCenter(x, z) > this.forestRadius) continue;
      if (!this.outsideVillage(x, z, 4)) continue;
      if (aabbOverlapsCollider(x, z, 0.9, this.colliders)) continue;
      const scale = 0.7 + rand(seed * 9.2) * 0.9;
      const rotation = rand(seed * 11.3) * Math.PI * 2;
      const groundY = this.terrainHeightAt(x, z);
      dummy.position.set(x, groundY - 0.05, z);
      dummy.rotation.set(0, rotation, 0);
      dummy.scale.set(scale, scale * (0.85 + rand(seed * 13.4) * 0.3), scale);
      dummy.updateMatrix();
      shrubMesh.setMatrixAt(shrubPlaced, dummy.matrix);
      shrubPlaced += 1;
    }
    shrubMesh.count = shrubPlaced;
    shrubMesh.instanceMatrix.needsUpdate = true;
    this.group.add(shrubMesh);

    const grassMesh = new THREE.InstancedMesh(createGrassClumpGeometry(), this.shrubMaterial, 540);
    grassMesh.receiveShadow = true;
    grassMesh.name = 'Foliage:grass';
    let grassPlaced = 0;
    let grassAttempt = 0;
    while (grassPlaced < 540 && grassAttempt < 540 * PLACEMENT_ATTEMPTS_PER_SLOT) {
      grassAttempt += 1;
      const seed = 311 + grassAttempt * 0.71;
      const { x, z } = this.randomInForestRing(seed);
      if (this.distFromVillageCenter(x, z) > this.forestRadius) continue;
      if (!this.outsideVillage(x, z, 0)) continue;
      if (aabbOverlapsCollider(x, z, 0.6, this.colliders)) continue;
      const scale = 0.8 + rand(seed * 5.1) * 1.1;
      const rotation = rand(seed * 7.9) * Math.PI * 2;
      const groundY = this.terrainHeightAt(x, z);
      dummy.position.set(x, groundY - 0.04, z);
      dummy.rotation.set(0, rotation, 0);
      dummy.scale.set(scale, scale * 0.9, scale);
      dummy.updateMatrix();
      grassMesh.setMatrixAt(grassPlaced, dummy.matrix);
      grassPlaced += 1;
    }
    grassMesh.count = grassPlaced;
    grassMesh.instanceMatrix.needsUpdate = true;
    this.group.add(grassMesh);

    const rockMesh = new THREE.InstancedMesh(createRockGeometry(), this.rockMaterial, 70);
    rockMesh.castShadow = true;
    rockMesh.receiveShadow = true;
    rockMesh.name = 'Foliage:rock';
    let rockPlaced = 0;
    let rockAttempt = 0;
    while (rockPlaced < 70 && rockAttempt < 70 * PLACEMENT_ATTEMPTS_PER_SLOT) {
      rockAttempt += 1;
      const seed = 503 + rockAttempt * 1.13;
      const { x, z } = this.randomInForestRing(seed);
      if (this.distFromVillageCenter(x, z) > this.forestRadius) continue;
      if (!this.outsideVillage(x, z, 6)) continue;
      if (aabbOverlapsCollider(x, z, 0.8, this.colliders)) continue;
      const scale = 0.7 + rand(seed * 8.4) * 1.4;
      const rotation = rand(seed * 12.7) * Math.PI * 2;
      const groundY = this.terrainHeightAt(x, z);
      dummy.position.set(x, groundY - 0.08, z);
      dummy.rotation.set(0, rotation, 0);
      dummy.scale.set(scale, scale * (0.7 + rand(seed * 14.5) * 0.5), scale);
      dummy.updateMatrix();
      rockMesh.setMatrixAt(rockPlaced, dummy.matrix);
      rockPlaced += 1;
    }
    rockMesh.count = rockPlaced;
    rockMesh.instanceMatrix.needsUpdate = true;
    this.group.add(rockMesh);

    this.stats.shrubs = shrubPlaced;
    this.stats.grass = grassPlaced;
    this.stats.rocks = rockPlaced;
  }

  dispose() {
    this.scene.remove(this.group);
    for (const child of this.group.children) {
      child.geometry?.dispose?.();
    }
    this.shrubMaterial.dispose();
    this.rockMaterial.dispose();
  }
}
