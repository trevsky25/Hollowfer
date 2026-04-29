// Hollowfen in-game HUD: compass strip, mini-map, objective card.
//
// Shown only after the player presses Begin and only while no menu is open.
// Hooks into main.js's player + cameraState every frame to stay in sync.
// Waypoint list is data — story missions later will mutate it.

const WAYPOINT_COLORS = {
  primary: '#d9bd6d',     // current objective (gold)
  secondary: '#7ec38a',   // optional point of interest (green)
  story:    '#c489d0',    // story landmark (purple)
  npc:      '#7da7c8'     // NPC (blue)
};

export class HUD {
  constructor({ player, cameraState, colliders, onOpenMenu }) {
    this.player = player;
    this.cameraState = cameraState;
    this.colliders = colliders || [];
    this.onOpenMenu = onOpenMenu || (() => {});

    this.waypoints = [
      // Default world waypoints from the village layout. Each carries the
      // building name + colour-class. The first 'primary' is the current
      // objective; others appear as smaller dots/icons.
      { id: 'inn',     label: 'The Crooked Pintle',   x:  38, z:  -4, color: 'primary' },
      { id: 'mill',    label: "Wren's Mill",          x: -28, z:  30, color: 'story' },
      { id: 'chapel',  label: "Father Calden's Chapel", x: -10, z: -54, color: 'story' },
      { id: 'smithy',  label: "Joren's Smithy",       x:  60, z:  30, color: 'secondary' },
      { id: 'well',    label: 'The Well',             x:   0, z:   0, color: 'secondary' }
    ];
    this.activeWaypointId = 'inn';

    this.root = document.createElement('div');
    this.root.className = 'hud-root';
    this.root.style.display = 'none';
    document.body.appendChild(this.root);

    this._buildCompass();
    this._buildMinimap();
    this._buildObjective();
    this._buildHotbar();

    this._lastUpdate = 0;
  }

  show() { this.root.style.display = ''; }
  hide() { this.root.style.display = 'none'; }
  isVisible() { return this.root.style.display !== 'none'; }

  setActiveWaypoint(id) {
    if (this.waypoints.find((w) => w.id === id)) {
      this.activeWaypointId = id;
    }
  }

  // Called every frame; throttled to ~30Hz to keep CPU low
  update(dt) {
    if (!this.isVisible()) return;
    this._lastUpdate += dt;
    if (this._lastUpdate < 1 / 45) return;
    this._lastUpdate = 0;
    this._updateCompass();
    this._updateMinimap();
    this._updateObjective();
  }

  // ---------- COMPASS ----------
  _buildCompass() {
    this.compass = document.createElement('div');
    this.compass.className = 'hud-compass';
    this.compass.innerHTML = `
      <div class="hud-compass-strip" data-compass-strip></div>
      <div class="hud-compass-center"></div>
    `;
    this.root.appendChild(this.compass);
    this.compassStrip = this.compass.querySelector('[data-compass-strip]');
  }

  _updateCompass() {
    // Camera yaw drives the compass: yaw=0 → looking north (−Z).
    // bearing = (−yaw) mod 2π, so bearing=0 is N, π/2 is E, π is S.
    const yaw = this.cameraState.yaw;
    const bearing = wrapAngle(-yaw);

    const stripWidth = this.compassStrip.clientWidth || 480;
    const visibleArc = Math.PI;        // show ±90° so 180° total visible
    const pxPerRad = stripWidth / visibleArc;

    // Build/refresh markers — cardinal directions every 30° + waypoint icons
    if (!this._compassMarkers) this._buildCompassMarkers();
    this._positionCompassMarkers(bearing, pxPerRad, stripWidth);
  }

  _buildCompassMarkers() {
    // Cardinal points + half-cardinals
    const cardinals = [
      { angle: 0,            label: 'N', major: true },
      { angle: Math.PI / 4,  label: 'NE' },
      { angle: Math.PI / 2,  label: 'E', major: true },
      { angle: (3 * Math.PI) / 4, label: 'SE' },
      { angle: Math.PI,      label: 'S', major: true },
      { angle: -3 * Math.PI / 4, label: 'SW' },
      { angle: -Math.PI / 2, label: 'W', major: true },
      { angle: -Math.PI / 4, label: 'NW' }
    ];

    this._compassMarkers = [];
    for (const c of cardinals) {
      const m = document.createElement('div');
      m.className = `hud-compass-mark ${c.major ? 'major' : 'minor'}`;
      m.textContent = c.label;
      this.compassStrip.appendChild(m);
      this._compassMarkers.push({ kind: 'cardinal', el: m, angle: c.angle });
    }

    this._compassWaypoints = {};
    for (const w of this.waypoints) {
      const el = document.createElement('div');
      el.className = `hud-compass-waypoint ${w.color}`;
      el.innerHTML = `
        <div class="hud-compass-pin"></div>
        <div class="hud-compass-label"><span class="hud-compass-name">${w.label}</span><span class="hud-compass-dist">— m</span></div>
      `;
      this.compassStrip.appendChild(el);
      this._compassWaypoints[w.id] = el;
    }
  }

