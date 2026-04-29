import * as THREE from 'three';

const RING_RADIUS = 400;
const HILL_SEGMENTS = 96;
const TREE_COUNT = 160;

function hash(n) {
  const x = Math.sin(n * 127.1 + 311.7) * 43758.5453;
  return x - Math.floor(x);
}

function makeHillRibbon(radius, baseY, minHeight, maxHeight, seed) {
  const vertices = [];
  const indices = [];

  for (let i = 0; i <= HILL_SEGMENTS; i++) {
    const t = i / HILL_SEGMENTS;
    const angle = t * Math.PI * 2;
    const soft = Math.sin(t * Math.PI * 2 * 3 + seed) * 0.5 + 0.5;
    const jagged = hash(i * 3.7 + seed) * 0.35 + hash(i * 0.47 + seed) * 0.65;
    const height = minHeight + (soft * 0.35 + jagged * 0.65) * (maxHeight - minHeight);
    const x = Math.cos(angle) * radius;
    const z = Math.sin(angle) * radius;

    vertices.push(x, baseY, z);
    vertices.push(x, baseY + height, z);

    if (i < HILL_SEGMENTS) {
      const a = i * 2;
      indices.push(a, a + 1, a + 2, a + 1, a + 3, a + 2);
    }
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  return geometry;
}

export class DistantHorizon {
  constructor(scene) {
    this.scene = scene;
    this.group = new THREE.Group();
    this.group.name = 'DistantHorizon';
    this.group.renderOrder = -10;
    scene.add(this.group);

    this.backHills = new THREE.Mesh(
      makeHillRibbon(RING_RADIUS * 1.18, -28, 32, 86, 14.2),
      new THREE.MeshBasicMaterial({
        color: 0x4f6963,
        side: THREE.DoubleSide,
        transparent: true,
        opacity: 0.34,
        depthWrite: false,
        fog: false
      })
    );

    this.frontHills = new THREE.Mesh(
      makeHillRibbon(RING_RADIUS, -26, 22, 56, 6.4),
      new THREE.MeshBasicMaterial({
        color: 0x2d4338,
        side: THREE.DoubleSide,
        transparent: true,
        opacity: 0.42,
        depthWrite: false,
        fog: false
      })
    );

    this.treeMaterial = new THREE.MeshBasicMaterial({
      color: 0x1d2f28,
      transparent: true,
      opacity: 0.46,
      depthWrite: false,
      fog: false
    });

    const treeGeometry = new THREE.ConeGeometry(4.2, 22, 5, 1);
    this.trees = new THREE.InstancedMesh(treeGeometry, this.treeMaterial, TREE_COUNT);
    const dummy = new THREE.Object3D();
    for (let i = 0; i < TREE_COUNT; i++) {
      const angle = (i / TREE_COUNT) * Math.PI * 2 + (hash(i * 8.1) - 0.5) * 0.08;
      const radius = RING_RADIUS * (0.74 + hash(i * 2.9) * 0.16);
      const height = 0.6 + hash(i * 4.5) * 0.85;
      dummy.position.set(Math.cos(angle) * radius, -15 + height * 10, Math.sin(angle) * radius);
      dummy.rotation.set(0, -angle, 0);
      dummy.scale.set(0.65 + hash(i * 1.7) * 0.55, height, 0.65 + hash(i * 2.2) * 0.55);
      dummy.updateMatrix();
      this.trees.setMatrixAt(i, dummy.matrix);
    }
    this.trees.instanceMatrix.needsUpdate = true;

    this.group.add(this.backHills, this.frontHills, this.trees);
  }

  update(playerPos) {
    this.group.position.x = playerPos.x;
    this.group.position.y = playerPos.y - 8;
    this.group.position.z = playerPos.z;
  }

  dispose() {
    this.scene.remove(this.group);
    for (const child of this.group.children) {
      child.geometry?.dispose?.();
      child.material?.dispose?.();
    }
  }
}
