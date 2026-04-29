import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/examples/jsm/loaders/DRACOLoader.js';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { Sky } from 'three/examples/jsm/objects/Sky.js';
import { Clouds } from './atmosphere/Clouds.js';
import { createFoliage } from './world/Foliage.js';
import { Village } from './world/Village.js';
import { Water } from './world/Water.js';
import { Menu } from './ui/Menu.js';
import { HUD } from './ui/HUD.js';
import './styles.css';

// Surface any uncaught error visibly so we never silently white-screen again.
window.addEventListener('error', (event) => {
  showFatalError(event.error?.stack || event.message || 'Unknown error');
});
window.addEventListener('unhandledrejection', (event) => {
  showFatalError(event.reason?.stack || event.reason || 'Unhandled promise rejection');
});
function showFatalError(detail) {
  let el = document.getElementById('fatal-error');
  if (!el) {
    el = document.createElement('pre');
    el.id = 'fatal-error';
    el.style.cssText = 'position:fixed;left:24px;bottom:24px;z-index:9999;max-width:calc(100vw - 48px);padding:14px 16px;background:rgba(120,30,30,0.92);color:#fff;font:12px/1.4 ui-monospace,monospace;border-radius:6px;border:1px solid #ff6a6a;white-space:pre-wrap;max-height:40vh;overflow:auto;';
    document.body.appendChild(el);
  }
  el.textContent = String(detail);
  console.error('[Hollowfen fatal]', detail);
}

const PLAYER_RADIUS = 0.42;
const PLAYER_HEIGHT = 1.78;
const GRAVITY = 18;
const JUMP_STRENGTH = 6.2;
const WALK_SPEED = 4.2;
const RUN_SPEED = 7.2;
const CAMERA_DISTANCE = 6.5;
const CAMERA_HEIGHT_OFFSET = 1.6;
const CAMERA_LERP = 1 - Math.pow(0.001, 1 / 60);

const app = document.querySelector('#app');
const status = document.createElement('div');
status.className = 'status';
status.innerHTML = `
  <div class="eyebrow">Hollowfen Prototype</div>
  <h1>The Failing Village</h1>
  <p>Loading the clean medieval village foundation...</p>
`;
app.appendChild(status);

const hint = document.createElement('div');
hint.className = 'hint';
hint.textContent = 'WASD to walk - Shift to run - Space to jump - Drag to turn camera';
app.appendChild(hint);

const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 0.55;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
app.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.fog = new THREE.FogExp2(0xa6becc, 0.0014);

const sky = new Sky();
sky.scale.setScalar(8000);
sky.material.uniforms.turbidity.value = 2.0;
sky.material.uniforms.rayleigh.value = 0.9;
sky.material.uniforms.mieCoefficient.value = 0.003;
sky.material.uniforms.mieDirectionalG.value = 0.85;
const sunPosition = new THREE.Vector3();
const sunPhi = THREE.MathUtils.degToRad(90 - 38);
const sunTheta = THREE.MathUtils.degToRad(115);
sunPosition.setFromSphericalCoords(1, sunPhi, sunTheta);
sky.material.uniforms.sunPosition.value.copy(sunPosition);
scene.add(sky);

const pmrem = new THREE.PMREMGenerator(renderer);
const skyEnvScene = new THREE.Scene();
skyEnvScene.add(sky.clone());
scene.environment = pmrem.fromScene(skyEnvScene, 0.02).texture;

const camera = new THREE.PerspectiveCamera(58, window.innerWidth / window.innerHeight, 0.1, 900);
camera.position.set(58, 30, 70);

const clouds = new Clouds(scene);
// const water = new Water(scene);  // removed for now — URP-style water not achievable in Three.js without significant shader work

const hemi = new THREE.HemisphereLight(0xc6d2dc, 0x33402c, 0.5);
scene.add(hemi);

const ambient = new THREE.AmbientLight(0xa3afb6, 0.10);
scene.add(ambient);