  _positionCompassMarkers(bearing, pxPerRad, stripWidth) {
    const halfWidth = stripWidth / 2;

    for (const m of this._compassMarkers) {
      let delta = wrapAngle(m.angle - bearing);
      const x = halfWidth + delta * pxPerRad;
      if (Math.abs(delta) > Math.PI / 2 + 0.05) {
        m.el.style.opacity = '0';
      } else {
        const fade = Math.max(0, 1 - Math.abs(delta) / (Math.PI / 2));
        m.el.style.opacity = String(0.35 + fade * 0.55);
        m.el.style.transform = `translateX(${x.toFixed(1)}px) translateX(-50%)`;
      }
    }

    const px = this.player.root.position.x;
    const pz = this.player.root.position.z;
    for (const w of this.waypoints) {
      const dx = w.x - px;
      const dz = w.z - pz;
      const wpBearing = Math.atan2(dx, -dz);
      const delta = wrapAngle(wpBearing - bearing);
      const dist = Math.hypot(dx, dz);

      const el = this._compassWaypoints[w.id];
      if (!el) continue;
      const distLabel = el.querySelector('.hud-compass-dist');
      if (distLabel) distLabel.textContent = `${Math.round(dist)} m`;

      const inFront = Math.abs(delta) < Math.PI / 2;
      if (!inFront) {
        el.style.opacity = '0';
        continue;
      }
      const x = halfWidth + delta * pxPerRad;
      const fade = Math.max(0.4, 1 - Math.abs(delta) / (Math.PI / 2));
      el.style.opacity = String(fade);
      el.style.transform = `translateX(${x.toFixed(1)}px) translateX(-50%)`;
      el.classList.toggle('active', w.id === this.activeWaypointId);
    }
  }

  // ---------- MINIMAP ----------
  _buildMinimap() {
    this.minimap = document.createElement('div');
    this.minimap.className = 'hud-minimap';
    this.minimap.innerHTML = `
      <canvas data-minimap width="200" height="200"></canvas>
      <div class="hud-minimap-label">Hollowfen</div>
    `;
    this.root.appendChild(this.minimap);
    this.minimapCanvas = this.minimap.querySelector('canvas');
    this.minimapCtx = this.minimapCanvas.getContext('2d');
    this.minimapScale = 1.4; // px per metre
  }

  // Bottom-centre hot bar with SVG icons. No emoji.
  _buildHotbar() {
    const items = [
      { id: 'story',    label: 'Story',    shortcut: 'T', icon: ICON_BOOK },
      { id: 'guide',    label: 'Guide',    shortcut: 'J', icon: ICON_LEAF },
      { id: 'wren',     label: 'Wren',     shortcut: 'C', icon: ICON_PERSON },
      { id: 'controls', label: 'Controls', shortcut: 'K', icon: ICON_KEYBOARD },
      { id: 'settings', label: 'Settings', shortcut: 'O', icon: ICON_GEAR }
    ];

    this.hotbar = document.createElement('div');
    this.hotbar.className = 'hud-hotbar';
    this.hotbar.innerHTML = items.map((it) => `
      <button class="hud-hotbtn" data-open="${it.id}" title="${it.label}${it.shortcut ? ` (${it.shortcut})` : ''}">
        <span class="hud-hotbtn-icon">${it.icon}</span>
        <span class="hud-hotbtn-label">
          ${it.label}${it.shortcut ? `<span class="hud-hotbtn-key">${it.shortcut}</span>` : ''}
        </span>
      </button>
    `).join('');
    this.root.appendChild(this.hotbar);

    this.hotbar.querySelectorAll('[data-open]').forEach((btn) => {
      btn.addEventListener('click', () => this.onOpenMenu(btn.dataset.open));
    });
  }

  _updateMinimap() {
    const ctx = this.minimapCtx;
    const size = this.minimapCanvas.width;
    const half = size / 2;
    const scale = this.minimapScale;
    const px = this.player.root.position.x;
    const pz = this.player.root.position.z;
    const yaw = this.cameraState.yaw;

    // Background — soft dark fill
    ctx.fillStyle = 'rgba(14, 20, 16, 0.85)';
    ctx.fillRect(0, 0, size, size);

    // Subtle ring for the visible radius
    ctx.strokeStyle = 'rgba(217, 189, 109, 0.18)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(half, half, half - 4, 0, Math.PI * 2);
    ctx.stroke();

    ctx.save();
    ctx.translate(half, half);
    // Rotate so the player's looking direction points up. We use yaw because
    // the camera yaw is what the user sees; -yaw matches our compass bearing.
    ctx.rotate(yaw);

    // Buildings — cap height filter (skip ground-level fences from cluttering)
    for (const c of this.colliders) {
      if (!c || c.top == null || c.top < 1.2) continue;
      const cx = (c.minX + c.maxX) / 2 - px;
      const cz = (c.minZ + c.maxZ) / 2 - pz;
      const w = (c.maxX - c.minX) * scale;
      const d = (c.maxZ - c.minZ) * scale;
      const x = cx * scale;
      const z = cz * scale;
      // World +Z is "south" — minimap up is forward, so we draw at (x, z) directly
      ctx.fillStyle = 'rgba(217, 189, 109, 0.42)';
      ctx.fillRect(x - w / 2, z - d / 2, w, d);
    }

    // Waypoints
    for (const w of this.waypoints) {
      const x = (w.x - px) * scale;
      const z = (w.z - pz) * scale;
      const r = w.id === this.activeWaypointId ? 5 : 3;
      ctx.fillStyle = WAYPOINT_COLORS[w.color] || '#fff';
      ctx.beginPath();
      ctx.arc(x, z, r, 0, Math.PI * 2);
      ctx.fill();
      if (w.id === this.activeWaypointId) {
        ctx.strokeStyle = WAYPOINT_COLORS.primary;
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.arc(x, z, r + 4, 0, Math.PI * 2);
        ctx.stroke();
      }
    }

    ctx.restore();

    // Player triangle dead centre, pointing up (forward direction)
    ctx.fillStyle = '#f5ecda';
    ctx.beginPath();
    ctx.moveTo(half, half - 7);
    ctx.lineTo(half - 5, half + 4);
    ctx.lineTo(half + 5, half + 4);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = '#1d2118';
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }

