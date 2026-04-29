import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Galerina marginata — deadly galerina.
// Small brown LBM (little brown mushroom). Contains the same amatoxins as Death Cap.
// Often grows where psilocybes grow, leading to fatal mistaken identification.

const CAP_TOP = new THREE.Color(0x8c5c2a);
const CAP_DARK = new THREE.Color(0x5e3818);
const GILL = new THREE.Color(0x8a6a4a);
const STEM_TOP = new THREE.Color(0xa68868);
const STEM_BASE = new THREE.Color(0x82684a);
const RING = new THREE.Color(0x7e5a3a);

const CAP_RADIUS = 0.024;
const CAP_HEIGHT = 0.020;
const STEM_HEIGHT = 0.060;
const STEM_TOP_R = 0.005;
const STEM_BASE_R = 0.006;
const RING_FRAC = 0.62;
const RING_R = 0.008;
const RING_THICK = 0.0018;
const RADIAL_SEGMENTS = 22;

// Convex / bell-shaped, with slight umbo. Smaller and not pointed like Liberty Cap.
const CAP_PROFILE = [
  [0.00, 1.00],
  [0.16, 0.96],
  [0.36, 0.88],
  [0.55, 0.74],
  [0.74, 0.55],
  [0.88, 0.30],
  [0.96, 0.10],
  [1.00, 0.00],
  [0.96, -0.04],
  [0.72, -0.06],
  [0.40, -0.06],
  [0.20, -0.05],
  [0.14, -0.04]
];

export function buildDeadlyGalerina(rand = Math.random) {
  const profile = CAP_PROFILE.map(([rf, yf]) => new THREE.Vector2(rf * CAP_RADIUS, yf * CAP_HEIGHT));
  const capGeom = new THREE.LatheGeometry(profile, RADIAL_SEGMENTS);
  capGeom.translate(0, STEM_HEIGHT, 0);
  _colorCap(capGeom);

  const stemGeom = new THREE.CylinderGeometry(STEM_TOP_R, STEM_BASE_R, STEM_HEIGHT, 12, 1);
  stemGeom.translate(0, STEM_HEIGHT / 2, 0);
  _colorStem(stemGeom);

  const ringGeom = new THREE.TorusGeometry(RING_R, RING_THICK, 6, 14);
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
    if (isUnderside) tmp.copy(GILL);
    else {
      const t = THREE.MathUtils.clamp(r / CAP_RADIUS, 0, 1);
      tmp.copy(CAP_DARK).lerp(CAP_TOP, 1 - Math.pow(t, 1.6) * 0.4);
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

export const DEADLY_GALERINA_INFO = {
  id: 'deadlyGalerina',
  commonName: 'Deadly Galerina',
  latinName: 'Galerina marginata',
  edibility: 'deadly',
  edibilityLabel: 'Deadly',
  photoUrl: '/mushrooms/field-guide/deadly_galerina.png',
  photoCredit: 'Wikimedia Commons',
  description: 'Small, plain, brown — a "little brown mushroom" that contains the same amatoxins as Death Cap. The most lethal of the LBMs. Often grows in the same grassy areas as psilocybin mushrooms, leading to fatal mistaken foraging.',
  idFeatures: [
    'Cap small (1.5-4cm), tan to brown, smooth, sticky when moist',
    'Cap convex when young, flatter at maturity, often with a faint umbo',
    'Tan-brown gills, becoming rusty brown with mature spores',
    'Pale brown stem with a small ring (annulus), sometimes worn off in older specimens',
    'Does NOT bruise blue (unlike Liberty Cap)',
    'Rusty-brown spore print (psilocybes have purple-black)'
  ],
  habitat: 'Grows in clumps on rotting wood — fallen logs, mulch, decaying conifer roots. Sometimes in grassy meadows with buried wood.',
  season: 'Autumn, peak in October-November',
  lookalikes: 'Liberty Cap (Psilocybe semilanceata) — magic, has a sharply pointed umbo and bruises blue. Galerina has a rounded cap and never bruises blue. Magic-mushroom hunters die from this every year. The blue-bruise test is critical.',
  notes: 'If you cannot positively rule out Galerina by spore print AND blue-bruise reaction, do not collect any small brown mushroom. Several deaths each year are attributed to this species being mistaken for psilocybes.'
};
