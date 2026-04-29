import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

const TARGET_HEIGHT = 8.5;
const CLUSTER_RADIUS = 4.0;
const MIN_TREE_HEIGHT = 0.8;
const MAX_HORIZ_RATIO = 2.5;

export async function loadPBRTreeTemplates(url = '/nature/Trees/mountain_trees/scene.gltf') {
  const loader = new GLTFLoader();
  const gltf = await loader.loadAsync(url);
  gltf.scene.updateMatrixWorld(true);
  return extractTemplates(gltf.scene);
}

function extractTemplates(scene) {
  const items = [];
  scene.traverse((obj) => {
    if (!obj.isMesh || !obj.geometry) return;
    const geometry = obj.geometry.clone();
    geometry.applyMatrix4(obj.matrixWorld);
    geometry.morphAttributes = {};
    delete geometry.attributes.skinIndex;
    delete geometry.attributes.skinWeight;
    const box = new THREE.Box3().setFromBufferAttribute(geometry.attributes.position);
    const cx = (box.min.x + box.max.x) / 2;
    const cz = (box.min.z + box.max.z) / 2;
    const material = Array.isArray(obj.material) ? obj.material[0] : obj.material;
    items.push({ geometry, material, cx, cz });
  });

  const clusters = [];
  for (const item of items) {
    let target = null;
    let bestDist = CLUSTER_RADIUS;
    for (const c of clusters) {
      const d = Math.hypot(c.cx - item.cx, c.cz - item.cz);
      if (d < bestDist) { bestDist = d; target = c; }
    }
    if (target) {
      target.items.push(item);
      target.cx = (target.cx * (target.items.length - 1) + item.cx) / target.items.length;
      target.cz = (target.cz * (target.items.length - 1) + item.cz) / target.items.length;
    } else {
      clusters.push({ cx: item.cx, cz: item.cz, items: [item] });
    }
  }

  const templates = [];
  for (const cluster of clusters) {
    const template = buildTemplate(cluster.items);
    if (template) templates.push(template);
  }
  return templates;
}

function buildTemplate(items) {
  const byMaterial = new Map();
  for (const item of items) {
    const key = item.material.uuid;
    if (!byMaterial.has(key)) byMaterial.set(key, { material: item.material, geometries: [] });
    byMaterial.get(key).geometries.push(item.geometry);
  }

  const parts = [];
  for (const { material, geometries } of byMaterial.values()) {
    let merged = null;
    try {
      merged = geometries.length === 1 ? geometries[0] : mergeGeometries(geometries, false);
    } catch {
      for (const geometry of geometries) parts.push({ geometry, material: tuneMaterial(material) });
      continue;
    }
    if (merged) parts.push({ geometry: merged, material: tuneMaterial(material) });
  }
  if (parts.length === 0) return null;

  const fullBox = new THREE.Box3();
  fullBox.makeEmpty();
  const tmpBox = new THREE.Box3();
  for (const part of parts) {
    tmpBox.setFromBufferAttribute(part.geometry.attributes.position);
    if (!tmpBox.isEmpty()) fullBox.union(tmpBox);
  }
  const size = new THREE.Vector3();
  fullBox.getSize(size);
  if (size.y < MIN_TREE_HEIGHT) return null;
  const horiz = Math.max(size.x, size.z);
  if (horiz > size.y * MAX_HORIZ_RATIO) return null;

  const cx = (fullBox.min.x + fullBox.max.x) / 2;
  const cz = (fullBox.min.z + fullBox.max.z) / 2;
  const minY = fullBox.min.y;
  const scale = TARGET_HEIGHT / size.y;
  for (const part of parts) {
    part.geometry.translate(-cx, -minY, -cz);
    part.geometry.scale(scale, scale, scale);
    part.geometry.computeBoundingSphere();
    part.geometry.computeVertexNormals();
  }

  return { parts };
}

function tuneMaterial(srcMaterial) {
  const name = (srcMaterial.name || '').toLowerCase();
  const isFoliage = name.includes('foliage') || name.includes('leaf') || name.includes('branch');
  const material = new THREE.MeshStandardMaterial({
    name: srcMaterial.name || 'tree',
    map: srcMaterial.map || null,
    normalMap: srcMaterial.normalMap || null,
    color: srcMaterial.color ? srcMaterial.color.clone() : new THREE.Color(0xffffff),
    roughness: srcMaterial.roughness ?? 0.85,
    metalness: 0,
    envMapIntensity: 1.0
  });
  if (material.map) material.map.colorSpace = THREE.SRGBColorSpace;
  if (isFoliage) {
    material.alphaTest = 0.5;
    material.transparent = false;
    material.side = THREE.DoubleSide;
  }
  return material;
}
