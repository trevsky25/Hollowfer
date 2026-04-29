import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Amanita muscaria — fly agaric.
// Iconic red cap with white universal-veil warts, white gills, ring on stem,
// small bulb at base. Mildly psychoactive and toxic.

const CAP_RED = new THREE.Color(0xc92a1a);
const CAP_RED_DARK = new THREE.Color(0x8e1010);
const WART = new THREE.Color(0xf2eadb);
const GILL = new THREE.Color(0xf2ead8);
const STEM = new THREE.Color(0xf3e9d4);
const STEM_BASE = new THREE.Color(0xe2d8b8);
const RING = new THREE.Color(0xece2c4);

const CAP_RADIUS = 0.065;
const CAP_HEIGHT = 0.042;
const STEM_HEIGHT = 0.115;
const STEM_TOP_R = 0.013;
const STEM_BASE_R = 0.018;
const BULB_R = 0.022;
const BULB_HEIGHT = 0.014;
const RING_FRAC = 0.70;
const RING_R = 0.020;
const RING_THICK = 0.0035;
const RADIAL_SEGMENTS = 30;
const WART_COUNT = 9;

const CAP_PROFILE = [
  [0.00, 1.00],
  [0.20, 0.96],
  [0.40, 0.88],
  [0.58, 0.74],
  [0.74, 0.55],
  [0.88, 0.30],
  [0.96, 0.10],
  [1.00, 0.00],
  [0.96, -0.04],
  [0.78, -0.05],
  [0.50, -0.06],
  [0.26, -0.06],
  [0.18, -0.05]
];

export function buildFlyAgaric(rand = Math.random) {
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

  const bulbGeom = new THREE.SphereGeometry(BULB_R, 14, 10);
  bulbGeom.scale(1, BULB_HEIGHT / BULB_R, 1);
  bulbGeom.translate(0, BULB_HEIGHT * 0.5, 0);
  _colorSolid(bulbGeom, STEM_BASE);

  const parts = [capGeom, stemGeom, ringGeom, bulbGeom];

  // Scatter wart bumps on the cap top
  for (let i = 0; i < WART_COUNT; i++) {
    const angle = rand() * Math.PI * 2;
    const radial = (0.15 + rand() * 0.65) * CAP_RADIUS;
    const wartR = 0.005 + rand() * 0.005;
    const wartGeom = new THREE.SphereGeometry(wartR, 7, 5);
    // Position on the cap surface (approximate by sampling profile)
    const tApprox = radial / CAP_RADIUS;
    const profileIdx = Math.min(CAP_PROFILE.length - 1, Math.floor(tApprox * 7));
    const yOnCap = STEM_HEIGHT + CAP_PROFILE[profileIdx][1] * CAP_HEIGHT;
    wartGeom.translate(Math.cos(angle) * radial, yOnCap + wartR * 0.5, Math.sin(angle) * radial);
    _colorSolid(wartGeom, WART);
    parts.push(wartGeom);
  }

  const ni = parts.map(g => g.toNonIndexed ? g.toNonIndexed() : g);
  let merged;
  try { merged = mergeGeometries(ni, false); } catch { merged = ni[0]; }
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
    if (isUnderside) {
      tmp.copy(GILL);
    } else {
      const t = THREE.MathUtils.clamp(r / CAP_RADIUS, 0, 1);
      tmp.copy(CAP_RED_DARK).lerp(CAP_RED, 1 - Math.pow(t, 1.4) * 0.4);
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
    tmp.copy(STEM_BASE).lerp(STEM, t);
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

export const FLY_AGARIC_INFO = {
  id: 'flyAgaric',
  commonName: 'Fly Agaric',
  latinName: 'Amanita muscaria',
  edibility: 'magic',
  edibilityLabel: 'Psychoactive / Toxic',
  photoUrl: '/mushrooms/field-guide/fly_agaric.png',
  photoCredit: 'Wikimedia Commons',
  description: 'The iconic toadstool of fairy tales. Brilliant red cap dotted with white wart-like flecks (remnants of the universal veil). Contains muscimol and ibotenic acid — psychoactive in low doses, toxic in higher amounts; deaths are rare but recorded.',
  idFeatures: [
    'Bright scarlet to red-orange cap, often fading toward the rim',
    'White or pale-yellow wart-like patches scattered across the cap',
    'White free gills, not attached to the stem',
    'White ring on the upper stem',
    'Small bulb at the base, sometimes with concentric scaly bands',
    'Often grows in fairy rings under birch and pine'
  ],
  habitat: 'Mycorrhizal with birch, pine, and spruce — common in coniferous and mixed forest',
  season: 'Late summer through autumn',
  lookalikes: 'Caesar\'s Mushroom (Amanita caesarea) — edible, has yellow gills/stem and an orange cap with no warts. Patches can wash off A. muscaria after rain, leaving an unmarked red cap that is harder to identify.',
  notes: 'Ethnobotanical use is ancient — Siberian shamans, Norse berserkers, and Mario games all owe something to this mushroom. Effects are unpredictable, can include nausea, delirium, and ataxia. Not recommended for casual experimentation.'
};