const sun = new THREE.DirectionalLight(0xfff1d4, 3.0);
sun.position.copy(sunPosition).multiplyScalar(120);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -140;
sun.shadow.camera.right = 140;
sun.shadow.camera.top = 140;
sun.shadow.camera.bottom = -140;
sun.shadow.camera.near = 1;
sun.shadow.camera.far = 260;
sun.shadow.bias = -0.0004;
sun.shadow.normalBias = 0.04;
scene.add(sun);
scene.add(sun.target);

const fill = new THREE.DirectionalLight(0x9ab8d5, 0.55);
fill.position.set(55, 30, -75);
scene.add(fill);

const rim = new THREE.DirectionalLight(0xffe6b8, 0.4);
rim.position.set(70, 22, 60);
scene.add(rim);

scene.add(createTerrain());

let foliage = null;

const loader = new GLTFLoader();
const draco = new DRACOLoader();
draco.setDecoderPath('/draco/gltf/');
loader.setDRACOLoader(draco);
loader.setMeshoptDecoder(MeshoptDecoder);

const root = new THREE.Group();
root.name = 'HollowfenDemo3Root';
scene.add(root);

const clock = new THREE.Clock();
const keys = new Set();
const worldState = {
  bounds: null,
  colliders: []
};
const cameraState = {
  yaw: Math.PI * 0.34,
  pitch: 0.48,
  dragging: false,
  lastX: 0,
  lastY: 0
};
const player = {
  root: new THREE.Group(),
  mixer: null,
  actions: {},
  currentAction: null,
  oneShotAction: null,
  oneShotName: null,
  verticalVelocity: 0,
  grounded: true,
  facing: 0,
  ready: false
};

// ---------- Developer / time-of-day controls ----------
const dev = {
  hours: 14,                // 14:00 = mid-afternoon (matches our default sky)
  speedMultiplier: 1.0,
  noclip: false,
  fogEnabled: true,
  wireframe: false,
  showFps: false
};

const FOG_COLOR_DAY = new THREE.Color(0xa6becc);
const FOG_COLOR_DUSK = new THREE.Color(0xc89878);
const FOG_COLOR_NIGHT = new THREE.Color(0x161e2a);

function setTimeOfDay(hours) {
  dev.hours = hours;
  // sunHeight: -1 at midnight, 0 at sunrise/sunset, 1 at noon
  const sunHeight = Math.sin(((hours - 6) / 12) * Math.PI);
  const phi = (Math.PI / 2) * (1 - sunHeight);
  const theta = THREE.MathUtils.degToRad(115);

  const pos = new THREE.Vector3().setFromSphericalCoords(1, phi, theta);
  sky.material.uniforms.sunPosition.value.copy(pos);
  sun.position.copy(pos).multiplyScalar(140);

  // Sun intensity falls off with elevation, never quite zero (moonlight).
  sun.intensity = Math.max(0.05, sunHeight * 3.2 + 0.05);
  // Hemisphere/ambient light: bright by day, deep-blue tint at night.
  if (sunHeight > 0) {
    hemi.intensity = 0.5;
    hemi.color.setHex(0xc6d2dc);
    hemi.groundColor.setHex(0x33402c);
  } else {
    hemi.intensity = 0.18;
    hemi.color.setHex(0x6f8aa6);
    hemi.groundColor.setHex(0x10131a);
  }

  // Sky scattering — hazy/golden near horizon, clear at noon, dim at night
  const horizonProx = 1 - Math.abs(sunHeight);    // peaks near sunrise/sunset
  const isNight = sunHeight < -0.05;
  sky.material.uniforms.turbidity.value = isNight ? 0.6 : (2.0 + horizonProx * 6);
  sky.material.uniforms.rayleigh.value = isNight ? 0.2 : (0.9 + horizonProx * 1.2);
  sky.material.uniforms.mieCoefficient.value = isNight ? 0.001 : 0.003 + horizonProx * 0.005;

  // Fog colour shifts: day → dusk warm → night blue-black
  const tFogNight = Math.max(0, -sunHeight);
  const tFogDusk = Math.max(0, 1 - Math.abs(sunHeight) - Math.max(0, sunHeight - 0.3));
  if (scene.fog) {
    scene.fog.color
      .copy(FOG_COLOR_DAY)
      .lerp(FOG_COLOR_DUSK, tFogDusk * 0.7)
      .lerp(FOG_COLOR_NIGHT, tFogNight);
  }

  // Compensate exposure so the night doesn't crush black completely
  renderer.toneMappingExposure = 0.55 - tFogNight * 0.18;
}
setTimeOfDay(dev.hours);

