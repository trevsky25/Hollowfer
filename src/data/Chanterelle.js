import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Cantharellus cibarius — golden chanterelle.
// Visual cues: shallow funnel cap with wavy upturned edges, decurrent false gills,
// vivid golden-orange color, paler stem.

const CAP_TOP = new THREE.Color(0xe89a30);
const CAP_RIM = new THREE.Color(0xf3b850);
const CAP_UNDER = new THREE.Color(0xf0ce80);
const STEM_TOP = new THREE.Color(0xe7c478);
const STEM_BASE = new THREE.Color(0xd0a958);

const CAP_RADIUS = 0.070;
const CAP_HEIGHT = 0.040;
const STEM_HEIGHT = 0.105;
const STEM_TOP_R = 0.016;
const STEM_BASE_R = 0.011;
const RADIAL_SEGMENTS = 32;

// Profile control points (rFactor, yFactor). rFactor scales by CAP_RADIUS, yFactor by CAP_HEIGHT.
// Trace: center top → outward and gently up → wavy rim → curl over → concave underside → stem join.
// Tuned for a shallow dome with raised wavy edges (not a deep bowl).
const CAP_PROFILE = [
  [0.00, 0.92],
  [0.12, 0.88],
  [0.28, 0.84],
  [0.46, 0.86],
  [0.64, 0.93],
  [0.80, 1.02],
  [0.92, 1.10],
  [0.98, 1.14],
  [0.99, 1.10],
  [0.96, 0.96],
  [0.88, 0.78],
  [0.74, 0.58],
  [0.55, 0.38],
  [0.38, 0.22],
  [0.25, 0.10],
  [0.18, 0.02]
];

export function buildChanterelle(rand = Math.random) {
  const profile = CAP_PROFILE.map(([rf, yf]) => new THREE.Vector2(rf * CAP_RADIUS, yf * CAP_HEIGHT));
  const capGeom = new THREE.LatheGeometry(profile, RADIAL_SEGMENTS);
  capGeom.translate(0, STEM_HEIGHT, 0);

  _waveCapEdge(capGeom, rand);
  _colorCap(capGeom);

  const stemGeom = new THREE.CylinderGeometry(STEM_TOP_R, STEM_BASE_R, STEM_HEIGHT, 16, 1);
  stemGeom.translate(0, STEM_HEIGHT / 2, 0);
  _colorStem(stemGeom);

  const capNI = capGeom.toNonIndexed ? capGeom.toNonIndexed() : capGeom;
  const stemNI = stemGeom.toNonIndexed ? stemGeom.toNonIndexed() : stemGeom;

  let merged;
  try {
    merged = mergeGeometries([capNI, stemNI], false);
  } catch {
    merged = capNI;
  }
  merged.computeVertexNormals();
  return merged;
}

function _waveCapEdge(geom, rand) {
  const pos = geom.attributes.position;
  const yRimMin = STEM_HEIGHT + CAP_HEIGHT * 0.95;
  const yRimMax = STEM_HEIGHT + CAP_HEIGHT * 1.20;
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i);
    const y = pos.getY(i);
    const z = pos.getZ(i);
    if (y < yRimMin || y > yRimMax) continue;
    const angle = Math.atan2(z, x);
    // Larger wave amplitude than before; reads at walking distance.
    const wave = Math.sin(angle * 4) * 0.009 + Math.cos(angle * 7) * 0.004 + (rand() - 0.5) * 0.005;
    pos.setY(i, y + wave);
  }
  pos.needsUpdate = true;
}

function _colorCap(geom) {
  const pos = geom.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  const tmp = new THREE.Color();
  const rimLevel = STEM_HEIGHT + CAP_HEIGHT * 0.90;
  const yMin = STEM_HEIGHT;
  const yMax = STEM_HEIGHT + CAP_HEIGHT * 1.20;
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i);
    const y = pos.getY(i);
    const z = pos.getZ(i);
    const r = Math.hypot(x, z);
    const isUnderside = y < rimLevel && r < CAP_RADIUS * 0.95;
    if (isUnderside) {
      const tu = THREE.MathUtils.clamp((CAP_RADIUS - r) / CAP_RADIUS, 0, 1);
      tmp.copy(CAP_UNDER).lerp(STEM_TOP, tu * 0.4);
    } else {
      const t = THREE.MathUtils.clamp((y - yMin) / (yMax - yMin), 0, 1);
      tmp.copy(CAP_TOP).lerp(CAP_RIM, Math.pow(t, 1.4));
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

export const CHANTERELLE_INFO = {
  id: 'chanterelle',
  commonName: 'Chanterelle',
  latinName: 'Cantharellus cibarius',
  edibility: 'edible',
  edibilityLabel: 'Edible — choice',
  photoUrl: '/mushrooms/field-guide/chanterelle.png',
  photoCredit: 'Wikimedia Commons',
  description: 'Prized golden mushroom of deciduous forests. Distinguished by its funnel-shaped cap, decurrent false gills (forking ridges, not true gills), and pleasant apricot-like aroma when fresh.',
  idFeatures: [
    'Funnel-shaped golden-orange cap with wavy edges',
    'False gills: forked ridges running down the stem, not blade-like gills',
    'Solid stem, slightly paler than cap, tapering toward the base',
    'Pleasant fruity aroma, often compared to apricot'
  ],
  habitat: 'Mycorrhizal with broadleaf trees — especially oak and beech',
  season: 'Mid-summer through autumn',
  lookalikes: 'Jack-O\'-Lantern (Omphalotus olearius) — TOXIC, has true blade-like gills, often grows in dense clumps on wood. False Chanterelle (Hygrophoropsis aurantiaca) — edible-poor, also has true forking gills and a softer texture.',
  notes: 'The presence of false gills (ridges, not blades) is the single most reliable identifier. When in doubt: cut the stem. Chanterelle flesh is pale yellow throughout; lookalikes often differ in flesh color.'
};
