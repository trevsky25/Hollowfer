import { HOLLOWFEN_CANON_LOCATIONS } from '../data/HollowfenLocations.js';
import { STORY_CARDS } from '../data/StoryCards.js';

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

const STORY_CARD_BY_ID = new Map(STORY_CARDS.map((card) => [card.id, card]));

const LOCATION_STORY_CARD_IDS = {
  arrival_road: ['homecoming'],
  village_square: ['homecoming', 'first_festival'],
  village_well: ['homecoming', 'cottages_reopen'],
  crooked_pintle: ['crooked_pintle', 'marra_kitchen', 'hollin_arrives', 'first_festival'],
  bram_start: ['crooked_pintle', 'marra_kitchen'],
  tobins_mill: ['fathers_mill', 'hidden_journal', 'almy_doorway', 'sealed_letter', 'aldric_offer'],
  mill_doorway: ['fathers_mill', 'almy_doorway', 'sealed_letter'],
  edge_woods_path: ['first_forage'],
  first_forage_grove: ['first_forage'],
  almy_garden: ['almy_lessons'],
  jorens_forge: ['jorens_forge'],
  theo_wagon_stop: ['theo_trade', 'theo_capital_offer'],
  voss_tax_table: ['voss_first_visit', 'sealed_letter'],
  wenmar_cottage: ['voss_first_visit'],
  edda_cottage: ['edda_grandfather', 'edda_apprentice'],
  reopened_cottage_a: ['cottages_reopen'],
  reopened_cottage_b: ['cottages_reopen'],
  old_tavern_closed: ['cottages_reopen'],
  chapel: ['caldens_doubt', 'chapel_garden'],
  chapel_garden_gate: ['caldens_doubt', 'chapel_garden'],
  chapel_garden_beds: ['chapel_garden'],
  witch_cottage: ['witch_cottage', 'ending_witchs_path'],
  witchwell_spring: ['witch_cottage', 'ending_witchs_path'],
  dry_wend_bed: ['wend_truth'],
  wendlight_cluster: ['wend_truth'],
  festival_table: ['first_festival'],
  festival_musicians: ['first_festival'],
  pell_ledger_spot: ['cottages_reopen', 'first_festival'],
  market_stalls: ['theo_trade', 'first_festival'],
  traveler_arrival_spot: ['hollin_arrives'],
  upstream_clearcut: ['wend_source'],
  aldermark_patch: ['wend_source', 'meeting_aldric'],
  aldric_manor: ['meeting_aldric', 'ending_free_hollow', 'ending_lordly_patronage'],
};

export class HUD {
  constructor({ player, cameraState, colliders, onOpenMenu, onMapClose }) {
    this.player = player;
    this.cameraState = cameraState;
    this.colliders = colliders || [];
    this.onOpenMenu = onOpenMenu || (() => {});
    this.onMapClose = onMapClose || (() => {});

    this.locations = HOLLOWFEN_CANON_LOCATIONS.map((location) => ({
      ...location,
      storyCardIds: LOCATION_STORY_CARD_IDS[location.id] || [],
      color: waypointColorForLocation(location)
    }));
    this.waypoints = this.locations.map((location) => ({
      id: location.id,
      label: location.name,
      x: location.position.x,
      z: location.position.z,
      color: location.color
    }));
    this.activeWaypointId = 'crooked_pintle';
    this.selectedMapLocationId = 'village_square';

    this.root = document.createElement('div');
    this.root.className = 'hud-root';
    this.root.style.display = 'none';
    document.body.appendChild(this.root);

    this._buildCompass();
    this._buildMinimap();
    this._buildObjective();
    this._buildHotbar();
    this._buildWorldMap();

    this._lastUpdate = 0;
  }

  show() { this.root.style.display = ''; }
  hide() { this.root.style.display = 'none'; }
  isVisible() { return this.root.style.display !== 'none'; }
  isWorldMapOpen() { return Boolean(this.worldMap && !this.worldMap.hidden); }

  openMap(locationId = this.selectedMapLocationId) {
    if (!this.worldMap) return;
    this.selectedMapLocationId = locationId;
    this.worldMap.hidden = false;
    this._renderWorldMap();
  }