function setFogEnabled(enabled) {
  dev.fogEnabled = enabled;
  scene.fog = enabled ? new THREE.FogExp2(FOG_COLOR_DAY.getHex(), 0.0014) : null;
  if (enabled) setTimeOfDay(dev.hours); // restore current time's fog tint
}

function setWireframe(enabled) {
  dev.wireframe = enabled;
  scene.traverse((obj) => {
    if (!obj.isMesh) return;
    const mats = Array.isArray(obj.material) ? obj.material : [obj.material];
    for (const m of mats) { if (m && 'wireframe' in m) m.wireframe = enabled; }
  });
}

const fpsHud = document.createElement('div');
fpsHud.id = 'fps-hud';
fpsHud.style.cssText = 'position:fixed;right:16px;top:16px;z-index:50;padding:6px 10px;background:rgba(8,12,10,0.78);border:1px solid rgba(255,255,255,0.12);border-radius:6px;color:#d9bd6d;font:12px ui-monospace,monospace;letter-spacing:0.04em;display:none;';
fpsHud.textContent = '— fps';
document.body.appendChild(fpsHud);
let fpsAccum = 0;
let fpsFrames = 0;

let hud = null;

const menu = new Menu({
  onBeginGame: () => {
    gameStarted = true;
    keys.clear();
    if (hud) hud.show();
  },
  onResume: () => {
    keys.clear();
  },
  dev: {
    getHours: () => dev.hours,
    setHours: (h) => setTimeOfDay(h),
    setSpeed: (m) => { dev.speedMultiplier = m; },
    getSpeed: () => dev.speedMultiplier,
    setNoclip: (b) => { dev.noclip = b; },
    getNoclip: () => dev.noclip,
    setFog: (b) => setFogEnabled(b),
    getFog: () => dev.fogEnabled,
    setWireframe: (b) => setWireframe(b),
    getWireframe: () => dev.wireframe,
    setShowFps: (b) => {
      dev.showFps = b;
      fpsHud.style.display = b ? 'block' : 'none';
    },
    getShowFps: () => dev.showFps
  }
});

let gameStarted = false;
menu.showMain();

loadVillage();
animate();

window.addEventListener('keydown', (event) => {
  // Menu-aware keybinds
  if (event.code === 'Escape') {
    event.preventDefault();
    if (menu.isOpen()) {
      // Modal first, then menu
      menu.hide();
      if (gameStarted) {
        // resumed
      } else {
        menu.showMain();
      }
    } else if (gameStarted) {
      menu.showPause();
    }
    return;
  }
  // Hot-bar shortcuts open the pause menu directly to a specific tab.
  // Each maps a single-letter key → tab id.
  const TAB_KEYS = { KeyT: 'story', KeyJ: 'guide', KeyC: 'wren', KeyK: 'controls', KeyO: 'settings' };
  if (TAB_KEYS[event.code] && gameStarted && !menu.isOpen()) {
    event.preventDefault();
    menu.activeTab = TAB_KEYS[event.code];
    menu.showPause();
    return;
  }
  // While menu is open, swallow gameplay keys
  if (menu.isOpen()) return;

  keys.add(event.code);
  if (event.code === 'Space') {
    event.preventDefault();
    tryJump();
  }
});

window.addEventListener('keyup', (event) => {
  keys.delete(event.code);
});

renderer.domElement.addEventListener('pointerdown', (event) => {
  cameraState.dragging = true;
  cameraState.lastX = event.clientX;
  cameraState.lastY = event.clientY;
  renderer.domElement.setPointerCapture?.(event.pointerId);
});

renderer.domElement.addEventListener('pointermove', (event) => {
  if (!cameraState.dragging) return;
  const dx = event.clientX - cameraState.lastX;
  const dy = event.clientY - cameraState.lastY;
  cameraState.lastX = event.clientX;
  cameraState.lastY = event.clientY;
  cameraState.yaw -= dx * 0.006;
  cameraState.pitch = THREE.MathUtils.clamp(cameraState.pitch + dy * 0.0035, -0.15, 1.45);
});