  // ---------- OBJECTIVE CARD ----------
  _buildObjective() {
    this.objective = document.createElement('div');
    this.objective.className = 'hud-objective';
    this.objective.innerHTML = `
      <div class="hud-objective-eyebrow">Current objective</div>
      <div class="hud-objective-title" data-obj-title>—</div>
      <div class="hud-objective-meta">
        <span data-obj-distance>—</span>
        <span data-obj-direction>—</span>
      </div>
    `;
    this.root.appendChild(this.objective);
  }

  _updateObjective() {
    const w = this.waypoints.find((wp) => wp.id === this.activeWaypointId);
    if (!w) return;
    const px = this.player.root.position.x;
    const pz = this.player.root.position.z;
    const dx = w.x - px;
    const dz = w.z - pz;
    const dist = Math.hypot(dx, dz);
    const wpBearing = wrapAngle(Math.atan2(dx, -dz));
    const cameraBearing = wrapAngle(-this.cameraState.yaw);
    const delta = wrapAngle(wpBearing - cameraBearing);

    const dirLabel = bearingToCompass(wpBearing);
    const turnHint = Math.abs(delta) < 0.15 ? 'ahead' : (delta > 0 ? `${Math.round((delta * 180 / Math.PI))}° right` : `${Math.round((-delta * 180 / Math.PI))}° left`);

    this.objective.querySelector('[data-obj-title]').textContent = w.label;
    this.objective.querySelector('[data-obj-distance]').textContent = `${Math.round(dist)} m`;
    this.objective.querySelector('[data-obj-direction]').textContent = `${dirLabel} · ${turnHint}`;
  }

}

// ---- helpers ----
function wrapAngle(a) {
  while (a > Math.PI) a -= Math.PI * 2;
  while (a < -Math.PI) a += Math.PI * 2;
  return a;
}

// Inline 24×24 line-art SVG icons. Stroke uses currentColor so the parent's
// `color` style drives them. Round caps + joins give them a hand-drawn feel.
const SVG_PROPS = 'viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"';

const ICON_BOOK = `<svg ${SVG_PROPS}>
  <path d="M3 5c2.4-1.2 5.4-1.2 8-0.2v14.4c-2.6-1-5.6-1-8 0.2V5z"/>
  <path d="M21 5c-2.4-1.2-5.4-1.2-8-0.2v14.4c2.6-1 5.6-1 8 0.2V5z"/>
  <path d="M11 4.8v14.4M13 4.8v14.4"/>
</svg>`;

const ICON_LEAF = `<svg ${SVG_PROPS}>
  <path d="M5 18C5 11 9 5 19 5c0 10-6 14-13 14-1.5 0-1.5-1-1-1z"/>
  <path d="M6 18 L14 9"/>
</svg>`;

const ICON_PERSON = `<svg ${SVG_PROPS}>
  <circle cx="12" cy="8.5" r="3.5"/>
  <path d="M5 20c0-3.5 3.2-6 7-6s7 2.5 7 6"/>
</svg>`;

const ICON_KEYBOARD = `<svg ${SVG_PROPS}>
  <rect x="2.5" y="6.5" width="19" height="11" rx="2"/>
  <path d="M6 10h0M9 10h0M12 10h0M15 10h0M18 10h0"/>
  <path d="M6 13.5h0M18 13.5h0"/>
  <path d="M8 16h8"/>
</svg>`;

const ICON_GEAR = `<svg ${SVG_PROPS}>
  <circle cx="12" cy="12" r="3"/>
  <path d="M12 2.5v2.5M12 19v2.5M21.5 12H19M5 12H2.5M18.7 5.3L17 7M7 17l-1.7 1.7M18.7 18.7L17 17M7 7L5.3 5.3"/>
</svg>`;

function bearingToCompass(bearing) {
  const b = wrapAngle(bearing);
  const dirs = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  const idx = ((Math.round(b / (Math.PI / 4)) % 8) + 8) % 8;
  return dirs[idx];
}