  closeMap() {
    if (this.worldMap) this.worldMap.hidden = true;
    try { this.onMapClose(); } catch {}
  }

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
      <button class="hud-minimap-open" type="button" data-open-world-map title="Open Hollowfen Map (M)">
        ${ICON_MAP}
      </button>
      <canvas data-minimap width="200" height="200"></canvas>
      <div class="hud-minimap-label">Hollowfen</div>
    `;
    this.root.appendChild(this.minimap);
    this.minimapCanvas = this.minimap.querySelector('canvas');
    this.minimapCtx = this.minimapCanvas.getContext('2d');
    this.minimapScale = 1.4; // px per metre
    this.minimap.querySelector('[data-open-world-map]').addEventListener('click', () => this.openMap());
  }

  // Bottom-centre hot bar with SVG icons. No emoji.
  _buildHotbar() {
    const items = [
      { id: 'map',      label: 'Map',      shortcut: 'M', icon: ICON_MAP },
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
      btn.addEventListener('click', () => {
        if (btn.dataset.open === 'map') {
          this.openMap();
        } else {
          this.onOpenMenu(btn.dataset.open);
        }
      });
    });
  }

  _buildWorldMap() {
    this.worldMap = document.createElement('div');
    this.worldMap.className = 'hud-world-map';
    this.worldMap.hidden = true;
    this.worldMap.innerHTML = `
      <section class="hud-world-map-panel" role="dialog" aria-modal="true" aria-label="Hollowfen map">
        <div class="hud-world-map-layout">
          <aside class="hud-world-map-side">
            <header class="hud-world-map-header">
              <div class="hud-world-map-title-block">
                <div class="hud-world-map-eyebrow">Pinned canon map · Surveyor's draft</div>
                <h2>Hollowfen</h2>
              </div>
              <button class="hud-world-map-close" type="button" data-close-world-map title="Close map">Close</button>
            </header>
            <div class="hud-world-map-legend" aria-hidden="true">
              <span><i class="legend-dot primary"></i>Objective</span>
              <span><i class="legend-dot secondary"></i>Village</span>
              <span><i class="legend-dot story"></i>Story</span>
              <span><i class="legend-dot npc"></i>People</span>
            </div>
            <div class="hud-world-map-info" data-map-info></div>
          </aside>
          <div class="hud-world-map-stage" data-map-stage>
            <div class="hud-world-map-vignette" aria-hidden="true"></div>
            <svg class="hud-world-map-art" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true" data-map-art></svg>
            <div class="hud-world-map-frame" aria-hidden="true"></div>
            <div class="hud-world-map-compass" aria-hidden="true">
              <svg viewBox="0 0 64 64">
                <circle cx="32" cy="32" r="28" class="compass-ring"/>
                <circle cx="32" cy="32" r="22" class="compass-ring-inner"/>
                <path d="M32 8 L36 32 L32 30 L28 32 Z" class="compass-needle-n"/>
                <path d="M32 56 L28 32 L32 34 L36 32 Z" class="compass-needle-s"/>
                <text x="32" y="6" text-anchor="middle" class="compass-letter">N</text>
                <text x="32" y="62" text-anchor="middle" class="compass-letter">S</text>
                <text x="60" y="34" text-anchor="middle" class="compass-letter small">E</text>
                <text x="4"  y="34" text-anchor="middle" class="compass-letter small">W</text>
              </svg>
            </div>
            <div class="hud-world-map-scale" aria-hidden="true">
              <div class="hud-world-map-scale-bar"></div>
              <div class="hud-world-map-scale-label">~50 paces</div>
            </div>
            <div class="hud-world-map-pins" data-world-map-pins></div>
            <div class="hud-world-map-player" data-world-map-player title="Wren"></div>
          </div>
        </div>
      </section>
    `;
    document.body.appendChild(this.worldMap);
    this.worldMapPins = this.worldMap.querySelector('[data-world-map-pins]');
    this.worldMapInfo = this.worldMap.querySelector('[data-map-info]');
    this.worldMapPlayer = this.worldMap.querySelector('[data-world-map-player]');
    this.worldMapArt = this.worldMap.querySelector('[data-map-art]');
    this.worldMap.querySelector('[data-close-world-map]').addEventListener('click', () => this.closeMap());
    this.worldMap.addEventListener('click', (event) => {
      if (event.target === this.worldMap) this.closeMap();
    });
    this._mapBounds = computeMapBounds(this.locations);
    this._renderMapArt();
  }

  // Generate the parchment-style cartographic art layer. Building footprints
  // come from the village backdrop colliders (so they reflect the actual
  // 3D scene), and roads/river/walls/woods anchor to canon location pins so
  // they always stay aligned to the rest of the map.
  _renderMapArt() {
    if (!this.worldMapArt || !this._mapBounds) return;
    const bounds = this._mapBounds;
    const locById = new Map(this.locations.map((l) => [l.id, l]));
    const pp = (x, z) => projectToMap({ x, z }, bounds);
    const pl = (id) => {
      const loc = locById.get(id);
      if (!loc) return { x: 50, y: 50 };
      return pp(loc.position.x, loc.position.z);
    };
    const fmt = (v) => v.toFixed(2);

    // Building rectangles from the actual GLB collider AABBs. Filter to
    // structural-height shapes so we skip fences, props, and tiny clutter,
    // and skip absurdly large boxes (some colliders span a whole quarter of
    // the map and would smear ink across half the page).
    const buildings = [];
    for (const c of this.colliders || []) {
      if (!c || c.top == null || c.top < 2.0) continue;
      const a = pp(c.minX, c.minZ);
      const b = pp(c.maxX, c.maxZ);
      const x = Math.min(a.x, b.x);
      const y = Math.min(a.y, b.y);
      const w = Math.abs(b.x - a.x);
      const h = Math.abs(b.y - a.y);
      if (w < 0.45 || h < 0.45) continue;
      if (w > 14 || h > 14) continue;
      buildings.push({ x, y, w, h, top: c.top });
    }
    // Sort tallest last so larger structures paint on top of small ones
    buildings.sort((a, b) => a.top - b.top);

    // Smooth Catmull-Rom-ish bezier path through a list of points.
    const smoothPath = (pts) => {
      if (pts.length < 2) return '';
      let d = `M${fmt(pts[0].x)} ${fmt(pts[0].y)}`;
      for (let i = 0; i < pts.length - 1; i++) {
        const p0 = pts[i - 1] || pts[i];
        const p1 = pts[i];
        const p2 = pts[i + 1];
        const p3 = pts[i + 2] || p2;
        const c1x = p1.x + (p2.x - p0.x) / 6;
        const c1y = p1.y + (p2.y - p0.y) / 6;
        const c2x = p2.x - (p3.x - p1.x) / 6;
        const c2y = p2.y - (p3.y - p1.y) / 6;
        d += ` C${fmt(c1x)} ${fmt(c1y)}, ${fmt(c2x)} ${fmt(c2y)}, ${fmt(p2.x)} ${fmt(p2.y)}`;
      }
      return d;
    };

    // Closed polygon (for wall/forest shapes) anchored to canon locations,
    // optionally inflated outward from the centroid so the line sits OUTSIDE
    // the buildings rather than running through them.
    const closedShape = (ids, inflate = 0) => {
      const pts = ids.map(pl).filter(Boolean);
      if (pts.length < 3) return '';
      const cx = pts.reduce((s, p) => s + p.x, 0) / pts.length;
      const cy = pts.reduce((s, p) => s + p.y, 0) / pts.length;
      const inflated = pts.map((p) => {
        const dx = p.x - cx;
        const dy = p.y - cy;
        const len = Math.hypot(dx, dy) || 1;
        return { x: p.x + (dx / len) * inflate, y: p.y + (dy / len) * inflate };
      });
      // Close the loop by repeating first point at end
      const loop = [...inflated, inflated[0]];
      return smoothPath(loop) + ' Z';
    };

    // ----- Cartographic features --------------------------------------------

    // Old Wood / Edge Woods — the forest blanket east + south of the village.
    // Bounded loosely by the forage/lore points so it always wraps the
    // outer-ring locations.
    const oldWoodPath = closedShape([
      'edge_woods_path',
      'first_forage_grove',
      'wendlight_cluster',
      'witchwell_spring',
      'witch_cottage',
      'almy_garden',
      'aldric_manor',
      'old_tavern_closed',
      'reopened_cottage_a',
      'dry_wend_bed',
      'aldermark_patch',
      'upstream_clearcut'
    ], 4.5);

    // Deep Wood pocket where the witch lives — denser cluster.
    const deepWoodPath = closedShape([
      'witch_cottage',
      'witchwell_spring',
      'wendlight_cluster'
    ], 3.5);

    // Village stone wall — irregular polygon enclosing the main built-up
    // cluster around the square. Pulls in the chapel grounds at the north.
    // Inflate generously so the line clearly encircles the buildings instead
    // of cutting through them.
    const wallPath = closedShape([
      'chapel_garden_beds',
      'chapel',
      'pell_ledger_spot',
      'traveler_arrival_spot',
      'bram_start',
      'theo_wagon_stop',
      'reopened_cottage_b',
      'edda_cottage',
      'reopened_cottage_a',
      'jorens_forge',
      'village_well',
      'tobins_mill',
      'wenmar_cottage',
      'mill_doorway'
    ], 6.0);

    // Roads. Hand-curated through the canon points so they read like a
    // village high-street + secondary lanes.
    const mainRoad = smoothPath([
      pl('arrival_road'),
      pl('reopened_cottage_a'),
      pl('reopened_cottage_b'),
      pl('theo_wagon_stop'),
      pl('crooked_pintle'),
      pl('village_square'),
      pl('mill_doorway'),
      pl('tobins_mill')
    ]);
    const chapelRoad = smoothPath([
      pl('village_square'),
      pl('voss_tax_table'),
      pl('pell_ledger_spot'),
      pl('chapel'),
      pl('chapel_garden_gate'),
      pl('chapel_garden_beds')
    ]);
    const woodsRoad = smoothPath([
      pl('village_square'),
      pl('jorens_forge'),
      pl('edge_woods_path'),
      pl('first_forage_grove')
    ]);
    const manorRoad = smoothPath([
      pl('arrival_road'),
      pl('almy_garden'),
      pl('aldric_manor')
    ]);
    const witchTrail = smoothPath([
      pl('edge_woods_path'),
      pl('dry_wend_bed'),
      pl('witch_cottage'),
      pl('witchwell_spring')
    ]);

    // The Wend — once the village's lifeblood, now a dry course running
    // roughly N→S past the village. Drawn as a broken/dashed channel.
    const riverPath = smoothPath([
      pp(-260, 40),
      pl('village_well'),
      pl('dry_wend_bed'),
      pl('wendlight_cluster'),
      pl('witchwell_spring'),
      pp(-200, 360)
    ]);

    // Mill pond / channel that fed Tobin's mill (now stagnant).
    const millChannel = smoothPath([
      pl('village_well'),
      pl('tobins_mill'),
      pl('wenmar_cottage')
    ]);

    // Special landmark glyphs.
    const well = pl('village_well');
    const chapelPos = pl('chapel');
    const gardenA = pl('chapel_garden_beds');
    const gardenB = pl('almy_garden');
    const mill = pl('tobins_mill');
    const inn = pl('crooked_pintle');
    const forge = pl('jorens_forge');
    const manor = pl('aldric_manor');
    const witch = pl('witch_cottage');
    const market = pl('market_stalls');
    const festival = pl('festival_table');

    // Tree dot scatter — small offsets from canon forage/lore points to give
    // the woods texture without occluding the labels.
    const treeSeeds = [
      'first_forage_grove', 'wendlight_cluster', 'aldermark_patch',
      'upstream_clearcut', 'witch_cottage', 'witchwell_spring',
      'edge_woods_path'
    ];
    const trees = [];
    let seed = 1;
    const rand = () => {
      seed = (seed * 9301 + 49297) % 233280;
      return seed / 233280;
    };
    for (const id of treeSeeds) {
      const c = pl(id);
      const count = 14;
      for (let i = 0; i < count; i++) {
        const r = 4 + rand() * 8;
        const a = rand() * Math.PI * 2;
        trees.push({ x: c.x + Math.cos(a) * r, y: c.y + Math.sin(a) * r, s: 0.5 + rand() * 0.6 });
      }
    }

    // ----- Build SVG -------------------------------------------------------

    // Render the raw collider AABBs as overlapping fills (no stroke per
    // rectangle). An SVG filter then computes a single dark outline around
    // the union of every overlapping shape — so internal wall-segment seams
    // disappear and what's left looks like a clean village block.
    let bldgD = '';
    let roofD = '';
    for (const b of buildings) {
      const roofH = Math.min(0.5, b.h * 0.34);
      bldgD += `M${fmt(b.x)} ${fmt(b.y)} h${fmt(b.w)} v${fmt(b.h)} h${fmt(-b.w)} Z `;
      roofD += `M${fmt(b.x)} ${fmt(b.y)} h${fmt(b.w)} v${fmt(roofH)} h${fmt(-b.w)} Z `;
    }
    const buildingRects = `
      <g class="map-bldg-group" filter="url(#bldgUnionOutline)">
        <path d="${bldgD}" class="map-bldg-base" fill-rule="nonzero"/>
        <path d="${roofD}" class="map-bldg-roof" fill-rule="nonzero"/>
      </g>`;

    const treesSvg = trees.map((t) => `
      <g class="map-tree" transform="translate(${fmt(t.x)} ${fmt(t.y)}) scale(${fmt(t.s)})">
        <circle cx="0" cy="0.4" r="1.05" class="map-tree-canopy"/>
        <circle cx="-0.55" cy="-0.1" r="0.85" class="map-tree-canopy"/>
        <circle cx="0.55" cy="-0.1" r="0.85" class="map-tree-canopy"/>
        <line x1="0" y1="0.7" x2="0" y2="1.3" class="map-tree-trunk"/>
      </g>`).join('');

    // Markers for special locations
    const markers = [
      // Well: stone ring with cross-shaft
      `<g class="map-mark map-well" transform="translate(${fmt(well.x)} ${fmt(well.y)})">
         <circle r="1.0" class="map-well-ring"/>
         <circle r="0.45" class="map-well-water"/>
         <line x1="-1.4" y1="0" x2="1.4" y2="0" class="map-well-bar"/>
       </g>`,
      // Chapel cross
      `<g class="map-mark map-chapel" transform="translate(${fmt(chapelPos.x)} ${fmt(chapelPos.y - 1.5)})">
         <line x1="0" y1="-1.5" x2="0" y2="1.5" class="map-cross"/>
         <line x1="-0.85" y1="-0.6" x2="0.85" y2="-0.6" class="map-cross"/>
       </g>`,
      // Garden bed glyphs (rows)
      ...[gardenA, gardenB].map((g) => `
        <g class="map-mark map-garden" transform="translate(${fmt(g.x)} ${fmt(g.y)})">
          <rect x="-2.2" y="-1.2" width="4.4" height="2.4" rx="0.2" class="map-garden-bed"/>
          <line x1="-2" y1="-0.4" x2="2" y2="-0.4" class="map-garden-row"/>
          <line x1="-2" y1="0.2"  x2="2" y2="0.2"  class="map-garden-row"/>
          <line x1="-2" y1="0.8"  x2="2" y2="0.8"  class="map-garden-row"/>
        </g>`).join(''),
      // Mill waterwheel hint
      `<g class="map-mark map-mill" transform="translate(${fmt(mill.x + 1.8)} ${fmt(mill.y)})">
         <circle r="1.2" class="map-mill-wheel"/>
         <line x1="-1.2" y1="0" x2="1.2" y2="0" class="map-mill-spoke"/>
         <line x1="0" y1="-1.2" x2="0" y2="1.2" class="map-mill-spoke"/>
         <line x1="-0.85" y1="-0.85" x2="0.85" y2="0.85" class="map-mill-spoke"/>
         <line x1="-0.85" y1="0.85" x2="0.85" y2="-0.85" class="map-mill-spoke"/>
       </g>`,
      // Inn signpost
      `<g class="map-mark map-inn" transform="translate(${fmt(inn.x)} ${fmt(inn.y - 2.4)})">
         <line x1="0" y1="0" x2="0" y2="2.0" class="map-sign-post"/>
         <rect x="-1.0" y="0.1" width="2.0" height="1.0" rx="0.15" class="map-sign-board"/>
       </g>`,
      // Forge anvil/flame
      `<g class="map-mark map-forge" transform="translate(${fmt(forge.x)} ${fmt(forge.y)})">
         <path d="M -1.2 0 L 1.2 0 L 0.8 0.8 L -0.8 0.8 Z" class="map-forge-anvil"/>
         <path d="M 0 -1.6 Q 0.6 -0.8 0 -0.4 Q -0.6 -0.8 0 -1.6 Z" class="map-forge-flame"/>
       </g>`,
      // Manor — larger keep silhouette
      `<g class="map-mark map-manor" transform="translate(${fmt(manor.x)} ${fmt(manor.y)})">
         <rect x="-2.2" y="-1.6" width="4.4" height="3.0" class="map-manor-body"/>
         <rect x="-2.6" y="-2.4" width="0.9" height="0.9" class="map-manor-tower"/>
         <rect x="1.7"  y="-2.4" width="0.9" height="0.9" class="map-manor-tower"/>
         <rect x="-0.5" y="-2.6" width="1.0" height="1.1" class="map-manor-tower"/>
       </g>`,
      // Witch's cottage — tilted hut
      `<g class="map-mark map-witch" transform="translate(${fmt(witch.x)} ${fmt(witch.y)}) rotate(-6)">
         <rect x="-1.4" y="-0.6" width="2.8" height="1.6" class="map-witch-body"/>
         <path d="M -1.6 -0.6 L 0 -2.0 L 1.6 -0.6 Z" class="map-witch-roof"/>
       </g>`,
      // Market stalls — three small awnings in a row
      `<g class="map-mark map-market" transform="translate(${fmt(market.x)} ${fmt(market.y)})">
         <path d="M -2.4 0 L -1.8 -1.0 L -1.2 0 Z" class="map-stall"/>
         <path d="M -0.6 0 L 0    -1.0 L 0.6 0 Z" class="map-stall"/>
         <path d="M  1.2 0 L 1.8 -1.0 L 2.4 0 Z" class="map-stall"/>
       </g>`,
      // Festival table glyph
      `<g class="map-mark map-festival" transform="translate(${fmt(festival.x)} ${fmt(festival.y)})">
         <rect x="-1.6" y="-0.4" width="3.2" height="0.8" class="map-festival-table"/>
         <line x1="-1.4" y1="0.4" x2="-1.4" y2="1.0" class="map-festival-leg"/>
         <line x1="1.4"  y1="0.4" x2="1.4"  y2="1.0" class="map-festival-leg"/>
       </g>`
    ].join('');

    const svg = `
      <defs>
        <linearGradient id="mapParchment" x1="0" x2="0" y1="0" y2="1">
          <stop offset="0"   stop-color="#3a4a2c"/>
          <stop offset="0.5" stop-color="#46583a"/>
          <stop offset="1"   stop-color="#2c3a26"/>
        </linearGradient>
        <radialGradient id="mapInk" cx="50%" cy="48%" r="70%">
          <stop offset="0"   stop-color="#5b6d44" stop-opacity="0"/>
          <stop offset="0.7" stop-color="#1a2218" stop-opacity="0.18"/>
          <stop offset="1"   stop-color="#0d1410" stop-opacity="0.55"/>
        </radialGradient>
        <pattern id="mapWeave" width="2" height="2" patternUnits="userSpaceOnUse" patternTransform="rotate(35)">
          <line x1="0" y1="0" x2="0" y2="2" stroke="rgba(245,236,218,0.05)" stroke-width="0.18"/>
        </pattern>
        <pattern id="mapForestPattern" width="3" height="3" patternUnits="userSpaceOnUse" patternTransform="rotate(8)">
          <circle cx="1.5" cy="1.5" r="0.55" fill="rgba(56,86,52,0.55)"/>
          <circle cx="0.4" cy="0.4" r="0.32" fill="rgba(40,68,42,0.45)"/>
        </pattern>
        <pattern id="mapWallHatch" width="1.8" height="1.8" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
          <line x1="0" y1="0" x2="0" y2="1.8" stroke="rgba(217,189,109,0.20)" stroke-width="0.2"/>
        </pattern>
        <filter id="mapGrain" x="0" y="0" width="100%" height="100%">
          <feTurbulence type="fractalNoise" baseFrequency="2.4" numOctaves="2" stitchTiles="stitch"/>
          <feColorMatrix type="saturate" values="0"/>
          <feComponentTransfer><feFuncA type="table" tableValues="0 0.20"/></feComponentTransfer>
          <feComposite in2="SourceGraphic" operator="in"/>
        </filter>
        <!-- Outline-only-on-the-union filter for buildings. Dilates the
             source alpha, paints the dilation dark, then composites the
             original on top. The visible result: a clean dark perimeter
             around the merged shape, with no internal wall-seam strokes. -->
        <filter id="bldgUnionOutline" x="-2%" y="-2%" width="104%" height="104%">
          <feMorphology in="SourceAlpha" operator="dilate" radius="0.22" result="DILATED"/>
          <feFlood flood-color="#1a1108" result="OUTLINE_COLOR"/>
          <feComposite in="OUTLINE_COLOR" in2="DILATED" operator="in" result="OUTLINE"/>
          <feMerge>
            <feMergeNode in="OUTLINE"/>
            <feMergeNode in="SourceGraphic"/>
          </feMerge>
        </filter>
      </defs>

