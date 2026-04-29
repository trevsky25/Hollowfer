import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

// Boletus edulis — king bolete, porcini, cep.
// Distinctive among MVP species: NO GILLS — has spongy pore tubes underneath.
// Brown cap, thick reticulated white stem.

const CAP_TOP = new THREE.Color(0x7a4f2a);
const CAP_DARK = new THREE.Color(0x4f3018);
const PORE = new THREE.Color(0xeae0a8);
const STEM_TOP = new THREE.Color(0xefe4c8);
const STEM_BASE = new THREE.Color(0xddc99a);

const CAP_RADIUS = 0.080;
const CAP_HEIGHT = 0.045;
const STEM_HEIGHT = 0.090;
const STEM_TOP_R = 0.026;
const STEM_BASE_R = 0.034;
const RADIAL_SEGMENTS = 30;

// Convex dome cap (boletes are usually hemispherical to flat-mature)
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
  [0.40, -0.06],
  [0.32, -0.05]
];

export function buildPorcini(rand = Math.random) {
  const profile = CAP_PROFILE.map(([rf, yf]) => new THREE.Vector2(rf * CAP_RADIUS, yf * CAP_HEIGHT));
  const capGeom = new THREE.LatheGeometry(profile, RADIAL_SEGMENTS);
  capGeom.translate(0, STEM_HEIGHT, 0);
  _colorCap(capGeom);

  // Bulbous stem — wider at the base than the top.
  const stemGeom = new THREE.CylinderGeometry(STEM_TOP_R, STEM_BASE_R, STEM_HEIGHT, 18, 1);
  stemGeom.translate(0, STEM_HEIGHT / 2, 0);
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
      tmp.copy(PORE);
    } else {
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

export const PORCINI_INFO = {
  id: 'porcini',
  commonName: 'Porcini',
  latinName: 'Boletus edulis',
  edibility: 'edible',
  edibilityLabel: 'Edible — choice',
  photoUrl: '/mushrooms/field-guide/porcini.png',
  photoCredit: 'Wikimedia Commons',
  description: 'The king of edible mushrooms. Sought after worldwide for its dense, nutty flavor. Substantial flesh and lack of gills make it nearly unmistakable when known.',
  idFeatures: [
    'Brown to chestnut cap, smooth or slightly tacky, hemispherical when young',
    'NO GILLS — underside is a spongy layer of fine pores (whitish in young specimens, yellowing with age)',
    'Thick, bulbous white-to-cream stem',
    'Fine white netting (reticulation) on the upper stem, visible up close',
    'Cut flesh stays white — does not stain blue (unlike some toxic boletes)'
  ],
  habitat: 'Mycorrhizal with broadleaf and conifer trees — beech, oak, pine, spruce',
  season: 'Summer through autumn, especially after warm rain',
  lookalikes: 'Bitter Bolete (Tylopilus felleus) — bitter and inedible, has pink (not white) pores and a darker reticulated stem. Devil\'s Bolete (Rubroboletus satanas) — toxic, has red pores and a stem that stains blue when cut.',
  notes: 'A blue-staining bolete is a danger signal — many edible boletes do not stain at all, while several toxic species do. When in doubt: cut the stem and watch the flesh for 30 seconds before deciding.'
};
