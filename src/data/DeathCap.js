import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Amanita phalloides — the death cap.
// Critical ID features: olive-green/yellow cap, free white gills, white ring on stem,
// and a BULBOUS VOLVA at the base (often hidden in litter). Responsible for ~50% of
// mushroom deaths worldwide.

const CAP_TOP = new THREE.Color(0x9eaa6e);
const CAP_RIM = new THREE.Color(0xc4c896);
const CAP_UNDER = new THREE.Color(0xf2ecd8);
const STEM_TOP = new THREE.Color(0xf2ecd8);
const STEM_BASE = new THREE.Color(0xe2dabe);
const VOLVA = new THREE.Color(0xd2c8a0);
const RING = new THREE.Color(0xece2c4);

const CAP_RADIUS = 0.062;
const CAP_HEIGHT = 0.038;
const STEM_HEIGHT = 0.130;
const STEM_TOP_R = 0.012;
const STEM_BASE_R = 0.016;
const VOLVA_R = 0.024;
const VOLVA_HEIGHT = 0.020;
const RING_HEIGHT_FRAC = 0.72;
const RING_OUTER_R = 0.020;
const RING_THICKNESS = 0.0035;
const RADIAL_SEGMENTS = 28;

// Convex hemispherical cap profile (no funnel — opposite of Chanterelle).
// Trace: center peak → curving down to flared edge → flat underside (where gills hang) → stem-join.
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
  [0.80, -0.06],
  [0.55, -0.07],
  [0.32, -0.07],
  [0.20, -0.06]
];

export function buildDeathCap(rand = Math.random) {
  const profile = CAP_PROFILE.map(([rf, yf]) => new THREE.Vector2(rf * CAP_RADIUS, yf * CAP_HEIGHT));
  const capGeom = new THREE.LatheGeometry(profile, RADIAL_SEGMENTS);
  capGeom.translate(0, STEM_HEIGHT, 0);
  _colorCap(capGeom);

  const stemGeom = new THREE.CylinderGeometry(STEM_TOP_R, STEM_BASE_R, STEM_HEIGHT, 16, 1);
  stemGeom.translate(0, STEM_HEIGHT / 2, 0);
  _colorStem(stemGeom);

  // Ring: flat torus around upper stem.
  const ringGeom = new THREE.TorusGeometry(RING_OUTER_R, RING_THICKNESS, 6, 16);
  ringGeom.rotateX(Math.PI / 2);
  ringGeom.translate(0, STEM_HEIGHT * RING_HEIGHT_FRAC, 0);
  _colorSolid(ringGeom, RING);

  // Volva: flattened sphere at base, partly above ground. The killer ID feature.
  const volvaGeom = new THREE.SphereGeometry(VOLVA_R, 16, 10);
  volvaGeom.scale(1, VOLVA_HEIGHT / VOLVA_R, 1);
  volvaGeom.translate(0, VOLVA_HEIGHT * 0.55, 0);
  _colorSolid(volvaGeom, VOLVA);

  const parts = [capGeom, stemGeom, ringGeom, volvaGeom].map(_toNonIndexed);
  let merged;
  try {
    merged = mergeGeometries(parts, false);
  } catch {
    merged = parts[0];
  }
  merged.computeVertexNormals();
  return merged;
}

function _toNonIndexed(g) {
  return g.toNonIndexed ? g.toNonIndexed() : g;
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
    if (isUnderside) {
      tmp.copy(CAP_UNDER);
    } else {
      const t = THREE.MathUtils.clamp(r / CAP_RADIUS, 0, 1);
      tmp.copy(CAP_TOP).lerp(CAP_RIM, Math.pow(t, 1.3) * 0.6);
    }
    colors[i * 3] = tmp.r;
    colors[i * 3 + 1] = tmp.g;
    colors[i * 3 + 2] = tmp.b;
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
    colors[i * 3] = tmp.r;
    colors[i * 3 + 1] = tmp.g;
    colors[i * 3 + 2] = tmp.b;
  }
  geom.setAttribute('color', new THREE.BufferAttribute(colors, 3));
}

function _colorSolid(geom, color) {
  const pos = geom.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  for (let i = 0; i < pos.count; i++) {
    colors[i * 3] = color.r;
    colors[i * 3 + 1] = color.g;
    colors[i * 3 + 2] = color.b;
  }
  geom.setAttribute('color', new THREE.BufferAttribute(colors, 3));
}

export const DEATH_CAP_INFO = {
  id: 'deathCap',
  commonName: 'Death Cap',
  latinName: 'Amanita phalloides',
  edibility: 'deadly',
  edibilityLabel: 'Deadly',
  photoUrl: '/mushrooms/field-guide/death_cap.png',
  photoCredit: 'Wikimedia Commons',
  description: 'The most lethal mushroom on Earth. Responsible for roughly half of all mushroom-poisoning deaths worldwide. Pleasant taste, delayed symptoms, and irreversible liver damage combine to make it uniquely dangerous.',
  idFeatures: [
    'Cap olive-green to pale yellow, smooth and slightly sticky when moist',
    'Free white gills — not attached to the stem',
    'White ring (annulus) on the upper stem',
    'BULBOUS VOLVA at the base — often hidden in litter; always brush down to check',
    'White spore print'
  ],
  habitat: 'Mycorrhizal with broadleaf trees — oak, beech, hornbeam',
  season: 'Late summer through autumn',
  lookalikes: 'Field Mushroom (Agaricus campestris) and Horse Mushroom (A. arvensis) — both edible, but have PINK or BROWN gills, never white, and no volva. Caesar\'s Mushroom (A. caesarea) — edible delicacy with orange cap and yellow gills/stem.',
  notes: 'There is no antidote. Symptoms appear 6–12 hours after ingestion: severe gastrointestinal distress, then a deceptive "well" period of 1–2 days, then liver and kidney failure. By the time you feel sick, the damage is already done. The volva at the base is the single most important identifier — always dig down through the leaf litter to check.'
};
