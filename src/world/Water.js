import * as THREE from 'three';

// A small reflective stream/pond system that bends past the Mill — story
// alignment with "the River Wend used to power the mill but now bypasses it."
//
// Each water body is built as a TESSELLATED ORGANIC BLOB with concentric rings
// of vertices. The outer ring's alpha fades to 0 so the water blends smoothly
// into the terrain instead of showing a hard rectangular edge. Surface uses a
// reflective PBR material so the procedural sky bounces off it; UV-scrolled
// normal noise creates moving ripples.

const WATER_COLOR = new THREE.Color(0x355c6e);

export class Water {
  constructor(scene) {
    this.scene = scene;
    this.group = new THREE.Group();
    this.group.name = 'Water';
    scene.add(this.group);

    const ripples = makeRippleNormalTexture();
    ripples.wrapS = ripples.wrapT = THREE.RepeatWrapping;
    ripples.repeat.set(6, 6);
    this.ripples = ripples;

    this.material = new THREE.MeshStandardMaterial({
      color: WATER_COLOR,
      roughness: 0.18,
      metalness: 0.55,
      transparent: true,
      opacity: 0.9,
      side: THREE.DoubleSide,
      depthWrite: false,
      normalMap: ripples,
      normalScale: new THREE.Vector2(0.6, 0.6),
      vertexColors: true,
      envMapIntensity: 1.4
    });

    // ---- Pond by the Mill — wider basin where the wheel used to spin ----
    this._addBody({
      width: 16, depth: 9, raggedness: 0.18, position: [-50, 0.04, 18], rotY: -0.4
    });

    // ---- Stream channel running south ----
    this._addBody({
      width: 4, depth: 28, raggedness: 0.10, position: [-58, 0.03, 50], rotY: 0.15
    });

    // ---- Chapel puddle (atmosphere) ----
    this._addBody({
      width: 3.5, depth: 2.2, raggedness: 0.22, position: [8, 0.025, -38], rotY: 0.6
    });

    // ---- Wet patch near south road (mud) ----
    this._addBody({
      width: 2.5, depth: 1.6, raggedness: 0.30, position: [12, 0.02, 50], rotY: 1.2
    });
  }

  _addBody({ width, depth, raggedness, position, rotY }) {
    const geometry = makeOrganicWaterGeometry(width, depth, raggedness);
    const mesh = new THREE.Mesh(geometry, this.material);
    mesh.rotation.x = -Math.PI / 2;
    mesh.rotation.z = rotY;
    mesh.position.set(position[0], position[1], position[2]);
    mesh.receiveShadow = true;
    this.group.add(mesh);
  }

  update(dt) {
    this.ripples.offset.x += dt * 0.04;
    this.ripples.offset.y += dt * 0.025;
  }

  dispose() {
    this.scene.remove(this.group);
    this.material.dispose();
    this.ripples.dispose();
    for (const child of this.group.children) child.geometry.dispose();
  }
}

// Build a flat, organic-shaped water surface with soft alpha fall-off at the
// edge. Concentric rings of vertices; outer ring is fully transparent; inner
// rings opaque. Per-ring radius is jittered by a sin function so the shape
// reads as organic (not a perfect ellipse).
function makeOrganicWaterGeometry(width, depth, raggedness) {
  const segments = 36;
  const rings = 5;
  const positions = [];
  const colors = [];
  const indices = [];

  // center vertex
  positions.push(0, 0, 0);
  colors.push(1, 1, 1, 1);

  for (let r = 1; r <= rings; r++) {
    const ringT = r / rings;
    for (let i = 0; i < segments; i++) {
      const angle = (i / segments) * Math.PI * 2;
      const wobble = 1 + Math.sin(angle * 3 + r * 1.7) * raggedness * (1 - ringT * 0.3);
      const x = Math.cos(angle) * width * 0.5 * ringT * wobble;
      const z = Math.sin(angle) * depth * 0.5 * ringT * wobble;
      positions.push(x, 0, z);
      // Alpha tapers smoothly across rings; outermost ring fully transparent
      const alpha = ringT < 0.55 ? 1.0 : (1.0 - (ringT - 0.55) / 0.45);
      colors.push(1, 1, 1, Math.max(0, alpha));
    }
  }

  // Center → first ring (triangle fan)
  for (let i = 0; i < segments; i++) {
    indices.push(0, 1 + i, 1 + ((i + 1) % segments));
  }

  // Between successive rings
  for (let r = 1; r < rings; r++) {
    const innerStart = 1 + (r - 1) * segments;
    const outerStart = 1 + r * segments;
    for (let i = 0; i < segments; i++) {
      const a = innerStart + i;
      const b = innerStart + ((i + 1) % segments);
      const c = outerStart + i;
      const d = outerStart + ((i + 1) % segments);
      indices.push(a, c, b);
      indices.push(b, c, d);
    }
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 4));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  return geometry;
}

function makeRippleNormalTexture() {
  const size = 256;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');

  ctx.fillStyle = '#8080ff';
  ctx.fillRect(0, 0, size, size);

  const image = ctx.getImageData(0, 0, size, size);
  const data = image.data;
  for (let i = 0; i < data.length; i += 4) {
    const noise = (Math.random() - 0.5) * 60;
    data[i]     = Math.max(60, Math.min(220, data[i] + noise));
    data[i + 1] = Math.max(60, Math.min(220, data[i + 1] + noise * 0.8));
  }
  ctx.putImageData(image, 0, 0);

  for (let i = 0; i < 80; i++) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const len = 18 + Math.random() * 28;
    const angle = Math.random() * Math.PI;
    ctx.strokeStyle = `rgba(140, 140, 255, ${0.12 + Math.random() * 0.18})`;
    ctx.lineWidth = 1 + Math.random() * 1.5;
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x + Math.cos(angle) * len, y + Math.sin(angle) * len);
    ctx.stroke();
  }

  return new THREE.CanvasTexture(canvas);
}
