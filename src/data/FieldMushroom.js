import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Agaricus campestris — field mushroom, meadow mushroom.
// White cap with PINK gills that mature to chocolate brown.
// Edible — and the most dangerous edible because Destroying Angel grows nearby and looks similar.

const CAP_TOP = new THREE.Color(0xf6f0e2);
const CAP_RIM = new THREE.Color(0xebe2d0);
const GILL_YOUNG = new THREE.Color(0xd29688);  // pink — the key ID
const STEM_TOP = new THREE.Color(0xf2eada);
const STEM_BASE = new THREE.Color(0xe0d6c0);
const RING = new THREE.Color(0xece2c4);

const CAP_RADIUS = 0.056;
const CAP_HEIGHT = 0.030;
const STEM_HEIGHT = 0.075;
const STEM_TOP_R = 0.013;
const STEM_BASE_R = 0.013;
const RING_FRAC = 0.66;
const RING_R = 0.018;
const RING_THICK = 0.0035;
const RADIAL_SEGMENTS = 26;

const CAP_PROFILE = [
  [0.00, 1.00],
  [0.20, 0.96],
  [0.42, 0.88],
  [0.62, 0.72],
  [0.80, 0.50],
  [0.92, 0.25],
  [0.98, 0.08],
  [1.00, 0.00],
  [0.96, -0.04],
  [0.78, -0.06],
  [0.50, -0.07],
  [0.28, -0.06],
  [0.18, -0.05]
];

export function buildFieldMushroom(rand = Math.random) {
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

  const parts = [capGeom, stemGeom, ringGeom].map(g => g.toNonIndexed ? g.toNonIndexed() : g);
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
    if (isUnderside) tmp.copy(GILL_YOUNG);
    else {
      const t = THREE.MathUtils.clamp(r / CAP_RADIUS, 0, 1);
      tmp.copy(CAP_TOP).lerp(CAP_RIM, t * 0.5);
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

export const FIELD_MUSHROOM_INFO = {
  id: 'fieldMushroom',
  commonName: 'Field Mushroom',
  latinName: 'Agaricus campestris',
  edibility: 'edible',
  edibilityLabel: 'Edible — choice',
  photoUrl: '/mushrooms/field-guide/field_mushroom.png',
  photoCredit: 'Wikimedia Commons',
  description: 'The classic meadow mushroom — close cousin to the cultivated button mushroom. Excellent eating, but the most dangerous edible to forage because deadly white amanitas share its habitat.',
  idFeatures: [
    'White or pale brown cap, smooth or finely scaly, hemispherical to flat',
    'PINK gills when young, maturing to chocolate brown — NEVER WHITE',
    'Short white stem, often slightly curved, with a thin ring',
    'NO volva at the base — clean stem',
    'Dark brown spore print'
  ],
  habitat: 'Open grassland and pasture, often in fairy rings. Loves areas grazed by horses or cattle.',
  season: 'Late summer through autumn',
  lookalikes: 'Destroying Angel (Amanita virosa) and Death Cap (A. phalloides) — DEADLY, both have WHITE gills and a volva. The pink-to-brown gill rule is the single most reliable safety check. If gills are white, walk away.',
  notes: 'When in doubt, look at the gills and look at the base. Pink-to-brown gills + no volva = Field Mushroom. White gills or any volva = stop.'
};