      <rect width="100" height="100" fill="url(#mapParchment)"/>
      <rect width="100" height="100" fill="url(#mapWeave)"/>
      <rect width="100" height="100" filter="url(#mapGrain)" opacity="0.7"/>

      <!-- Forest layer -->
      <g class="map-layer map-layer-forest">
        <path d="${oldWoodPath}" class="map-forest-fill"/>
        <path d="${oldWoodPath}" class="map-forest-pattern"/>
        <path d="${oldWoodPath}" class="map-forest-edge"/>
        <path d="${deepWoodPath}" class="map-forest-deep"/>
      </g>

      <!-- Dry river bed (under everything) -->
      <g class="map-layer map-layer-river-bed">
        <path d="${riverPath}" class="map-river-bed"/>
        <path d="${millChannel}" class="map-river-channel"/>
      </g>

      <!-- Wall fill + hatch (under buildings) -->
      <g class="map-layer map-layer-wall-fill">
        <path d="${wallPath}" class="map-wall-fill"/>
        <path d="${wallPath}" class="map-wall-hatch"/>
      </g>

      <!-- Roads (under buildings, drawn over wall fill) -->
      <g class="map-layer map-layer-roads">
        <path d="${mainRoad}" class="map-road-shadow"/>
        <path d="${chapelRoad}" class="map-road-shadow"/>
        <path d="${mainRoad}" class="map-road"/>
        <path d="${chapelRoad}" class="map-road"/>
        <path d="${woodsRoad}" class="map-path"/>
        <path d="${manorRoad}" class="map-path"/>
        <path d="${witchTrail}" class="map-trail"/>
      </g>