renderer.domElement.addEventListener('pointerup', (event) => {
  cameraState.dragging = false;
  renderer.domElement.releasePointerCapture?.(event.pointerId);
});

async function loadVillage() {
  try {
    const village = new Village({ scene, terrainHeightAt });
    const { spawn, colliders } = await village.load();

    worldState.colliders = colliders;
    worldState.spawn = spawn;

    await loadWren();
    spawnWren();
    foliage = await createFoliage({
      scene,
      terrainHeightAt,
      colliders
    });

    // HUD becomes available now that the player + colliders exist. It stays
    // hidden until the player presses Begin (handled by the Menu callback).
    hud = new HUD({
      player,
      cameraState,
      colliders,
      onOpenMenu: (tab) => {
        menu.activeTab = tab;
        menu.showPause();
      }
    });
    // Replace the legacy corner status + hint with the HUD once it's live.
    status.style.display = 'none';
    hint.style.display = 'none';
    const stats = foliage.stats || { trees: 0, shrubs: 0, grass: 0, rocks: 0 };
    status.innerHTML = `
      <div class="eyebrow">Hollowfen Prototype</div>
      <h1>The Failing Village</h1>
      <p>Wren returns to the failing village. Walk through to find the watermill, the inn, the chapel, and the workshops.</p>
      <div class="stats">
        <span>${colliders.length} collision walls</span>
        <span>${stats.trees + stats.shrubs} foliage</span>
      </div>
    `;
  } catch (error) {
    console.error(error);
    status.innerHTML = `
      <div class="eyebrow">Hollowfen Prototype</div>
      <h1>Load failed</h1>
      <p>${String(error.message || error)}</p>
    `;
  }
}

function createTerrain() {
  const width = 440;
  const depth = 360;
  const geometry = new THREE.PlaneGeometry(width, depth, 176, 144);
  geometry.rotateX(-Math.PI / 2);

  const position = geometry.attributes.position;
  const colors = [];
  const grass = new THREE.Color(0xffffff);
  const wetGrass = new THREE.Color(0xc4d2b2);
  const path = new THREE.Color(0xd1a87a);
  const stone = new THREE.Color(0xb8b3a4);
  const forestFloor = new THREE.Color(0x8a986d);

  for (let i = 0; i < position.count; i++) {
    const x = position.getX(i);
    const z = position.getZ(i);
    position.setY(i, terrainHeightAt(x, z));

    const distance = Math.hypot(x * 0.78, z);
    const pathMask = Math.max(
      pathBand(x, z, [-108, -40], [-18, -10], 12),
      pathBand(x, z, [-28, -8], [52, 50], 10),
      pathBand(x, z, [48, 38], [112, -48], 9),
      pathBand(x, z, [-34, 8], [42, 26], 7)
    );
    const stoneMask = smoothstep(55, 124, distance) * 0.18;
    const forestMask = smoothstep(95, 175, distance);
    const noise = (Math.sin(x * 0.12) + Math.cos(z * 0.13)) * 0.5;
    const color = grass.clone().lerp(wetGrass, THREE.MathUtils.clamp(0.24 + noise * 0.12, 0, 1));
    color.lerp(stone, stoneMask);
    color.lerp(forestFloor, forestMask * 0.6);
    color.lerp(path, pathMask);
    colors.push(color.r, color.g, color.b);
  }

  geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  geometry.computeVertexNormals();

  const grassMap = makeProceduralGrassTexture();
  const repeat = 80;
  grassMap.wrapS = grassMap.wrapT = THREE.RepeatWrapping;
  grassMap.repeat.set(repeat, repeat * (depth / width));
  grassMap.anisotropy = 16;

  const material = new THREE.MeshStandardMaterial({
    map: grassMap,
    vertexColors: true,
    roughness: 0.96,
    metalness: 0,
    envMapIntensity: 0.22,
    side: THREE.DoubleSide
  });
  const terrain = new THREE.Mesh(geometry, material);
  terrain.name = 'Hollowfen sculpted terrain';
  terrain.receiveShadow = true;
  console.log('[Terrain]', {
    verts: position.count,
    centerY: terrainHeightAt(0, 0).toFixed(2),
    textureSize: grassMap.image?.width + 'x' + grassMap.image?.height
  });
  return terrain;
}

