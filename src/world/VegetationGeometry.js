import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

const TRUNK_BROWN = new THREE.Color(0x4a311a);
const TRUNK_DARK = new THREE.Color(0x35221a);
const TRUNK_BIRCH = new THREE.Color(0xcec7b4);
const LEAF_OAK = new THREE.Color(0x415d2e);
const LEAF_OAK_DARK = new THREE.Color(0x32492a);
const LEAF_PINE = new THREE.Color(0x2a4a32);
const LEAF_PINE_DARK = new THREE.Color(0x1d3a26);
const LEAF_BIRCH = new THREE.Color(0x6f9252);
const SHRUB_GREEN = new THREE.Color(0x3f5b32);
const SHRUB_DRY = new THREE.Color(0x5c6a3a);
const GRASS_GREEN = new THREE.Color(0x4f6a3a);
const ROCK_GREY = new THREE.Color(0x6b6760);
const ROCK_DARK = new THREE.Color(0x47433d);

function colorize(geometry, color) {
  const count = geometry.attributes.position.count;
  const colors = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) {
    colors[i * 3] = color.r;
    colors[i * 3 + 1] = color.g;
    colors[i * 3 + 2] = color.b;
  }
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  return geometry;
}

function part(geometry, color, opts = {}) {
  if (opts.scale) {
    const s = opts.scale;
    geometry.scale(Array.isArray(s) ? s[0] : s, Array.isArray(s) ? s[1] : s, Array.isArray(s) ? s[2] : s);
  }
  if (opts.translate) {
    const [x, y, z] = opts.translate;
    geometry.translate(x, y, z);
  }
  const flat = geometry.index ? geometry.toNonIndexed() : geometry;
  return colorize(flat, color);
}

function jitterIcosahedron(geometry, seed) {
  const pos = geometry.attributes.position;
  for (let i = 0; i < pos.count; i++) {
    const j = ((i * 9301 + seed * 49297) % 233280) / 233280;
    const k = ((i * 4523 + seed * 27361) % 233280) / 233280;
    const m = ((i * 7919 + seed * 11173) % 233280) / 233280;
    const f = 0.85 + j * 0.3;
    pos.setXYZ(i, pos.getX(i) * f, pos.getY(i) * (0.85 + k * 0.3), pos.getZ(i) * (0.85 + m * 0.3));
  }
  pos.needsUpdate = true;
  geometry.computeVertexNormals();
  return geometry;
}

export function createOakGeometry() {
  const parts = [
    part(new THREE.CylinderGeometry(0.28, 0.46, 4.6, 8), TRUNK_BROWN, { translate: [0, 2.3, 0] }),
    part(new THREE.IcosahedronGeometry(2.1, 1), LEAF_OAK, { translate: [0, 5.4, 0] }),
    part(new THREE.IcosahedronGeometry(1.6, 1), LEAF_OAK_DARK, { translate: [1.1, 4.9, 0.5] }),
    part(new THREE.IcosahedronGeometry(1.55, 1), LEAF_OAK, { translate: [-0.95, 5.0, -0.7] }),
    part(new THREE.IcosahedronGeometry(1.3, 1), LEAF_OAK_DARK, { translate: [0.0, 6.3, 0.0] })
  ];
  return mergeGeometries(parts, false);
}

export function createPineGeometry() {
  const parts = [
    part(new THREE.CylinderGeometry(0.18, 0.30, 6.4, 8), TRUNK_DARK, { translate: [0, 3.2, 0] }),
    part(new THREE.ConeGeometry(2.0, 2.6, 8), LEAF_PINE, { translate: [0, 4.6, 0] }),
    part(new THREE.ConeGeometry(1.55, 2.1, 8), LEAF_PINE_DARK, { translate: [0, 5.9, 0] }),
    part(new THREE.ConeGeometry(1.05, 1.7, 8), LEAF_PINE, { translate: [0, 7.2, 0] }),
    part(new THREE.ConeGeometry(0.65, 1.2, 8), LEAF_PINE_DARK, { translate: [0, 8.3, 0] })
  ];
  return mergeGeometries(parts, false);
}