      <!-- Trees (drawn before buildings so trunks tuck behind) -->
      <g class="map-layer map-layer-trees">${treesSvg}</g>

      <!-- Building footprints (from village backdrop colliders) -->
      <g class="map-layer map-layer-buildings">${buildingRects}</g>

      <!-- Wall stroke + river dash (drawn ABOVE buildings) -->
      <g class="map-layer map-layer-overlays">
        <path d="${riverPath}" class="map-river-line"/>
        <path d="${wallPath}" class="map-wall-line"/>
      </g>

      <!-- Special landmarks -->
      <g class="map-layer map-layer-marks">${markers}</g>

      <!-- Vignette overlay -->
      <rect width="100" height="100" fill="url(#mapInk)" pointer-events="none"/>
    `;

    this.worldMapArt.innerHTML = svg;
  }

  _renderWorldMap() {
    if (!this.worldMapPins || !this.worldMapInfo) return;
    this.worldMapPins.innerHTML = '';
    const ALWAYS_LABEL = new Set([
      'village_square', 'crooked_pintle', 'tobins_mill', 'chapel',
      'jorens_forge', 'aldric_manor', 'witch_cottage', 'first_forage_grove'
    ]);
    for (const location of this.locations) {
      const pos = projectToMap(location.position, this._mapBounds);
      const btn = document.createElement('button');
      btn.type = 'button';
      const cls = [`hud-world-map-pin`, location.color];
      if (location.id === this.selectedMapLocationId) cls.push('selected');
      if (ALWAYS_LABEL.has(location.id)) cls.push('always-show');
      btn.className = cls.join(' ');
      btn.style.left = `${pos.x}%`;
      btn.style.top = `${pos.y}%`;
      btn.title = location.name;
      btn.innerHTML = `
        <span class="hud-world-map-pin-dot"></span>
        <span class="hud-world-map-pin-label">${escapeHtml(location.name)}</span>
      `;
      btn.addEventListener('click', () => {
        this.selectedMapLocationId = location.id;
        this.setActiveWaypoint(location.id);
        this._renderWorldMap();
      });
      this.worldMapPins.appendChild(btn);
    }

    const playerPos = this.player?.root?.position;
    if (playerPos && this.worldMapPlayer) {
      const projected = projectToMap(playerPos, this._mapBounds);
      this.worldMapPlayer.style.left = `${projected.x}%`;
      this.worldMapPlayer.style.top = `${projected.y}%`;
    }

    this._renderMapInfo();
  }

  _renderMapInfo() {
    const location = this.locations.find((loc) => loc.id === this.selectedMapLocationId) || this.locations[0];
    if (!location) return;
    const cards = location.storyCardIds.map((id) => STORY_CARD_BY_ID.get(id)).filter(Boolean);
    const distance = this.player?.root?.position
      ? Math.round(Math.hypot(location.position.x - this.player.root.position.x, location.position.z - this.player.root.position.z))
      : null;
    this.worldMapInfo.innerHTML = `
      <div class="hud-world-map-info-type">${escapeHtml(location.act || 'Hollowfen')} · ${escapeHtml(location.type)}</div>
      <h3>${escapeHtml(location.name)}</h3>
      <p>${escapeHtml(location.description)}</p>
      <dl class="hud-world-map-coords">
        <div><dt>Distance</dt><dd>${distance == null ? '—' : `${distance} m`}</dd></div>
        <div><dt>World X</dt><dd>${location.position.x.toFixed(1)}</dd></div>
        <div><dt>World Z</dt><dd>${location.position.z.toFixed(1)}</dd></div>
      </dl>
      <div class="hud-world-map-info-actions">
        <button type="button" data-set-objective>Track</button>
      </div>
      <div class="hud-world-map-card-heading">Story Cards</div>
      <div class="hud-world-map-cards">
        ${cards.length ? cards.map((card) => `
          <article class="hud-world-map-card">
            <img src="${escapeHtml(card.image)}" alt="">
            <div>
              <div class="hud-world-map-card-meta">${escapeHtml(card.act)} · ${escapeHtml(card.scene)}</div>
              <h4>${escapeHtml(card.title)}</h4>
              <p>${escapeHtml(card.subtitle)}</p>
            </div>
          </article>
        `).join('') : '<p class="hud-world-map-empty">No story card is linked yet.</p>'}
      </div>
    `;
    this.worldMapInfo.querySelector('[data-set-objective]')?.addEventListener('click', () => {
      this.setActiveWaypoint(location.id);
      this.selectedMapLocationId = location.id;
      this._renderWorldMap();
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
function computeMapBounds(locations) {
  const xs = locations.map((loc) => loc.position.x);
  const zs = locations.map((loc) => loc.position.z);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minZ = Math.min(...zs);
  const maxZ = Math.max(...zs);
  const padX = Math.max(35, (maxX - minX) * 0.12);
  const padZ = Math.max(35, (maxZ - minZ) * 0.12);
  return { minX: minX - padX, maxX: maxX + padX, minZ: minZ - padZ, maxZ: maxZ + padZ };
}

function projectToMap(position, bounds) {
  const x = ((position.x - bounds.minX) / (bounds.maxX - bounds.minX)) * 100;
  const y = ((position.z - bounds.minZ) / (bounds.maxZ - bounds.minZ)) * 100;
  return {
    x: Math.max(2, Math.min(98, x)),
    y: Math.max(2, Math.min(98, y))
  };
}

function waypointColorForLocation(location) {
  if (location.id === 'crooked_pintle') return 'primary';
  if (location.type === 'npc' || location.type === 'event') return 'npc';
  if (['forage', 'lore', 'chapel', 'manor', 'river', 'endgame'].includes(location.type)) return 'story';
  return 'secondary';
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function wrapAngle(a) {
  while (a > Math.PI) a -= Math.PI * 2;
  while (a < -Math.PI) a += Math.PI * 2;
  return a;
}

// Inline 24×24 line-art SVG icons. Stroke uses currentColor so the parent's
// `color` style drives them. Round caps + joins give them a hand-drawn feel.
const SVG_PROPS = 'viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"';

const ICON_MAP = `<svg ${SVG_PROPS}>
  <path d="M9 18l-6 2.5v-15L9 3l6 3 6-2.5v15L15 21l-6-3z"/>
  <path d="M9 3v15M15 6v15"/>
</svg>`;

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
