import * as THREE from 'three';

const SKY_VERTEX = `
varying vec3 vWorldDirection;

void main() {
  vec4 worldPosition = modelMatrix * vec4(position, 1.0);
  vWorldDirection = normalize(worldPosition.xyz - cameraPosition);
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

const SKY_FRAGMENT = `
uniform vec3 uZenith;
uniform vec3 uHorizon;
uniform vec3 uGlow;
uniform float uDusk;
varying vec3 vWorldDirection;

void main() {
  float h = clamp(vWorldDirection.y * 0.5 + 0.5, 0.0, 1.0);
  float horizonBand = smoothstep(0.18, 0.46, h);
  vec3 color = mix(uHorizon, uZenith, horizonBand);
  float glow = pow(1.0 - abs(h - 0.44), 3.0) * uDusk;
  color = mix(color, uGlow, glow * 0.42);
  gl_FragColor = vec4(color, 1.0);
}
`;

export class StorySky {
  constructor(scene) {
    this.scene = scene;
    this.material = new THREE.ShaderMaterial({
      uniforms: {
        uZenith: { value: new THREE.Color(0x6e8694) },
        uHorizon: { value: new THREE.Color(0xc8a479) },
        uGlow: { value: new THREE.Color(0xe6a05a) },
        uDusk: { value: 0.62 }
      },
      vertexShader: SKY_VERTEX,
      fragmentShader: SKY_FRAGMENT,
      side: THREE.BackSide,
      depthWrite: false,
      depthTest: false,
      fog: false
    });
    this.mesh = new THREE.Mesh(new THREE.SphereGeometry(520, 32, 18), this.material);
    this.mesh.renderOrder = -100;
    scene.add(this.mesh);
  }

  update(playerPos) {
    this.mesh.position.copy(playerPos);
  }

  dispose() {
    this.scene.remove(this.mesh);
    this.mesh.geometry.dispose();
    this.material.dispose();
  }
}
