import * as THREE from 'three';

const CLOUD_COUNT = 22;
const FIELD_RADIUS = 320;
const ALT_MIN = 78;
const ALT_MAX = 118;
const DRIFT_SPEED = 1.4;

function hash(n) {
  const x = Math.sin(n * 9301 + 49297) * 233280;
  return x - Math.floor(x);
}

export class Clouds {
  constructor(scene) {
    this.scene = scene;
    this.material = new THREE.MeshBasicMaterial({
      color: 0xe6dcc5,
      fog: false,
      transparent: true,
      opacity: 0.78,
      depthWrite: false
    });

    this.group = new THREE.Group();
    this.group.name = 'Clouds';
    scene.add(this.group);

    for (let i = 0; i < CLOUD_COUNT; i++) {
      const cluster = new THREE.Group();
      const puffCount = 4 + Math.floor(hash(i * 3.1) * 4);
      for (let j = 0; j < puffCount; j++) {
        const r = 5.5 + hash(i * 7 + j) * 5.5;
        const geometry = new THREE.IcosahedronGeometry(r, 0);
        const mesh = new THREE.Mesh(geometry, this.material);
        mesh.position.set(
          (hash(i * 11 + j * 1.7) - 0.5) * 22,
          (hash(i * 13 + j * 2.3) - 0.5) * 4,
          (hash(i * 17 + j * 3.1) - 0.5) * 14
        );
        mesh.scale.set(1, 0.4, 1);
        cluster.add(mesh);
      }

      const angle = (i / CLOUD_COUNT) * Math.PI * 2 + hash(i * 5.9) * 0.6;
      const radius = FIELD_RADIUS * (0.35 + hash(i * 4.3) * 0.65);
      cluster.position.set(
        Math.cos(angle) * radius,
        ALT_MIN + hash(i * 8.7) * (ALT_MAX - ALT_MIN),
        Math.sin(angle) * radius
      );
      cluster.userData.driftSpeed = DRIFT_SPEED * (0.7 + hash(i * 2.5) * 0.6);
      this.group.add(cluster);
    }
  }

  update(dt, playerPos) {
    this.group.position.x = playerPos.x;
    this.group.position.z = playerPos.z;
    for (const cluster of this.group.children) {
      cluster.position.x += dt * cluster.userData.driftSpeed;
      if (cluster.position.x > FIELD_RADIUS) cluster.position.x -= FIELD_RADIUS * 2;
    }
  }

  dispose() {
    this.scene.remove(this.group);
    for (const cluster of this.group.children) {
      for (const mesh of cluster.children) mesh.geometry.dispose();
    }
    this.material.dispose();
  }
}
