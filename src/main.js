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
import { DialogController } from './ui/Dialog.js';
import { playHomecomingIntro, isNarrationActive } from './ui/Narration.js';
import { DIALOGS, pickDialogForBram } from './data/Dialogs.js';
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
const CAMERA_DISTANCE_DEFAULT = 6.5;
const CAMERA_DISTANCE_DIALOG = 3.6;
let cameraDistance = CAMERA_DISTANCE_DEFAULT;
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
// Pixel ratio is applied below via applyQuality(). Shadow map size + type
// also live there. Antialias has to stay fixed at construction since toggling
// requires recreating the renderer.
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 0.55;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.shadowMap.autoUpdate = true;
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

// Far plane was 900 — way past the fog wall (FogExp2 0.0014 hits ~99% by ~1500
// and is visually opaque by ~600). Tightening to 400 lets the GPU reject more
// distant geometry per frame and gives the depth buffer better precision.
// applyQuality() can override this per quality tier.
const camera = new THREE.PerspectiveCamera(58, window.innerWidth / window.innerHeight, 0.1, 400);
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
// Initial shadow map size — applyQuality() resizes per quality tier. 1024² is
// 1/4 the texels of 2048² and visually similar after PCF filtering at the
// distances we shadow.
sun.shadow.mapSize.set(1024, 1024);
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

// ---------- Render quality presets ----------
// The "Render quality" buttons in the Settings tab call applyQuality(level).
// Each preset trades shadow / pixel detail for FPS:
//   low    — DPR 1.0, 512² shadow map (basic, no PCF), camera far 280
//   medium — DPR 1.25, 1024² shadow map (PCF), camera far 350
//   high   — DPR 1.5, 1024² shadow map (PCFSoft), camera far 400
// Antialias stays on (locked at renderer construction). On retina displays the
// DPR cap is the dominant lever — DPR 2 → 1.5 cuts pixel count ~44%.
const QUALITY_PRESETS = {
  low:    { dprCap: 1.0,  shadowSize: 512,  shadowType: THREE.BasicShadowMap,    far: 280 },
  medium: { dprCap: 1.25, shadowSize: 1024, shadowType: THREE.PCFShadowMap,      far: 350 },
  high:   { dprCap: 1.5,  shadowSize: 1024, shadowType: THREE.PCFSoftShadowMap,  far: 400 }
};
let currentQuality = 'high';

function applyQuality(level) {
  const preset = QUALITY_PRESETS[level] || QUALITY_PRESETS.high;
  currentQuality = QUALITY_PRESETS[level] ? level : 'high';
  // Pixel ratio: cap below the device's DPR. On a DPR-2 retina at "high" this
  // means ~1.5 → 56% of the native pixel count, the single biggest perf win.
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, preset.dprCap));
  // Shadow type can be swapped without re-creating maps.
  renderer.shadowMap.type = preset.shadowType;
  // Resize the shadow map by disposing the existing target — Three.js will
  // recreate it at the new size on the next frame.
  if (sun.shadow.mapSize.width !== preset.shadowSize) {
    sun.shadow.mapSize.set(preset.shadowSize, preset.shadowSize);
    if (sun.shadow.map) {
      sun.shadow.map.dispose();
      sun.shadow.map = null;
    }
  }
  // Camera far + projection.
  if (camera.far !== preset.far) {
    camera.far = preset.far;
    camera.updateProjectionMatrix();
  }
}

const worldState = {
  bounds: null,
  colliders: []
};

// Fallback ground plane. The Unity TerrainMesh from the village backdrop is
// the authoritative ground inside the village footprint, but it doesn't cover
// the whole world. This big flat plane sits at Y=0 underneath everything so
// Wren doesn't fall into the void if she walks past the terrain edge into
// the surrounding forest area. Color is a muted grass — the procedural
// foliage and existing forest floor obscure most of it.
const fallbackGround = new THREE.Mesh(
  new THREE.PlaneGeometry(2000, 2000).rotateX(-Math.PI / 2),
  new THREE.MeshStandardMaterial({ color: 0x4a5d36, roughness: 0.95, metalness: 0 })
);
fallbackGround.name = 'FallbackGround';
fallbackGround.receiveShadow = true;
fallbackGround.position.y = -0.05;  // tiny dip avoids z-fighting with TerrainMesh edges
scene.add(fallbackGround);

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
const _focusFallback = new THREE.Vector3();
const keys = new Set();
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
  ready: false,
  dialogTargetFacing: null
};

