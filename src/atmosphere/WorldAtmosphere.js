import * as THREE from 'three';

const FOG_PATCHES = 28;
const MOTE_COUNT = 160;
const FIELD_RADIUS = 80;

function hash(n) {
  const x = Math.sin(n * 41.17 + 19.93) * 10000;
  return x - Math.floor(x);
}

function makeSoftCircleTexture() {
  const size = 128;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');
  const g = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
  g.addColorStop(0.0, 'rgba(232, 224, 194, 0.34)');
  g.addColorStop(0.45, 'rgba(180, 198, 162, 0.15)');
  g.addColorStop(1.0, 'rgba(180, 198, 162, 0.0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, size, size);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

function makeMoteTexture() {
  const size = 64;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');
  const g = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
  g.addColorStop(0.0, 'rgba(255, 255, 255, 0.95)');
  g.addColorStop(0.35, 'rgba(255, 255, 255, 0.32)');
  g.addColorStop(1.0, 'rgba(255, 255, 255, 0.0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, size, size);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

export class WorldAtmosphere {
  constructor(scene) {
    this.scene = scene;
    this.group = new THREE.Group();
    this.group.name = 'WorldAtmosphere';
    scene.add(this.group);

    this.fogMaterial = new THREE.MeshBasicMaterial({
      map: makeSoftCircleTexture(),
      color: 0xd9d6ba,
      transparent: true,
      opacity: 0.22,
      depthWrite: false,
      fog: false,
      side: THREE.DoubleSide
    });

    const fogGeometry = new THREE.PlaneGeometry(1, 1);
    this.fogPatches = [];
    for (let i = 0; i < FOG_PATCHES; i++) {
      const patch = new THREE.Mesh(fogGeometry, this.fogMaterial);
      const angle = hash(i * 5.1) * Math.PI * 2;
      const radius = FIELD_RADIUS * (0.18 + hash(i * 7.3) * 0.82);
      patch.position.set(Math.cos(angle) * radius, -1.0 + hash(i * 2.8) * 1.4, Math.sin(angle) * radius);
      patch.rotation.x = -Math.PI / 2;
      patch.rotation.z = hash(i * 9.5) * Math.PI;
      const scale = 14 + hash(i * 3.4) * 28;
      patch.scale.set(scale * (1.0 + hash(i * 4.2) * 0.7), scale, 1);
      patch.userData.baseX = patch.position.x;
      patch.userData.baseZ = patch.position.z;
      patch.userData.speed = 0.18 + hash(i * 6.6) * 0.24;
      this.group.add(patch);
      this.fogPatches.push(patch);
    }

    const positions = new Float32Array(MOTE_COUNT * 3);
    for (let i = 0; i < MOTE_COUNT; i++) {
      const angle = hash(i * 11.1) * Math.PI * 2;
      const radius = FIELD_RADIUS * hash(i * 13.5);
      positions[i * 3] = Math.cos(angle) * radius;
      positions[i * 3 + 1] = 1.5 + hash(i * 3.9) * 16;
      positions[i * 3 + 2] = Math.sin(angle) * radius;
    }
    const moteGeometry = new THREE.BufferGeometry();
    moteGeometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    this.moteMaterial = new THREE.PointsMaterial({
      map: makeMoteTexture(),
      color: 0xf5d88a,
      size: 1.7,
      sizeAttenuation: true,
      transparent: true,
      opacity: 0.28,
      alphaTest: 0.01,
      depthWrite: false,
      fog: false
    });
    this.motes = new THREE.Points(moteGeometry, this.moteMaterial);
    this.group.add(this.motes);
  }

  update(dt, playerPos, time) {
    this.group.position.set(playerPos.x, playerPos.y - 1.2, playerPos.z);

    for (let i = 0; i < this.fogPatches.length; i++) {
      const patch = this.fogPatches[i];
      const drift = time + i;
      patch.position.x = patch.userData.baseX + Math.sin(drift * patch.userData.speed) * 3.2;
      patch.position.z = patch.userData.baseZ + Math.cos(drift * patch.userData.speed * 0.8) * 2.4;
      patch.rotation.z += dt * 0.012 * (i % 2 === 0 ? 1 : -1);
    }

    this.motes.rotation.y += dt * 0.018;
    this.motes.position.y = Math.sin(time) * 0.45;
  }

  dispose() {
    this.scene.remove(this.group);
    this.fogMaterial.map?.dispose?.();
    this.moteMaterial.map?.dispose?.();
    this.fogMaterial.dispose();
    this.moteMaterial.dispose();
    for (const child of this.group.children) child.geometry?.dispose?.();
  }
}