function makeProceduralGrassTexture() {
  const size = 512;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');

  ctx.fillStyle = '#4f6638';
  ctx.fillRect(0, 0, size, size);

  const image = ctx.getImageData(0, 0, size, size);
  const data = image.data;
  for (let i = 0; i < data.length; i += 4) {
    const noise = (Math.random() - 0.5) * 36;
    data[i] = Math.max(0, Math.min(255, data[i] + noise * 0.6));
    data[i + 1] = Math.max(0, Math.min(255, data[i + 1] + noise));
    data[i + 2] = Math.max(0, Math.min(255, data[i + 2] + noise * 0.4));
  }
  ctx.putImageData(image, 0, 0);

  for (let i = 0; i < 1800; i++) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const len = 2 + Math.random() * 4;
    const angle = Math.random() * Math.PI * 2;
    const shade = 30 + Math.random() * 40;
    ctx.strokeStyle = `rgba(${Math.round(60 + shade * 0.4)}, ${Math.round(80 + shade)}, ${Math.round(40 + shade * 0.3)}, 0.85)`;
    ctx.lineWidth = 0.8 + Math.random() * 0.6;
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x + Math.cos(angle) * len, y + Math.sin(angle) * len);
    ctx.stroke();
  }

  for (let i = 0; i < 90; i++) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const r = 4 + Math.random() * 12;
    ctx.fillStyle = `rgba(${85 + Math.random() * 30}, ${65 + Math.random() * 25}, ${42 + Math.random() * 18}, ${0.18 + Math.random() * 0.18})`;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
  }

  for (let i = 0; i < 60; i++) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const r = 1 + Math.random() * 2.5;
    ctx.fillStyle = `rgba(${230 + Math.random() * 25}, ${230 + Math.random() * 25}, ${200 + Math.random() * 30}, 0.7)`;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

function pathBand(x, z, a, b, width) {
  const distance = distanceToSegment(x, z, a[0], a[1], b[0], b[1]);
  return 1 - smoothstep(width * 0.42, width, distance);
}

function distanceToSegment(px, pz, ax, az, bx, bz) {
  const dx = bx - ax;
  const dz = bz - az;
  const lengthSq = dx * dx + dz * dz || 1;
  const t = THREE.MathUtils.clamp(((px - ax) * dx + (pz - az) * dz) / lengthSq, 0, 1);
  const x = ax + dx * t;
  const z = az + dz * t;
  return Math.hypot(px - x, pz - z);
}

function smoothstep(edge0, edge1, value) {
  const t = THREE.MathUtils.clamp((value - edge0) / (edge1 - edge0), 0, 1);
  return t * t * (3 - 2 * t);
}

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(clock.getDelta(), 0.05);
  // Game logic only runs after the player has clicked Begin and the menu
  // is closed. The renderer keeps drawing so the world is visible behind
  // the title screen / pause overlay.
  if (gameStarted && !menu.isOpen()) {
    updatePlayer(dt);
    updateCamera(dt);
  }
  const focus = player.ready ? player.root.position : new THREE.Vector3();
  clouds.update(dt, focus);
  if (hud) {
    if (gameStarted && !menu.isOpen()) {
      if (!hud.isVisible()) hud.show();
      hud.update(dt);
    } else if (hud.isVisible()) {
      hud.hide();
    }
  }
  renderer.render(scene, camera);

  if (dev.showFps && dt > 0) {
    fpsAccum += dt;
    fpsFrames += 1;
    if (fpsAccum >= 0.5) {
      fpsHud.textContent = `${Math.round(fpsFrames / fpsAccum)} fps`;
      fpsAccum = 0;
      fpsFrames = 0;
    }
  }
}