// ---------- Developer / time-of-day controls ----------
const dev = {
  hours: 14,                // 14:00 = mid-afternoon (matches our default sky)
  speedMultiplier: 1.0,
  noclip: false,
  fly: false,
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
const locationPinGroup = new THREE.Group();
locationPinGroup.name = 'DevLocationPins';
scene.add(locationPinGroup);

// Resolves once loadVillage() finishes its async work — the loading screen
// holds the title-to-game transition open until this resolves so Wren is
// never visible standing on the FallbackGround mid-load.
let _resolveVillageReady;
const villageReady = new Promise((resolve) => { _resolveVillageReady = resolve; });

// Re-acquire pointer lock on resume. Chrome enforces a ~1.3s cooldown after
// the user exits the lock via ESC — calling requestPointerLock() during the
// cooldown throws SecurityError. Catch it and retry once after the cooldown,
// guarded so we don't re-lock if the menu reopened in the meantime.
function tryReacquirePointerLock(retryDelay = 1400) {
  const el = renderer.domElement;
  if (!el || !gameStarted || menu.isOpen() || hud?.isWorldMapOpen?.()) return;
  let promise;
  try {
    promise = el.requestPointerLock?.();
  } catch {
    promise = null;
  }
  if (promise && typeof promise.catch === 'function') {
    promise.catch(() => {
      setTimeout(() => {
        if (!gameStarted || menu.isOpen() || hud?.isWorldMapOpen?.()) return;
        try { el.requestPointerLock?.(); } catch {}
      }, retryDelay);
    });
  }
}

const menu = new Menu({
  awaitVillageReady: () => villageReady,
  // Called synchronously inside the "Begin Foraging" click — locks the
  // mouse before the loading screen even appears. By the time the screen
  // fades to game ~3s later, the cursor is already captured and the
  // player can move the camera immediately.
  onRequestCameraLock: () => {
    try { renderer.domElement.requestPointerLock?.()?.catch?.(() => {}); }
    catch {}
  },
  onBeginGame: () => {
    gameStarted = true;
    keys.clear();
    if (hud) hud.show();
    // Play the Homecoming opening narration once per save. Captions fade
    // through over the live 3D scene so Wren is already standing in the
    // village when control is handed back to the player.
    playHomecomingIntro().catch((err) => console.warn('[narration] failed', err));
  },
  onResume: () => {
    keys.clear();
    tryReacquirePointerLock();
    // Restore the dialog overlay if a conversation was paused mid-line.
    if (dialog.isOpen()) dialog.resume();
  },
  // Live-applied render quality. The Settings tab's Low/Medium/High buttons
  // call this; main.js owns the actual renderer/shadow/camera knobs.
  setQuality: (level) => applyQuality(level),
  getQuality: () => currentQuality,
  dev: {
    getHours: () => dev.hours,
    setHours: (h) => setTimeOfDay(h),
    setSpeed: (m) => { dev.speedMultiplier = m; },
    getSpeed: () => dev.speedMultiplier,
    setNoclip: (b) => { dev.noclip = b; },
    getNoclip: () => dev.noclip,
    setFlyMode: (b) => {
      dev.fly = b;
      if (b) dev.noclip = true;
      player.verticalVelocity = 0;
      player.grounded = true;
    },
    getFlyMode: () => dev.fly,
    getPlayerSnapshot: () => getPlayerSnapshot(),
    setLocationPins: (pins) => renderLocationPins(pins),
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

// Apply the user's saved quality on startup so the chosen tier takes effect
// before the first frame. `loadSave()` inside Menu pulled the same setting.
const initialQuality = (menu.save && menu.save.settings && menu.save.settings.quality) || 'high';
applyQuality(initialQuality);
renderLocationPins(menu.locationPins || []);

let gameStarted = false;
menu.showMain();

// Expose key scene objects on window for debug / dev-tool inspection.
// Cheap, no production cost — only useful when DevTools is open.
if (typeof window !== 'undefined') {
  window.__hf = { scene, renderer, camera, player, worldState };
  // Lazy attach — dialog is created later in this file. Re-export after init.
  setTimeout(() => { window.__hf.dialog = dialog; window.__hf.cinematicCam = cinematicCam; window.__hf.interactState = interactState; }, 0);
}

// Proximity-based NPC interaction prompt. Shown when Wren is near a villager
// who has interaction animations loaded. Replaced by the real dialog UI once
// scenes are designed.
const INTERACT_RANGE = 3.2;
const interactPrompt = document.createElement('div');
interactPrompt.id = 'interact-prompt';
interactPrompt.style.cssText = [
  'position:fixed',
  'left:50%',
  'bottom:18%',
  'transform:translateX(-50%)',
  'padding:10px 18px',
  'background:rgba(8,10,14,0.7)',
  'color:#f4ead7',
  'font-family:"Cormorant Garamond", serif',
  'font-size:18px',
  'letter-spacing:0.5px',
  'border:1px solid rgba(212,184,130,0.45)',
  'border-radius:4px',
  'pointer-events:none',
  'display:none',
  'z-index:100'
].join(';');
document.body.appendChild(interactPrompt);
const interactState = { bramInRange: false };

// Per-NPC dialog progression state. Read by pickDialogForBram() to choose
// which dialog tree fires next when Wren talks to him.
const npcState = {
  bramTalkedRecognition: false,
  bramKeyGiven: false
};

// Cinematic camera state for dialog scenes. updateCamera() reads this when
// dialog is open and lerps toward the framed shot instead of the follow rig.
const cinematicCam = {
  active: false,
  position: new THREE.Vector3(),
  lookAt: new THREE.Vector3(),
  // Smoothed lerp targets so cuts have a slight glide instead of snapping.
  smoothPos: new THREE.Vector3(),
  smoothLookAt: new THREE.Vector3(),
  initialized: false
};

const dialog = new DialogController({
  dialogs: DIALOGS,
  onTrigger: (trigger) => {
    const target = trigger.target === 'bram' ? worldState.bram
      : trigger.target === 'marra' ? worldState.marra
      : null;
    if (!target) return;
    if (trigger.action === 'handoff') playVillagerOneShot(target, 'handoff');
    else if (target.actions[trigger.action]) playVillagerAction(target, trigger.action, 0.25);
  },
  onLineChange: ({ speaker, shot }) => {
    // Pick a shot for the speaker. Default 'wide' is the side-angle two-shot
    // we use for normal beats; 'closeup' tightens onto the speaker's face
    // for emotional climaxes (tagged on individual lines in Dialogs.js).
    if (!worldState.bram?.root || !player.ready) return;
    cinematicCam.active = true;
    setCinematicShotForSpeaker(speaker, shot);
    if (!cinematicCam.initialized) {
      // Seed the smoothed values from the gameplay camera's current state
      // so the first cinematic lerp glides over from wherever the player's
      // camera was, instead of snapping into the new shot.
      cinematicCam.smoothPos.copy(camera.position);
      const _camForward = new THREE.Vector3(0, 0, -1).applyQuaternion(camera.quaternion);
      cinematicCam.smoothLookAt.copy(camera.position).add(_camForward.multiplyScalar(6));
      cinematicCam.initialized = true;
    }
    // Wren also turns to face Bram during dialog.
    if (worldState.bram) {
      const dx = worldState.bram.root.position.x - player.root.position.x;
      const dz = worldState.bram.root.position.z - player.root.position.z;
      player.dialogTargetFacing = Math.atan2(dx, dz);
    }
  },
  onOutcome: (outcome) => {
    if (outcome.unlockCard === 'homecoming') npcState.bramTalkedRecognition = true;
    if (outcome.giveItem === 'item.mill_key') {
      npcState.bramKeyGiven = true;
      console.log('[outcome] Wren received the mill key');
    }
    if (outcome.completeQuest) console.log(`[outcome] quest complete: ${outcome.completeQuest}`);
    if (outcome.unlockCard) console.log(`[outcome] story card unlocked: ${outcome.unlockCard}`);
    // Chaining is handled inside DialogController so the letterbox/vignette
    // don't retract between linked dialogs. Outcome here just records state.
  },
  onClose: () => {
    if (worldState.bram) playVillagerAction(worldState.bram, 'idle', 0.4);
    cinematicCam.active = false;
    cinematicCam.initialized = false;
    player.dialogTargetFacing = null;
  }
});

// Compute and store the cinematic shot for a given speaker. The camera
// stays on a single side of the action (no shot/reverse-shot flips) and
// glides along the action axis as the speaker changes — a gentle left-to-
// right pan instead of a cut. The lookAt biases toward the current speaker
// so they land on the rule-of-thirds.
const _cinScratchA = new THREE.Vector3();
const _cinScratchB = new THREE.Vector3();
const _cinScratchPerp = new THREE.Vector3();
function setCinematicShotForSpeaker(speaker, shot = 'wide') {
  const bram = worldState.bram?.root;
  if (!bram || !player.ready) return;
  const bramPos = _cinScratchA.copy(bram.position);
  const wrenPos = _cinScratchB.copy(player.root.position);

  const dx = wrenPos.x - bramPos.x;
  const dz = wrenPos.z - bramPos.z;
  const sep = Math.hypot(dx, dz) || 1;
  const dirX = dx / sep;
  const dirZ = dz / sep;
  // Perpendicular to the Bram→Wren axis (CW around Y) — the open side of
  // the square at the Crooked Pintle.
  _cinScratchPerp.set(dirZ, 0, -dirX);

  const subjectPos = speaker === 'Wren' ? wrenPos : bramPos;
  const subjectHeadY = subjectPos.y + 1.62;

  if (shot === 'closeup') {
    // Tight close-up for emotional climaxes. Camera sits ~0.95m from the
    // speaker's face, on the same side of the action as the wide shot
    // (so the cut into closeup doesn't cross the 180° line) and tilted
    // slightly off-axis so the gaze reads as off-camera, not directly at
    // the lens — that 3/4 angle is what gives a face shot real intimacy.
    const closeDist = 0.95;
    const sideOffset = 0.45;
    const eyeY = subjectHeadY - 0.04;
    cinematicCam.position.set(
      subjectPos.x + _cinScratchPerp.x * sideOffset + dirX * (speaker === 'Wren' ? -closeDist : closeDist),
      eyeY,
      subjectPos.z + _cinScratchPerp.z * sideOffset + dirZ * (speaker === 'Wren' ? -closeDist : closeDist)
    );
    cinematicCam.lookAt.set(subjectPos.x, eyeY, subjectPos.z);
    return;
  }

  // Default — wide side-angle two-shot, gentle along-axis pan per speaker.
  const midX = (bramPos.x + wrenPos.x) * 0.5;
  const midZ = (bramPos.z + wrenPos.z) * 0.5;
  const midY = (bramPos.y + wrenPos.y) * 0.5 + 1.42;
  const sideDist = THREE.MathUtils.clamp(2.2 + sep * 0.35, 2.2, 3.6);
  const heightOffset = 0.55;
  const alongNudge = speaker === 'Wren' ? +0.55 : -0.55;

  cinematicCam.position.set(
    midX + _cinScratchPerp.x * sideDist + dirX * alongNudge,
    midY + heightOffset,
    midZ + _cinScratchPerp.z * sideDist + dirZ * alongNudge
  );
  cinematicCam.lookAt.set(
    subjectPos.x * 0.6 + midX * 0.4,
    subjectHeadY * 0.6 + midY * 0.4 + 0.04 - 0.07,
    subjectPos.z * 0.6 + midZ * 0.4
  );
}

loadVillage();
animate();

window.addEventListener('keydown', (event) => {
  // Menu-aware keybinds
  if (event.code === 'Escape') {
    event.preventDefault();
    if (hud?.isWorldMapOpen?.()) {
      hud.closeMap(); // onMapClose callback handles keys.clear + pointer lock
    } else if (menu.isOpen()) {
      // Modal first, then menu
      menu.hide();
      if (gameStarted) {
        keys.clear();
        tryReacquirePointerLock();
        // Restore the dialog overlay if a conversation was paused.
        if (dialog.isOpen()) dialog.resume();
      } else {
        menu.showMain();
      }
    } else if (gameStarted) {
      // Hide dialog while the pause menu is up so it doesn't show on top.
      if (dialog.isOpen()) dialog.suspend();
      menu.showPause();
    }
    return;
  }
  // Hot-bar shortcuts open the pause menu directly to a specific tab.
  // Each maps a single-letter key → tab id.
  const TAB_KEYS = { KeyT: 'story', KeyJ: 'guide', KeyC: 'wren', KeyK: 'controls', KeyO: 'settings' };
  if (TAB_KEYS[event.code] && gameStarted && !menu.isOpen()) {
    event.preventDefault();
    if (dialog.isOpen()) dialog.suspend();
    menu.activeTab = TAB_KEYS[event.code];
    menu.showPause();
    document.exitPointerLock?.();
    return;
  }
  if (event.code === 'KeyM' && gameStarted && !menu.isOpen()) {
    event.preventDefault();
    if (hud?.isWorldMapOpen?.()) {
      hud.closeMap(); // onMapClose callback handles keys.clear + pointer lock
    } else {
      keys.clear();
      if (dialog.isOpen()) dialog.suspend();
      hud?.openMap();
      document.exitPointerLock?.();
    }
    return;
  }
  // While menu is open, swallow gameplay keys
  if (menu.isOpen() || hud?.isWorldMapOpen?.()) return;

  // Dialog advance — Space and E both work while a dialog is open.
  if (dialog.isOpen() && (event.code === 'Space' || event.code === 'KeyE')) {
    event.preventDefault();
    dialog.advance();
    return;
  }

  // Open dialog with Bram. Fires before keys.add so E doesn't double-bind to fly mode.
  if (event.code === 'KeyE' && interactState.bramInRange && worldState.bram?.mixer && !dialog.isOpen()) {
    event.preventDefault();
    const id = pickDialogForBram(npcState);
    dialog.start(id);
    return;
  }

  keys.add(event.code);
  if (event.code === 'Space') {
    event.preventDefault();
    if (!dev.fly) tryJump();
  }
});

window.addEventListener('keyup', (event) => {
  keys.delete(event.code);
});

// FPS-style mouse look via the Pointer Lock API: while the pointer is
// locked, raw mouse motion (movementX/Y) drives the camera with no need
// to click-and-drag. ESC always releases the lock — the browser handles
// that automatically and the cursor reappears, so we get the menu UX
// "for free." Click the canvas to re-engage.
function tryEngagePointerLock() {
  if (!gameStarted) return;
  if (menu.isOpen()) return;
  if (hud?.isWorldMapOpen?.()) return;
  if (document.pointerLockElement === renderer.domElement) return;
  renderer.domElement.requestPointerLock?.();
}

renderer.domElement.addEventListener('click', tryEngagePointerLock);

document.addEventListener('mousemove', (event) => {
  if (document.pointerLockElement !== renderer.domElement) return;
  // Freeze mouse-look during cinematic dialog or scene narration so the
  // player doesn't fight the framing/captions.
  if (dialog.isOpen() || isNarrationActive()) return;
  cameraState.yaw -= event.movementX * 0.0025;
  cameraState.pitch = THREE.MathUtils.clamp(
    cameraState.pitch + event.movementY * 0.0018,
    -0.15,
    1.45
  );
});

document.addEventListener('pointerlockchange', () => {
  // When lock releases (ESC, menu open, alt-tab, etc.) make sure the
  // pause menu is showing — otherwise the player is stuck with cursor
  // visible mid-game with nothing to click. Skip when the game hasn't
  // started yet (main menu already showing).
  if (document.pointerLockElement !== renderer.domElement
      && gameStarted && !menu.isOpen()
      && !hud?.isWorldMapOpen?.()) {
    menu.showPause();
  }
});

async function loadVillage() {
  try {
    // Demo 1 backdrop mode: load the pre-baked Magic Pig Games "Medieval
    // Environment - Demo 1" village as a single GLB instead of placing the
    // 138 individual prefabs from villageLayout.js. The backdrop is centered
    // at world origin with X∈[-178,178] and Z∈[-161,161]. Spawn Wren south of
    // the village (positive Z = south in this project) facing north.
    // Phase 2 follow-up: derive collision from backdrop meshes; overlay
    // enterable building prefabs (mill, inn, chapel) at matching coordinates.
    const village = new Village({
      scene,
      terrainHeightAt,
      backdrop: {
        // demo1_village_with_terrain_min.glb is the second-pass export that
        // includes the baked TerrainMesh (Unity Terrain → mesh via
        // tools/clean_demo1_with_terrain.py). With the actual hill in the
        // scene, buildings sit naturally on it instead of floating/sinking.
        path: '/world/demo3/demo1_village_with_terrain_v2.glb',
        offset: { x: 0, y: 0, z: 0 }
      },
      // Spawn point picked from a 10m density grid: open ground at the
      // south-west outside corner of the village wall. Facing east-ish
      // (≈ -π/2) so the wall + gate are dead ahead and Wren walks toward
      // the town on initial movement.
      spawn: { x: 10, z: -20, facing: -Math.PI / 2 }
    });
    const { spawn, colliders } = await village.load();

    worldState.colliders = colliders;
    worldState.spawn = spawn;

    // terrainHeightAt() reads worldState.village to raycast against the
    // backdrop's TerrainMesh. Setting it after load() resolves the chicken-
    // and-egg: the FallbackGround was the placeholder ground until now, the
    // Unity hill takes over for any (x, z) inside its footprint.
    worldState.village = village;

    // Compute the village's world-space footprint from the backdrop scene.
    // Foliage uses this to exclude trees/shrubs/rocks from inside the
    // village, and to center its forest ring around the actual town
    // (rather than world origin, which the village no longer sits on).
    // Compute villageBounds from the loaded backdrop AND override the
    // hardcoded spawn so Wren lands at the actual village center (the
    // backdrop is loaded at its raw Unity coords, which aren't (0, 0)).
    let villageBounds = null;
    if (village.backdropScene) {
      village.backdropScene.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(village.terrainMesh || village.backdropScene);
      if (!box.isEmpty()) {
        villageBounds = {
          minX: box.min.x, maxX: box.max.x,
          minZ: box.min.z, maxZ: box.max.z,
          centerX: (box.min.x + box.max.x) / 2,
          centerZ: (box.min.z + box.max.z) / 2,
          halfX: (box.max.x - box.min.x) / 2,
          halfZ: (box.max.z - box.min.z) / 2
        };
        console.log('[loadVillage] foliage exclusion bounds', villageBounds);

        // Spawn in an open square in the town centre. The bbox-centre that
        // used to be picked here lands in the river to the north; the
        // building-density centroid (-261.6, 157.7) lands inside an inn.
        // (-256, 138) is the closest cell to the centroid with ≥7m
        // clearance from any wall — needed so the third-person camera
        // doesn't clip through buildings on spawn. Y comes from the
        // TerrainMesh raycast in spawnWren().
        worldState.spawn = { x: -256, z: 138, facing: 0 };
        console.log('[loadVillage] spawn relocated to town centre', worldState.spawn);
      }
    }

    await loadWren();
    spawnWren();
    loadBram().catch((err) => console.warn('[loadBram] failed', err));
    loadMarra().catch((err) => console.warn('[loadMarra] failed', err));
    foliage = await createFoliage({
      scene,
      terrainHeightAt,
      colliders,
      villageBounds
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
      },
      // Re-grab the cursor whenever the map closes — by button click,
      // Escape, M, or backdrop click — so the player can move the camera
      // immediately without re-clicking the canvas.
      onMapClose: () => {
        keys.clear();
        tryReacquirePointerLock();
        if (dialog.isOpen()) dialog.resume();
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
    _resolveVillageReady();
  } catch (error) {
    console.error(error);
    status.innerHTML = `
      <div class="eyebrow">Hollowfen Prototype</div>
      <h1>Load failed</h1>
      <p>${String(error.message || error)}</p>
    `;
    _resolveVillageReady();  // unblock the loading overlay even on failure
  }
}

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(clock.getDelta(), 0.05);
  // Game logic only runs after the player has clicked Begin and the menu
  // is closed. The renderer keeps drawing so the world is visible behind
  // the title screen / pause overlay.
  if (gameStarted && !menu.isOpen() && !hud?.isWorldMapOpen?.()) {
    updatePlayer(dt);
    updateCamera(dt);
    updateVillagers(dt);
  } else {
    interactPrompt.style.display = 'none';
  }
  // Reuse a single scratch Vector3 for the cloud focus when the player isn't
  // ready yet — was allocating per frame during the loading screen.
  const focus = player.ready ? player.root.position : _focusFallback;
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

async function loadVillagerFBX(manifestUrl, name) {
  const manifest = await fetch(manifestUrl, { cache: 'no-store' }).then((res) => res.json());
  const loader = new FBXLoader();
  const model = await loader.loadAsync(manifest.active);
  normalizeMixamoObjectNames(model);

  const targetHeight = manifest.height || 1.7;
  const box = new THREE.Box3().setFromObject(model);
  const size = box.getSize(new THREE.Vector3());
  model.scale.setScalar(size.y > 0 ? targetHeight / size.y : 0.01);
  const scaledBox = new THREE.Box3().setFromObject(model);
  model.position.y -= scaledBox.min.y;

  model.traverse((child) => {
    if (!child.isMesh) return;
    child.castShadow = true;
    child.receiveShadow = true;
    // Three.js frustum-culls SkinnedMeshes against their REST pose bounds,
    // not their actual skinned bounds. With aggressive cinematic camera
    // angles the rest-pose sphere can sit outside the frustum even though
    // the rendered character is on screen — Bram disappeared from
    // over-shoulder shots until this was disabled.
    if (child.isSkinnedMesh) child.frustumCulled = false;
    const materials = Array.isArray(child.material) ? child.material : [child.material];
    const converted = materials.map((mat) => {
      if (!mat) return mat;
      if (mat.map) mat.map.colorSpace = THREE.SRGBColorSpace;
      if (mat.isMeshStandardMaterial) {
        mat.roughness = Math.max(mat.roughness ?? 0.85, 0.86);
        mat.metalness = Math.min(mat.metalness ?? 0, 0.03);
        mat.envMapIntensity = 1.1;
        return mat;
      }
      const next = new THREE.MeshStandardMaterial({
        color: mat.color ? mat.color.clone() : new THREE.Color(0xc8b89e),
        map: mat.map || null,
        normalMap: mat.normalMap || null,
        roughness: 0.88,
        metalness: 0,
        transparent: mat.transparent || false,
        opacity: mat.opacity ?? 1
      });
      if (next.map) next.map.colorSpace = THREE.SRGBColorSpace;
      return next;
    });
    child.material = Array.isArray(child.material) ? converted : converted[0];
  });

  const root = new THREE.Group();
  root.name = name;
  root.add(model);
  const pos = manifest.position || { x: 0, z: 0 };
  // playerGroundAt raycasts the full backdrop (terrain + porches + decks),
  // so a villager standing on the inn's doorstep lands on the porch top, not
  // the terrain underneath. terrainHeightAt would have left them floating.
  const groundY = playerGroundAt(pos.x, pos.z, 100);
  root.position.set(pos.x, groundY, pos.z);
  root.rotation.y = manifest.facing ?? 0;
  scene.add(root);

  const villager = {
    root,
    model,
    mixer: null,
    actions: {},
    currentAction: null,
    oneShotAction: null,
    previousLoopName: null,
    homeFacing: manifest.facing ?? 0
  };

  if (manifest.animations) {
    villager.mixer = new THREE.AnimationMixer(model);
    villager.mixer.addEventListener('finished', (event) => {
      if (villager.oneShotAction && event.action === villager.oneShotAction) {
        villager.oneShotAction = null;
        const fallback = villager.previousLoopName || 'idle';
        if (villager.actions[fallback]) {
          playVillagerAction(villager, fallback, 0.25);
        }
      }
    });

    for (const [animName, config] of Object.entries(manifest.animations)) {
      try {
        const fbx = await loader.loadAsync(config.path);
        normalizeMixamoObjectNames(fbx);
        const clip = selectClip(fbx.animations, config.clip);
        if (!clip) continue;
        clip.name = animName;
        sanitizeClipTracks(clip);
        // Villagers stay rooted — strip translation from any clip with root motion.
        stripHorizontalRootMotion(clip);
        if (clip.tracks.length === 0) continue;
        const action = villager.mixer.clipAction(clip);
        action.userData = { options: config };
        villager.actions[animName] = action;
      } catch (err) {
        console.warn(`[loadVillagerFBX:${name}] failed to load ${animName}`, err);
      }
    }

    if (villager.actions.idle) {
      playVillagerAction(villager, 'idle', 0);
      // Mixamo idle clips often raise the Hips above the TPose's rest
      // height, which leaves the character floating. Find the lowest foot
      // bone in the animated pose and shift the model so that bone's world
      // Y lands on root.position.y. Using a bone (rather than Box3) avoids
      // Three.js's known issue where setFromObject on a SkinnedMesh uses
      // the rest-pose geometry bounds — which wildly mis-reports the
      // actual rendered Y in many rigs.
      villager.mixer.update(0.001);
      model.updateMatrixWorld(true);
      let lowestFootY = Infinity;
      const _bonePos = new THREE.Vector3();
      model.traverse((child) => {
        if (!child.isBone) return;
        const name = child.name.toLowerCase();
        if (!name.includes('foot') && !name.includes('toe')) return;
        child.getWorldPosition(_bonePos);
        if (_bonePos.y < lowestFootY) lowestFootY = _bonePos.y;
      });
      if (lowestFootY !== Infinity) {
        const correction = lowestFootY - root.position.y;
        // Sanity clamp — never move the model more than 1m. Anything bigger
        // is a measurement error, not a real float.
        if (Math.abs(correction) > 0.001 && Math.abs(correction) < 1.0) {
          model.position.y -= correction;
        }
      }
    }
  }

  return villager;
}

function playVillagerAction(villager, name, fade = 0.22) {
  if (!villager?.mixer) return;
  const action = villager.actions[name];
  if (!action || villager.currentAction === action) return;
  action.enabled = true;
  action.paused = false;
  action.reset().setLoop(THREE.LoopRepeat, Infinity).setEffectiveTimeScale(1).fadeIn(fade).play();
  if (villager.currentAction) villager.currentAction.fadeOut(fade);
  villager.currentAction = action;
  villager.oneShotAction = null;
  villager.previousLoopName = name;
}

function playVillagerOneShot(villager, name) {
  if (!villager?.mixer) return;
  const action = villager.actions[name];
  if (!action) return;
  if (villager.currentAction) villager.currentAction.fadeOut(0.18);
  action.reset()
    .setLoop(THREE.LoopOnce, 1)
    .setEffectiveTimeScale(1)
    .fadeIn(0.18)
    .play();
  action.clampWhenFinished = false;
  villager.currentAction = action;
  villager.oneShotAction = action;
}

async function loadBram() {
  worldState.bram = await loadVillagerFBX('/models/bram/manifest.json', 'Bram');
}

async function loadMarra() {
  worldState.marra = await loadVillagerFBX('/models/marra/manifest.json', 'Marra');
}

// How close Wren has to be before a villager turns to look at her. Wider than
// INTERACT_RANGE so the head/torso swing finishes before the prompt appears.
const VILLAGER_LOOK_RANGE = 6.0;

function updateVillagerFacing(villager, dt) {
  if (!villager?.root || !player.ready) return;
  const dx = player.root.position.x - villager.root.position.x;
  const dz = player.root.position.z - villager.root.position.z;
  const distSq = dx * dx + dz * dz;
  // atan2(x, z) matches the convention Wren uses for player.facing — yaw 0
  // points down +Z, increasing CCW. Mixamo characters import facing +Z, so
  // root.rotation.y aligns directly with this convention.
  const targetFacing = distSq < VILLAGER_LOOK_RANGE * VILLAGER_LOOK_RANGE
    ? Math.atan2(dx, dz)
    : villager.homeFacing;
  const delta = wrapAngle(targetFacing - villager.root.rotation.y);
  villager.root.rotation.y += delta * Math.min(1, dt * 6);
}

function updateVillagers(dt) {
  worldState.bram?.mixer?.update(dt);
  worldState.marra?.mixer?.update(dt);
  updateVillagerFacing(worldState.bram, dt);
  updateVillagerFacing(worldState.marra, dt);

  const bram = worldState.bram;
  if (!bram?.mixer || !player.ready) {
    interactPrompt.style.display = 'none';
    interactState.bramInRange = false;
    return;
  }

  const dx = player.root.position.x - bram.root.position.x;
  const dz = player.root.position.z - bram.root.position.z;
  const distSq = dx * dx + dz * dz;
  const inRange = distSq < INTERACT_RANGE * INTERACT_RANGE;

  if (inRange) {
    interactState.bramInRange = true;
    if (!dialog.isOpen() && !isNarrationActive()) {
      // First-time approach (Homecoming Scene 1): auto-trigger the
      // recognition dialog so the player doesn't have to press E. Per
      // story.md this scene "can be mostly on rails or lightly guided."
      // After Bram has been met once, future approaches show the prompt.
      if (!npcState.bramTalkedRecognition) {
        dialog.start(pickDialogForBram(npcState));
        interactPrompt.style.display = 'none';
      } else {
        interactPrompt.innerHTML = '<b>[E]</b> Talk to Bram';
        interactPrompt.style.display = 'block';
      }
    } else {
      interactPrompt.style.display = 'none';
    }
  } else {
    if (interactState.bramInRange) {
      interactState.bramInRange = false;
      // Wren stepped away — Bram returns to idle, but only if no dialog is
      // active. Dialog close has its own idle trigger.
      if (!dialog.isOpen()) playVillagerAction(bram, 'idle', 0.4);
    }
    interactPrompt.style.display = 'none';
  }
}

function getPlayerSnapshot() {
  if (!player.ready) return null;
  return {
    position: {
      x: roundCoord(player.root.position.x),
      y: roundCoord(player.root.position.y),
      z: roundCoord(player.root.position.z)
    },
    facing: roundCoord(player.facing),
    camera: {
      yaw: roundCoord(cameraState.yaw),
      pitch: roundCoord(cameraState.pitch)
    }
  };
}

function renderLocationPins(pins = []) {
  locationPinGroup.clear();
  for (const pin of pins) {
    const marker = createLocationPinMarker(pin);
    if (marker) locationPinGroup.add(marker);
  }
}

function createLocationPinMarker(pin) {
  if (!pin?.position) return null;
  const group = new THREE.Group();
  group.name = `LocationPin:${pin.id}`;
  group.position.set(pin.position.x, pin.position.y + 0.12, pin.position.z);

  const color = colorForLocationType(pin.type);
  const pole = new THREE.Mesh(
    new THREE.CylinderGeometry(0.045, 0.045, 2.2, 10),
    new THREE.MeshBasicMaterial({ color })
  );
  pole.position.y = 1.1;
  group.add(pole);

  const head = new THREE.Mesh(
    new THREE.SphereGeometry(0.22, 16, 12),
    new THREE.MeshBasicMaterial({ color })
  );
  head.position.y = 2.25;
  group.add(head);

  const label = createTextSprite(pin.name || pin.id, color);
  label.position.set(0, 2.72, 0);
  group.add(label);

  return group;
}

function createTextSprite(text, color) {
  const canvas = document.createElement('canvas');
  canvas.width = 512;
  canvas.height = 128;
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = 'rgba(20, 14, 10, 0.78)';
  roundRect(ctx, 12, 20, 488, 76, 16);
  ctx.fill();
  ctx.strokeStyle = `#${color.toString(16).padStart(6, '0')}`;
  ctx.lineWidth = 4;
  ctx.stroke();
  ctx.fillStyle = '#fff8eb';
  ctx.font = '700 30px Georgia, serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, 256, 58, 450);

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: false }));
  sprite.scale.set(4.6, 1.15, 1);
  sprite.renderOrder = 20;
  return sprite;
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function colorForLocationType(type) {
  const colors = {
    home: 0xd9bd6d,
    inn: 0xd28a4b,
    npc: 0x90b8e8,
    chapel: 0xc9c0ee,
    garden: 0x7fb56b,
    forage: 0x8fcf8b,
    trade: 0xe0cf7a,
    cottage: 0xcaa17a,
    river: 0x82bdd8,
    endgame: 0xd46a52,
    lore: 0xb18ad8,
    manor: 0xb8a07c,
    event: 0xe7b968
  };
  return colors[type] || 0xffffff;
}

function roundCoord(value) {
  return Number(value.toFixed(3));
}

function updatePlayer(dt) {
  if (!player.ready) return;

  // Freeze movement during cinematic dialog OR scene narration — only the
  // mixer + facing lerp tick. Wren slowly rotates to face Bram during
  // dialog (target set in onLineChange).
  if (dialog.isOpen() || isNarrationActive()) {
    if (player.dialogTargetFacing != null) {
      const delta = wrapAngle(player.dialogTargetFacing - player.facing);
      player.facing += delta * Math.min(1, dt * 5);
      player.root.rotation.y = player.facing;
    }
    playAction('idle');
    player.mixer?.update(dt);
    return;
  }

  const moveX = (keys.has('KeyD') || keys.has('ArrowRight') ? 1 : 0) - (keys.has('KeyA') || keys.has('ArrowLeft') ? 1 : 0);
  const moveZ = (keys.has('KeyW') || keys.has('ArrowUp') ? 1 : 0) - (keys.has('KeyS') || keys.has('ArrowDown') ? 1 : 0);
  const moving = moveX !== 0 || moveZ !== 0;
  const running = keys.has('ShiftLeft') || keys.has('ShiftRight');
  const speed = (running ? RUN_SPEED : WALK_SPEED) * dev.speedMultiplier;

  if (dev.fly) {
    updateFlyPlayer(dt, moveX, moveZ, speed);
    player.mixer?.update(dt);
    return;
  }

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

  // playerGroundAt raycasts the whole village backdrop (terrain + buildings
  // + stairs), so Wren naturally rises onto stair tops, porches, decks.
  const ground = playerGroundAt(player.root.position.x, player.root.position.z, player.root.position.y);
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

function updateFlyPlayer(dt, moveX, moveZ, speed) {
  const moveY =
    (keys.has('Space') || keys.has('KeyE') ? 1 : 0) -
    (keys.has('KeyQ') || keys.has('KeyC') ? 1 : 0);
  const moving = moveX !== 0 || moveZ !== 0 || moveY !== 0;
  const flySpeed = speed * 1.8;

  if (moving) {
    const rightX = Math.cos(cameraState.yaw);
    const rightZ = -Math.sin(cameraState.yaw);
    const forwardX = -Math.sin(cameraState.yaw);
    const forwardZ = -Math.cos(cameraState.yaw);
    const horizontalLength = Math.hypot(moveX, moveZ) || 1;
    const x = moveX / horizontalLength;
    const z = moveZ / horizontalLength;
    const worldX = rightX * x + forwardX * z;
    const worldZ = rightZ * x + forwardZ * z;
    if (moveX !== 0 || moveZ !== 0) {
      player.root.position.x += worldX * flySpeed * dt;
      player.root.position.z += worldZ * flySpeed * dt;
      const targetFacing = Math.atan2(worldX, worldZ);
      player.facing += wrapAngle(targetFacing - player.facing) * Math.min(1, dt * 12);
      player.root.rotation.y = player.facing;
    }
    player.root.position.y += moveY * flySpeed * dt;
    player.root.position.y = Math.max(terrainHeightAt(player.root.position.x, player.root.position.z) + 0.2, player.root.position.y);
  }

  player.verticalVelocity = 0;
  player.grounded = true;
  if (player.oneShotName !== 'jump') {
    if (!moving) playAction('idle');
    else playAction('run');
  }
}

// Scratch vectors reused per frame so updateCamera() doesn't allocate three
// Vector3s every tick (was noticeable as GC pressure at 60+ fps).
const _camTargetOffset = new THREE.Vector3(0, 1.35, 0);
const _camTarget = new THREE.Vector3();
const _camDesired = new THREE.Vector3();
function updateCamera(dt) {
  if (!player.ready) return;

  // Cinematic mode — side-angle two-shot framed on the current speaker.
  // The lerp is intentionally slow (~1.2s settle time) so cuts read as a
  // smooth dolly rather than a snap. setCinematicShotForSpeaker updates
  // the targets each line change; the smoothed values catch up here.
  if (cinematicCam.active) {
    const cinAlpha = Math.min(1, dt * 1.7);
    cinematicCam.smoothPos.lerp(cinematicCam.position, cinAlpha);
    cinematicCam.smoothLookAt.lerp(cinematicCam.lookAt, cinAlpha);
    camera.position.copy(cinematicCam.smoothPos);
    camera.lookAt(cinematicCam.smoothLookAt);
    return;
  }

  // Zoom in when Wren is near a villager. The lerp is independent from
  // the camera position lerp so the dolly feels intentional.
  const targetDistance = interactState.bramInRange
    ? CAMERA_DISTANCE_DIALOG
    : CAMERA_DISTANCE_DEFAULT;
  cameraDistance += (targetDistance - cameraDistance) * Math.min(1, dt * 3.5);
  _camTarget.copy(player.root.position).add(_camTargetOffset);
  const horizontalDistance = Math.cos(cameraState.pitch) * cameraDistance;
  _camDesired.set(
    _camTarget.x + Math.sin(cameraState.yaw) * horizontalDistance,
    _camTarget.y + Math.sin(cameraState.pitch) * cameraDistance + CAMERA_HEIGHT_OFFSET,
    _camTarget.z + Math.cos(cameraState.yaw) * horizontalDistance
  );
  const alpha = dt >= 1 ? 1 : CAMERA_LERP;
  camera.position.lerp(_camDesired, alpha);
  camera.lookAt(_camTarget);
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

// Approximate Wren's standing head height. Anything whose bottom is above
// this clearance is treated as overhead (overhang, archway, second-story
// floor) — Wren walks beneath without colliding.
const PLAYER_HEAD_CLEARANCE = 1.9;
// Max obstacle height Wren can step over without being blocked. Stair risers
// (~0.2m), doorsteps (~0.1m), low ledges (~0.4m) all qualify. Anything taller
// is a real wall.
const PLAYER_STEP_HEIGHT = 0.6;

function resolveMovement(x, z) {
  // Use the player's current Y as the raycast hint so we don't pick up
  // a roof above her as the ground level near the target position.
  const currentY = player.root?.position?.y ?? 100;
  const ground = playerGroundAt(x, z, currentY);
  const head = ground + PLAYER_HEAD_CLEARANCE;
  const stepThreshold = ground + PLAYER_STEP_HEIGHT;
  let resolvedX = x;
  let resolvedZ = z;
  for (const collider of worldState.colliders) {
    if (ground >= collider.top - 0.1) continue;
    // Walk-under: skip the collider if its bottom is above Wren's head
    // (with a tiny margin so brushing is forgiven).
    if (collider.bottom !== undefined && collider.bottom > head + 0.1) continue;
    // Step-up: skip the collider if its top is within Wren's step height
    // above her current ground (stair risers, low doorsteps, small ledges).
    if (collider.top <= stepThreshold) continue;
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
  const head = ground + PLAYER_HEAD_CLEARANCE;
  const stepThreshold = ground + PLAYER_STEP_HEIGHT;
  for (const collider of worldState.colliders) {
    if (ground >= collider.top - 0.1) continue;
    if (collider.bottom !== undefined && collider.bottom > head + 0.1) continue;
    if (collider.top <= stepThreshold) continue;
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

// Returns the TERRAIN ground Y at world (x, z) — raycasts the village
// backdrop's TerrainMesh only (the Unity hill). Use for foliage placement
// and other "natural ground" queries that should ignore buildings/stairs.
const FALLBACK_GROUND_Y = -0.05;
function terrainHeightAt(x, z) {
  if (worldState.village) {
    const y = worldState.village.getBackdropHeightAt(x, z);
    if (y !== null) return y;
  }
  return FALLBACK_GROUND_Y;
}

// Returns the STANDABLE Y for the player at world (x, z) — raycasts the
// entire backdrop scene (terrain + buildings + stairs + decks). The ray
// starts just above the player's current Y so we don't accidentally
// teleport her onto roofs she's standing under. This is what makes
// stairs / porches / ramps work natively.
function playerGroundAt(x, z, currentY) {
  if (worldState.village) {
    const fromY = (currentY ?? 100) + 2;
    const y = worldState.village.getStandableHeightAt(x, z, fromY);
    if (y !== null) return y;
  }
  return FALLBACK_GROUND_Y;
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
