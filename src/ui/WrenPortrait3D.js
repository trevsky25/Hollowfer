import * as THREE from 'three';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';

// A self-contained 3D portrait of Wren for the menu's Wren tab.
// Owns its own renderer, scene, camera, and animation loop — completely
// isolated from the main game scene. Drag-to-rotate, auto-rotate when idle.
// Call dispose() when the tab is unmounted to free GPU resources.

const PORTRAIT_HEIGHT = 1.78; // metres — same proportions as the gameplay model

export class WrenPortrait3D {
  constructor(container) {
    this.container = container;
    this.disposed = false;

    // Container can be 0×0 if we mount before layout. Use a sane fallback and
    // resize as soon as ResizeObserver tells us the real size.
    const w = Math.max(container.clientWidth, 320);
    const h = Math.max(container.clientHeight, 480);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(w, h);
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.35;
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.domElement.style.cursor = 'grab';
    container.appendChild(this.renderer.domElement);

    this.scene = new THREE.Scene();

    this.camera = new THREE.PerspectiveCamera(28, w / h, 0.05, 50);
    this.camera.position.set(0, PORTRAIT_HEIGHT * 0.55, 4.6);
    this.camera.lookAt(0, PORTRAIT_HEIGHT * 0.5, 0);

    const key = new THREE.DirectionalLight(0xfff4d6, 2.4);
    key.position.set(2.2, 3.4, 3);
    this.scene.add(key);

    const fill = new THREE.DirectionalLight(0xb6d2e8, 0.7);
    fill.position.set(-2.5, 1.5, 1.5);
    this.scene.add(fill);

    const back = new THREE.DirectionalLight(0xffd7a8, 0.9);
    back.position.set(0, 2.2, -3.5);
    this.scene.add(back);

    this.scene.add(new THREE.AmbientLight(0xfff4e0, 0.65));
    const hemi = new THREE.HemisphereLight(0xffe6c4, 0x32382f, 0.7);
    this.scene.add(hemi);

    this.pivot = new THREE.Group();
    this.scene.add(this.pivot);

    this.targetYaw = 0;
    this.currentYaw = 0;
    this.dragging = false;
    this.lastX = 0;
    this.lastInteractionAt = 0;
    this.mixer = null;
    this.action = null;

    this._bindInputs();
    this._loadModel();

    this.clock = new THREE.Clock();
    this._tick = this._tick.bind(this);
    this.frame = requestAnimationFrame(this._tick);

    // Track real container size — ResizeObserver fires once after layout
    // settles, then on every resize. Falls back to a window listener.
    this._onResize = () => this._resize();
    window.addEventListener('resize', this._onResize);
    if (typeof ResizeObserver !== 'undefined') {
      this._resizeObserver = new ResizeObserver(() => this._resize());
      this._resizeObserver.observe(container);
    }
    requestAnimationFrame(() => this._resize());
  }

  async _loadModel() {
    try {
      const manifest = await fetch('/models/wren/manifest.json', { cache: 'no-store' }).then((r) => r.json());
      const loader = new FBXLoader();
      const model = await loader.loadAsync(manifest.active);

      // Normalise to PORTRAIT_HEIGHT
      const box = new THREE.Box3().setFromObject(model);
      const size = box.getSize(new THREE.Vector3());
      const scale = size.y > 0 ? PORTRAIT_HEIGHT / size.y : 0.01;
      model.scale.setScalar(scale);
      const scaledBox = new THREE.Box3().setFromObject(model);
      model.position.y = -scaledBox.min.y;

      model.traverse((child) => {
        if (!child.isMesh) return;
        child.castShadow = false;
        child.receiveShadow = false;
        const materials = Array.isArray(child.material) ? child.material : [child.material];
        for (const mat of materials) {
          if (!mat) continue;
          if (mat.map) mat.map.colorSpace = THREE.SRGBColorSpace;
          if (mat.isMeshStandardMaterial) {
            // Lower roughness so colours catch more light; tone down baseline
            // grey by nudging colour saturation up a touch.
            mat.roughness = Math.min(mat.roughness ?? 0.85, 0.65);
            mat.metalness = 0;
            if (mat.color) {
              const hsl = { h: 0, s: 0, l: 0 };
              mat.color.getHSL(hsl);
              mat.color.setHSL(hsl.h, Math.min(1, hsl.s * 1.25), hsl.l);
            }
          }
        }
      });

      this.pivot.add(model);
      this.model = model;

      // Idle animation if available
      const idleConfig = manifest.animations?.idle;
      if (idleConfig) {
        try {
          const fbx = await loader.loadAsync(idleConfig.path);
          const clip = fbx.animations?.[0];
          if (clip) {
            this.mixer = new THREE.AnimationMixer(model);
            this.action = this.mixer.clipAction(clip);
            this.action.setLoop(THREE.LoopRepeat, Infinity).play();
          }
        } catch {
          // Idle animation optional; static pose is fine.
        }
      }
    } catch (err) {
      console.warn('[WrenPortrait3D] failed to load model:', err);
    }
  }

  _bindInputs() {
    const dom = this.renderer.domElement;
    dom.addEventListener('pointerdown', (e) => {
      this.dragging = true;
      this.lastX = e.clientX;
      this.lastInteractionAt = performance.now();
      dom.setPointerCapture?.(e.pointerId);
      dom.style.cursor = 'grabbing';
    });
    dom.addEventListener('pointermove', (e) => {
      if (!this.dragging) return;
      const dx = e.clientX - this.lastX;
      this.lastX = e.clientX;
      this.targetYaw -= dx * 0.012;
      this.lastInteractionAt = performance.now();
    });
    const release = (e) => {
      if (!this.dragging) return;
      this.dragging = false;
      dom.style.cursor = 'grab';
      dom.releasePointerCapture?.(e.pointerId);
    };
    dom.addEventListener('pointerup', release);
    dom.addEventListener('pointercancel', release);
    dom.addEventListener('pointerleave', release);
  }

  _resize() {
    if (this.disposed) return;
    const w = this.container.clientWidth || 360;
    const h = this.container.clientHeight || 540;
    if (w < 1 || h < 1) return;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  _tick() {
    if (this.disposed) return;
    this.frame = requestAnimationFrame(this._tick);
    const dt = Math.min(this.clock.getDelta(), 0.1);

    // Slow auto-rotate after 2 seconds without interaction
    const idleSince = performance.now() - this.lastInteractionAt;
    if (!this.dragging && idleSince > 2000) {
      this.targetYaw -= dt * 0.18;
    }

    this.currentYaw += (this.targetYaw - this.currentYaw) * Math.min(1, dt * 6);
    this.pivot.rotation.y = this.currentYaw;

    if (this.mixer) this.mixer.update(dt);
    this.renderer.render(this.scene, this.camera);
  }

  dispose() {
    this.disposed = true;
    if (this.frame) cancelAnimationFrame(this.frame);
    window.removeEventListener('resize', this._onResize);
    if (this._resizeObserver) this._resizeObserver.disconnect();
    this.renderer.dispose();
    if (this.renderer.domElement.parentNode) this.renderer.domElement.parentNode.removeChild(this.renderer.domElement);
    if (this.model) {
      this.model.traverse((child) => {
        if (child.isMesh) {
          child.geometry?.dispose?.();
          const mats = Array.isArray(child.material) ? child.material : [child.material];
          for (const m of mats) m?.dispose?.();
        }
      });
    }
  }
}