async function loadWren() {
  const manifest = await fetch('/models/wren/manifest.json', { cache: 'no-store' }).then((res) => res.json());
  const loader = new FBXLoader();
  const model = await loader.loadAsync(manifest.active);
  normalizeMixamoObjectNames(model);

  const box = new THREE.Box3().setFromObject(model);
  const size = box.getSize(new THREE.Vector3());
  model.scale.setScalar(size.y > 0 ? PLAYER_HEIGHT / size.y : 0.01);
  const scaledBox = new THREE.Box3().setFromObject(model);
  model.position.y -= scaledBox.min.y;

  model.traverse((child) => {
    if (!child.isMesh) return;
    child.castShadow = true;
    child.receiveShadow = true;
    const materials = Array.isArray(child.material) ? child.material : [child.material];
    const converted = materials.map((mat) => {
      if (!mat) return mat;
      if (mat.map) mat.map.colorSpace = THREE.SRGBColorSpace;
      if (mat.isMeshStandardMaterial) {
        mat.roughness = Math.max(mat.roughness ?? 0.8, 0.84);
        mat.metalness = Math.min(mat.metalness ?? 0, 0.03);
        mat.envMapIntensity = 1.15;
        return mat;
      }
      const next = new THREE.MeshStandardMaterial({
        color: mat.color ? mat.color.clone() : new THREE.Color(0xd6c8b4),
        map: mat.map || null,
        normalMap: mat.normalMap || null,
        roughness: 0.86,
        metalness: 0,
        transparent: mat.transparent || false,
        opacity: mat.opacity ?? 1
      });
      if (next.map) next.map.colorSpace = THREE.SRGBColorSpace;
      return next;
    });
    child.material = Array.isArray(child.material) ? converted : converted[0];
  });

  player.root.name = 'Wren';
  player.root.add(model);
  player.mixer = new THREE.AnimationMixer(model);
  player.mixer.addEventListener('finished', (event) => {
    if (player.oneShotAction && event.action === player.oneShotAction) {
      player.oneShotAction = null;
      player.oneShotName = null;
    }
  });

  for (const [name, config] of Object.entries(manifest.animations || {})) {
    const fbx = await loader.loadAsync(config.path);
    normalizeMixamoObjectNames(fbx);
    const clip = selectClip(fbx.animations, config.clip);
    if (!clip) continue;
    clip.name = name;
    sanitizeClipTracks(clip);
    if (name === 'walk' || name === 'run') stripHorizontalRootMotion(clip);
    if (clip.tracks.length === 0) continue;
    const action = player.mixer.clipAction(clip);
    action.userData = { options: config };
    player.actions[name] = action;
  }

  scene.add(player.root);
  player.ready = true;
  playAction('idle', 0);
}

function spawnWren() {
  const spawn = worldState.spawn || { x: 0, z: 0, facing: 0 };
  player.root.position.set(spawn.x, terrainHeightAt(spawn.x, spawn.z), spawn.z);
  player.facing = spawn.facing ?? 0;
  player.root.rotation.y = player.facing;
  updateCamera(1);
}

function updatePlayer(dt) {
  if (!player.ready) return;

  const moveX = (keys.has('KeyD') || keys.has('ArrowRight') ? 1 : 0) - (keys.has('KeyA') || keys.has('ArrowLeft') ? 1 : 0);
  const moveZ = (keys.has('KeyW') || keys.has('ArrowUp') ? 1 : 0) - (keys.has('KeyS') || keys.has('ArrowDown') ? 1 : 0);
  const moving = moveX !== 0 || moveZ !== 0;
  const running = keys.has('ShiftLeft') || keys.has('ShiftRight');
  const speed = (running ? RUN_SPEED : WALK_SPEED) * dev.speedMultiplier;

  let worldX = 0;
  let worldZ = 0;
  if (moving) {
    const length = Math.hypot(moveX, moveZ);
    const x = moveX / length;
    const z = moveZ / length;
    const rightX = Math.cos(cameraState.yaw);
    const rightZ = -Math.sin(cameraState.yaw);
    const forwardX = -Math.sin(cameraState.yaw);
    const forwardZ = -Math.cos(cameraState.yaw);
    worldX = rightX * x + forwardX * z;
    worldZ = rightZ * x + forwardZ * z;
    const targetX = player.root.position.x + worldX * speed * dt;
    const targetZ = player.root.position.z + worldZ * speed * dt;
    // Noclip: skip collision resolution when dev mode is enabled
    const resolved = dev.noclip ? { x: targetX, z: targetZ } : resolveMovement(targetX, targetZ);
    player.root.position.x = resolved.x;
    player.root.position.z = resolved.z;
    const targetFacing = Math.atan2(worldX, worldZ);
    player.facing += wrapAngle(targetFacing - player.facing) * Math.min(1, dt * 12);
    player.root.rotation.y = player.facing;
  }

  const ground = terrainHeightAt(player.root.position.x, player.root.position.z);
  if (player.grounded) {
    player.root.position.y += (ground - player.root.position.y) * Math.min(1, dt * 12);
  } else {
    player.verticalVelocity -= GRAVITY * dt;
    player.root.position.y += player.verticalVelocity * dt;
    if (player.verticalVelocity <= 0 && player.root.position.y <= ground) {
      player.root.position.y = ground;
      player.verticalVelocity = 0;
      player.grounded = true;
      player.oneShotAction = null;
      player.oneShotName = null;
    }
  }

  if (player.oneShotName !== 'jump') {
    if (!moving) playAction('idle');
    else playAction(running ? 'run' : 'walk');
  }

  player.mixer?.update(dt);
}

