import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Amanita virosa — destroying angel.
// Pure white. Lethal. The dangerous mirror of Field Mushroom: similar size and habitat,
// but white gills (not pink) and a bulbous volva at the base.

const CAP_TOP = new THREE.Color(0xfaf5e8);
const CAP_RIM = new THREE.Color(0xf0e8d0);
const GILL = new THREE.Color(0xfff8e0);
const STEM_TOP = new THREE.Color(0xf8f0d8);
const STEM_BASE = new THREE.Color(0xeee2c0);
const VOLVA = new THREE.Color(0xe8dcb8);
const RING = new THREE.Color(0xece2c4);

const CAP_RADIUS = 0.058;
const CAP_HEIGHT = 0.034;
const STEM_HEIGHT = 0.140;
const STEM_TOP_R = 0.011;
const STEM_BASE_R = 0.014;
const VOLVA_R = 0.026;
const VOLVA_HEIGHT = 0.022;
const RING_FRAC = 0.74;
const RING_R = 0.018;
const RING_THICK = 0.0035;
const RADIAL_SEGMENTS = 28;

const CAP_PROFILE = [
  [0.00, 1.00],
  [0.18, 0.96],
  [0.36, 0.88],
  [0.55, 0.74],
  [0.72, 0.55],
  [0.86, 0.32],
  [0.95, 0.12],
  [1.00, 0.00],
  [0.96, -0.04],
  [0.78, -0.06],
  [0.55, -0.07],
  [0.32, -0.07],
  [0.20, -0.06]
];

export function buildDestroyingAngel(rand = Math.random) {
  const profile = CAP_PROFILE.map(([rf, yf]) => new THREE.Vector2(rf * CAP_RADIUS, yf * CAP_HEIGHT));
  const capGeom = new THREE.LatheGeometry(profile, RADIAL_SEGMENTS);
  capGeom.translate(0, STEM_HEIGHT, 0);
  _colorCap(capGeom);

  const stemGeom = new THREE.CylinderGeometry(STEM_TOP_R, STEM_BASE_R, STEM_HEIGHT, 14, 1);
  stemGeom.translate(0, STEM_HEIGHT / 2, 0);
  _colorStem(stemGeom);

  const ringGeom = new THREE.TorusGeometry(RING_R, RING_THICK, 6, 16);
  ringGeom.rotateX(Math.PI / 2);
  ringGeom.translate(0, STEM_HEIGHT * RING_FRAC, 0);
  _colorSolid(ringGeom, RING);

  const volvaGeom = new THREE.SphereGeometry(VOLVA_R, 16, 10);
  volvaGeom.scale(1, VOLVA_HEIGHT / VOLVA_R, 1);
  volvaGeom.translate(0, VOLVA_HEIGHT * 0.55, 0);
  _colorSolid(volvaGeom, VOLVA);

  const parts = [capGeom, stemGeom, ringGeom, volvaGeom].map(g => g.toNonIndexed ? g.toNonIndexed() : g);
  let merged;
  try { merged = mergeGeometries(parts, false); } catch { merged = parts[0]; }
  merged.computeVertexNormals();
  return merged;
}

function _colorCap(geom) {
  const pos = geom.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  const tmp = new THREE.Color();
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i);
    const y = pos.getY(i);
    const z = pos.getZ(i);
    const r = Math.hypot(x, z);
    const isUnderside = y < STEM_HEIGHT - CAP_HEIGHT * 0.02;
    if (isUnderside) tmp.copy(GILL);
    else {
      const t = THREE.MathUtils.clamp(r / CAP_RADIUS, 0, 1);
      tmp.copy(CAP_TOP).lerp(CAP_RIM, t * 0.4);
    }
    colors[i * 3] = tmp.r; colors[i * 3 + 1] = tmp.g; colors[i * 3 + 2] = tmp.b;
  }
  geom.setAttribute('color', new THREE.BufferAttribute(colors, 3));
}

function _colorStem(geom) {
  const pos = geom.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  const tmp = new THREE.Color();
  for (let i = 0; i < pos.count; i++) {
    const y = pos.getY(i);
    const t = THREE.MathUtils.clamp(y / STEM_HEIGHT, 0, 1);
    tmp.copy(STEM_BASE).lerp(STEM_TOP, t);
    colors[i * 3] = tmp.r; colors[i * 3 + 1] = tmp.g; colors[i * 3 + 2] = tmp.b;
  }
  geom.setAttribute('color', new THREE.BufferAttribute(colors, 3));
}

function _colorSolid(geom, color) {
  const pos = geom.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  for (let i = 0; i < pos.count; i++) {
    colors[i * 3] = color.r; colors[i * 3 + 1] = color.g; colors[i * 3 + 2] = color.b;
  }
  geom.setAttribute('color', new THREE.BufferAttribute(colors, 3));
}

export const DESTROYING_ANGEL_INFO = {
  id: 'destroyingAngel',
  commonName: 'Destroying Angel',
  latinName: 'Amanita virosa',
  edibility: 'deadly',
  edibilityLabel: 'Deadly',
  photoUrl: '/mushrooms/field-guide/destroying_angel.png',
  photoCredit: 'Wikimedia Commons',
  description: 'Pure white and elegant. Among the deadliest mushrooms in the world, containing the same liver-destroying amatoxins as the Death Cap. Easily mistaken for the edible Field Mushroom, especially when young.',
  idFeatures: [
    'Entirely pure white — cap, gills, stem, all white',
    'Cap convex when young, expanding to flat with maturity, often umbonate',
    'White FREE gills (never pink, never brown)',
    'White ring (annulus) on the upper stem',
    'BULBOUS VOLVA at the base — sac-like, often hidden in soil or moss',
    'White spore print'
  ],
  habitat: 'Mycorrhizal with broadleaf trees — birch, beech, oak. Often in mixed woodland edges.',
  season: 'Mid-summer through autumn',
  lookalikes: 'Field Mushroom (Agaricus campestris) — EDIBLE, but has PINK or BROWN gills (never white) and no volva. Horse Mushroom (A. arvensis) — edible, also has dark gills. The unchanging white gills of Destroying Angel are the universal warning.',
  notes: 'Together with Death Cap, accounts for nearly all fatal mushroom poisonings in temperate regions. The same delayed-onset pattern: 6-12 hours of nothing, then violent illness, then a fake recovery, then liver failure. Treat any all-white gilled mushroom with extreme suspicion.'
};