export function createBirchGeometry() {
  const parts = [
    part(new THREE.CylinderGeometry(0.16, 0.22, 5.8, 8), TRUNK_BIRCH, { translate: [0, 2.9, 0] }),
    part(new THREE.IcosahedronGeometry(1.3, 1), LEAF_BIRCH, { scale: [1.0, 1.45, 1.0], translate: [0, 5.2, 0] }),
    part(new THREE.IcosahedronGeometry(1.0, 1), LEAF_BIRCH, { scale: [1.0, 1.25, 1.0], translate: [0.55, 6.1, -0.35] }),
    part(new THREE.IcosahedronGeometry(0.9, 1), LEAF_BIRCH, { scale: [1.0, 1.25, 1.0], translate: [-0.45, 5.6, 0.45] })
  ];
  return mergeGeometries(parts, false);
}

export function createShrubGeometry() {
  const parts = [
    part(new THREE.IcosahedronGeometry(0.7, 1), SHRUB_GREEN, { translate: [0, 0.55, 0] }),
    part(new THREE.IcosahedronGeometry(0.55, 1), SHRUB_DRY, { translate: [0.5, 0.45, 0.25] }),
    part(new THREE.IcosahedronGeometry(0.5, 1), SHRUB_GREEN, { translate: [-0.45, 0.4, -0.3] })
  ];
  return mergeGeometries(parts, false);
}

export function createGrassClumpGeometry() {
  const parts = [
    part(new THREE.IcosahedronGeometry(0.32, 0), GRASS_GREEN, { scale: [1.2, 0.5, 1.2], translate: [0, 0.16, 0] }),
    part(new THREE.IcosahedronGeometry(0.22, 0), GRASS_GREEN, { scale: [1.2, 0.5, 1.2], translate: [0.32, 0.12, 0.18] }),
    part(new THREE.IcosahedronGeometry(0.18, 0), GRASS_GREEN, { scale: [1.2, 0.5, 1.2], translate: [-0.28, 0.1, -0.22] })
  ];
  return mergeGeometries(parts, false);
}

export function createRockGeometry() {
  const parts = [
    part(jitterIcosahedron(new THREE.IcosahedronGeometry(0.7, 0), 11), ROCK_GREY, { translate: [0, 0.45, 0] }),
    part(jitterIcosahedron(new THREE.IcosahedronGeometry(0.5, 0), 17), ROCK_DARK, { translate: [0.6, 0.3, 0.15] }),
    part(jitterIcosahedron(new THREE.IcosahedronGeometry(0.4, 0), 23), ROCK_GREY, { translate: [-0.42, 0.25, -0.42] })
  ];
  return mergeGeometries(parts, false);
}

const MUSHROOM_STEM = new THREE.Color(0xeae0c8);
const MUSHROOM_CAP_RED = new THREE.Color(0xb83a2c);
const MUSHROOM_CAP_BROWN = new THREE.Color(0x7a5236);
const MUSHROOM_CAP_GOLD = new THREE.Color(0xc89d3a);

export function createMushroomGeometry(capColor = MUSHROOM_CAP_RED) {
  const parts = [
    part(new THREE.CylinderGeometry(0.06, 0.08, 0.32, 8), MUSHROOM_STEM, { translate: [0, 0.16, 0] }),
    part(new THREE.SphereGeometry(0.18, 8, 6, 0, Math.PI * 2, 0, Math.PI / 2), capColor, { translate: [0, 0.32, 0] })
  ];
  return mergeGeometries(parts, false);
}