function updateCamera(dt) {
  if (!player.ready) return;
  const target = player.root.position.clone().add(new THREE.Vector3(0, 1.35, 0));
  const horizontalDistance = Math.cos(cameraState.pitch) * CAMERA_DISTANCE;
  const desired = new THREE.Vector3(
    target.x + Math.sin(cameraState.yaw) * horizontalDistance,
    target.y + Math.sin(cameraState.pitch) * CAMERA_DISTANCE + CAMERA_HEIGHT_OFFSET,
    target.z + Math.cos(cameraState.yaw) * horizontalDistance
  );
  const alpha = dt >= 1 ? 1 : CAMERA_LERP;
  camera.position.lerp(desired, alpha);
  camera.lookAt(target);
}

function tryJump() {
  if (!player.ready || !player.grounded) return;
  player.grounded = false;
  player.verticalVelocity = JUMP_STRENGTH;
  playOneShot('jump');
}

function playAction(name, fade = 0.22) {
  const action = player.actions[name];
  if (!action || player.currentAction === action) return;
  action.enabled = true;
  action.paused = false;
  action.reset().setLoop(THREE.LoopRepeat, Infinity).setEffectiveTimeScale(1).fadeIn(fade).play();
  if (player.currentAction) player.currentAction.fadeOut(fade);
  player.currentAction = action;
}

function playOneShot(name) {
  const action = player.actions[name];
  if (!action) return;
  const options = action.userData.options || {};
  if (player.currentAction) player.currentAction.fadeOut(options.fade ?? 0.16);
  action.reset()
    .setLoop(THREE.LoopOnce, 1)
    .setEffectiveTimeScale(options.timeScale ?? 1)
    .fadeIn(options.fade ?? 0.16)
    .play();
  if (options.startTime) action.time = options.startTime;
  action.clampWhenFinished = true;
  player.currentAction = action;
  player.oneShotAction = action;
  player.oneShotName = name;
}

function resolveMovement(x, z) {
  const ground = terrainHeightAt(x, z);
  let resolvedX = x;
  let resolvedZ = z;
  for (const collider of worldState.colliders) {
    if (ground >= collider.top - 0.1) continue;
    const nearestX = THREE.MathUtils.clamp(resolvedX, collider.minX, collider.maxX);
    const nearestZ = THREE.MathUtils.clamp(resolvedZ, collider.minZ, collider.maxZ);
    const dx = resolvedX - nearestX;
    const dz = resolvedZ - nearestZ;
    const distanceSq = dx * dx + dz * dz;
    if (distanceSq > PLAYER_RADIUS * PLAYER_RADIUS) continue;

    if (distanceSq > 0.0001) {
      const distance = Math.sqrt(distanceSq);
      const push = (PLAYER_RADIUS - distance) / distance;
      resolvedX += dx * push;
      resolvedZ += dz * push;
      continue;
    }

    const left = Math.abs(resolvedX - collider.minX);
    const right = Math.abs(collider.maxX - resolvedX);
    const back = Math.abs(resolvedZ - collider.minZ);
    const front = Math.abs(collider.maxZ - resolvedZ);
    const min = Math.min(left, right, back, front);
    if (min === left) resolvedX = collider.minX - PLAYER_RADIUS;
    else if (min === right) resolvedX = collider.maxX + PLAYER_RADIUS;
    else if (min === back) resolvedZ = collider.minZ - PLAYER_RADIUS;
    else resolvedZ = collider.maxZ + PLAYER_RADIUS;
  }

  return { x: resolvedX, z: resolvedZ };
}

