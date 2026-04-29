import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Psilocybe semilanceata — liberty cap. Small, conical, pointed nipple at the top.
// Psychoactive (psilocybin). Bruises blue/blue-green when handled.

const CAP_TOP = new THREE.Color(0x8a6c40);
const CAP_DARK = new THREE.Color(0x5e4828);
const GILL = new THREE.Color(0x4a4030);
const STEM = new THREE.Color(0xc8b08c);
const STEM_BASE = new THREE.Color(0x9e8268);

const CAP_RADIUS = 0.020;
const CAP_HEIGHT = 0.034;
const STEM_HEIGHT = 0.075;
const STEM_TOP_R = 0.005;
const STEM_BASE_R = 0.004;
const RADIAL_SEGMENTS = 20;

// Bell-shaped/conical with a sharp pointed umbo at the very top.
const CAP_PROFILE = [
  [0.00, 1.20],   // sharp pointed umbo
  [0.10, 1.05],
  [0.25, 0.92],
  [0.45, 0.76],
  [0.65, 0.55],
  [0.82, 0.32],
  [0.94, 0.12],
  [1.00, 0.00],
  [0.96, -0.03],
  [0.70, -0.04],
  [0.42, -0.05],
  [0.20, -0.05],
  [0.12, -0.04]
];

export function buildLibertyCap(rand = Math.random) {
  const profile = CAP_PROFILE.map(([rf, yf]) => new THREE.Vector2(rf * CAP_RADIUS, yf * CAP_HEIGHT));
  const capGeom = new THREE.LatheGeometry(profile, RADIAL_SEGMENTS);
  capGeom.translate(0, STEM_HEIGHT, 0);
  _colorCap(capGeom);

  const stemGeom = new THREE.CylinderGeometry(STEM_TOP_R, STEM_BASE_R, STEM_HEIGHT, 10, 1);
  stemGeom.translate(0, STEM_HEIGHT / 2, 0);
  // Slight bend in the stem (typical liberty cap)
  _colorStem(stemGeom);

  const parts = [capGeom, stemGeom].map(g => g.toNonIndexed ? g.toNonIndexed() : g);
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
    if (isUnderside) {
      tmp.copy(GILL);
    } else {
      const t = THREE.MathUtils.clamp(r / CAP_RADIUS, 0, 1);
      tmp.copy(CAP_DARK).lerp(CAP_TOP, 1 - Math.pow(t, 1.6) * 0.5);
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

export const LIBERTY_CAP_INFO = {
  id: 'libertyCap',
  commonName: 'Liberty Cap',
  latinName: 'Psilocybe semilanceata',
  edibility: 'magic',
  edibilityLabel: 'Psychoactive',
  photoUrl: '/mushrooms/field-guide/liberty_cap.png',
  photoCredit: 'Wikimedia Commons',
  description: 'A small, slender mushroom famous for its potent psilocybin content. Easy to overlook — barely larger than a fingernail and the same color as the dying grass it grows in.',
  idFeatures: [
    'Distinctive bell or conical cap with a sharp pointed nipple (umbo) at the apex',
    'Cap small (5-25mm), tan to chestnut when moist, paler when dry',
    'Dark brown to purple-black gills',
    'Long slender pale stem, often slightly wavy',
    'Bruises blue or blue-green where handled or damaged — diagnostic for psilocybin'
  ],
  habitat: 'Grassy meadows and pastures, especially those grazed by sheep and cattle. Never grows directly on dung.',
  season: 'Autumn — typically September through November in temperate regions',
  lookalikes: 'Galerina species — DEADLY, contain the same liver toxins as Death Cap. Do not bruise blue. Conocybe and Pholiotina species — also potentially deadly. The blue-bruise reaction is the safest single indicator, but should be confirmed with multiple features.',
  notes: 'Possession is illegal in many jurisdictions. Effects last 4-6 hours and are dose-dependent. Easy to confuse with deadly Galerina species when not bruised — never collect by appearance alone.'
};