export function createMushroomClumpGeometry() {
  const parts = [
    part(new THREE.CylinderGeometry(0.06, 0.08, 0.30, 8), MUSHROOM_STEM, { translate: [0, 0.15, 0] }),
    part(new THREE.SphereGeometry(0.16, 8, 6, 0, Math.PI * 2, 0, Math.PI / 2), MUSHROOM_CAP_RED, { translate: [0, 0.30, 0] }),
    part(new THREE.CylinderGeometry(0.05, 0.07, 0.22, 8), MUSHROOM_STEM, { translate: [0.20, 0.11, 0.10] }),
    part(new THREE.SphereGeometry(0.13, 8, 6, 0, Math.PI * 2, 0, Math.PI / 2), MUSHROOM_CAP_BROWN, { translate: [0.20, 0.22, 0.10] }),
    part(new THREE.CylinderGeometry(0.04, 0.06, 0.18, 8), MUSHROOM_STEM, { translate: [-0.15, 0.09, -0.13] }),
    part(new THREE.SphereGeometry(0.10, 8, 6, 0, Math.PI * 2, 0, Math.PI / 2), MUSHROOM_CAP_GOLD, { translate: [-0.15, 0.18, -0.13] })
  ];
  return mergeGeometries(parts, false);
}

const FLOWER_COLORS = [0xff5a82, 0xffd24a, 0xa9c8ff, 0xe8e8e8, 0xc78ad9, 0xff8a44];

export function createFlowerClumpGeometry(seed = 1) {
  const stemColor = new THREE.Color(0x6a8a4f);
  const colorA = new THREE.Color(FLOWER_COLORS[seed % FLOWER_COLORS.length]);
  const colorB = new THREE.Color(FLOWER_COLORS[(seed + 2) % FLOWER_COLORS.length]);
  const colorC = new THREE.Color(FLOWER_COLORS[(seed + 4) % FLOWER_COLORS.length]);
  const parts = [
    part(new THREE.CylinderGeometry(0.012, 0.018, 0.32, 6), stemColor, { translate: [0, 0.16, 0] }),
    part(new THREE.IcosahedronGeometry(0.07, 0), colorA, { translate: [0, 0.36, 0] }),
    part(new THREE.CylinderGeometry(0.012, 0.018, 0.28, 6), stemColor, { translate: [0.10, 0.14, 0.05] }),
    part(new THREE.IcosahedronGeometry(0.06, 0), colorB, { translate: [0.10, 0.30, 0.05] }),
    part(new THREE.CylinderGeometry(0.012, 0.018, 0.30, 6), stemColor, { translate: [-0.08, 0.15, -0.06] }),
    part(new THREE.IcosahedronGeometry(0.06, 0), colorC, { translate: [-0.08, 0.32, -0.06] })
  ];
  return mergeGeometries(parts, false);
}

const LOG_BROWN = new THREE.Color(0x4d3722);
const LOG_LIGHT = new THREE.Color(0x8e6d4a);

export function createTreeStumpGeometry() {
  const parts = [
    part(new THREE.CylinderGeometry(0.42, 0.55, 0.4, 12), LOG_BROWN, { translate: [0, 0.2, 0] }),
    part(new THREE.CylinderGeometry(0.40, 0.42, 0.04, 12), LOG_LIGHT, { translate: [0, 0.42, 0] })
  ];
  return mergeGeometries(parts, false);
}

export function createFallenLogGeometry() {
  const trunk = new THREE.CylinderGeometry(0.22, 0.26, 2.2, 10);
  trunk.rotateZ(Math.PI / 2);
  const cap = new THREE.CylinderGeometry(0.20, 0.22, 0.05, 10);
  cap.rotateZ(Math.PI / 2);
  cap.translate(1.1, 0, 0);
  const cap2 = cap.clone();
  cap2.translate(-2.2, 0, 0);
  const parts = [
    part(trunk, LOG_BROWN, { translate: [0, 0.24, 0] }),
    part(cap, LOG_LIGHT, { translate: [0, 0.24, 0] }),
    part(cap2, LOG_LIGHT, { translate: [0, 0.24, 0] })
  ];
  return mergeGeometries(parts, false);
}