function isBlocked(x, z, ground) {
  for (const collider of worldState.colliders) {
    if (ground >= collider.top - 0.1) continue;
    if (
      x > collider.minX - PLAYER_RADIUS &&
      x < collider.maxX + PLAYER_RADIUS &&
      z > collider.minZ - PLAYER_RADIUS &&
      z < collider.maxZ + PLAYER_RADIUS
    ) {
      return true;
    }
  }
  return false;
}

function normalizeColliders(colliders) {
  return colliders
    .filter((collider) => collider && collider.active !== false && collider.shape === 'box')
    .filter((collider) => collider.top > 1.2)
    .filter((collider) => {
      const name = String(collider.name || '').toLowerCase();
      return !name.includes('roof') && !name.includes('ceiling') && !name.includes('floor');
    })
    .map((collider) => ({
      minX: Math.min(collider.minX, collider.maxX),
      maxX: Math.max(collider.minX, collider.maxX),
      minZ: Math.min(collider.minZ, collider.maxZ),
      maxZ: Math.max(collider.minZ, collider.maxZ),
      top: collider.top,
      name: collider.name || 'collider'
    }));
}

function selectClip(animations, preferredName) {
  if (!animations || animations.length === 0) return null;
  if (!preferredName) return animations[0];
  const needle = preferredName.toLowerCase();
  return animations.find((clip) => clip.name.toLowerCase().includes(needle)) || animations[0];
}

function sanitizeClipTracks(clip) {
  const validTracks = [];
  for (const track of clip.tracks) {
    if (track.name.includes('|')) track.name = track.name.split('|').pop();
    if (track.name.startsWith('Armature.')) track.name = track.name.slice('Armature.'.length);
    track.name = normalizeMixamoName(track.name);
    try {
      THREE.PropertyBinding.parseTrackName(track.name);
      validTracks.push(track);
    } catch {
      // Meshy sometimes includes naked root transform tracks like "position".
      // Those are not bindable in Three and can be dropped safely.
    }
  }
  clip.tracks = validTracks;
}

function stripHorizontalRootMotion(clip) {
  for (const track of clip.tracks) {
    if (!track.name.endsWith('.position')) continue;
    const lower = track.name.toLowerCase();
    if (!lower.includes('hip') && !lower.includes('root')) continue;
    const values = track.values;
    if (values.length < 3) continue;
    const x0 = values[0];
    const z0 = values[2];
    for (let i = 0; i < values.length; i += 3) {
      values[i] = x0;
      values[i + 2] = z0;
    }
  }
}

function normalizeMixamoObjectNames(root) {
  root.traverse((child) => {
    if (typeof child.name === 'string') child.name = normalizeMixamoName(child.name);
  });
}

function normalizeMixamoName(name) {
  return name.replace(/^mixamorig\d*:/, 'mixamorig').replace(/^mixamorig\d+/, 'mixamorig');
}

function terrainHeightAt(x, z) {
  const distance = Math.hypot(x * 0.78, z);
  const edgeRise = smoothstep(58, 128, distance) * 5.4;
  const roll = Math.sin(x * 0.035) * 0.35 + Math.cos(z * 0.045) * 0.28 + Math.sin((x + z) * 0.025) * 0.22;
  const centerFlatten = 1 - smoothstep(20, 74, distance);
  return -0.18 + edgeRise + roll * (1 - centerFlatten * 0.92);
}

function wrapAngle(angle) {
  while (angle > Math.PI) angle -= Math.PI * 2;
  while (angle < -Math.PI) angle += Math.PI * 2;
  return angle;
}

window.addEventListener('resize', () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});
