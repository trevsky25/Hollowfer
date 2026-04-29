// Hollowfen UI: main menu, pause menu, field guide, story, Wren bio.
// Single module so the tabs share state and DOM lifecycle. Pure DOM —
// no Three.js coupling. Persists "started" flag and reveal state in
// localStorage so the player picks up where they left off.

import { MUSHROOM_INFO } from '../data/mushroomIndex.js';
import { STORY_CARDS } from '../data/StoryCards.js';
import { WrenPortrait3D } from './WrenPortrait3D.js';

const SAVE_KEY = 'hollowfen.save.v1';

function loadSave() {
  try {
    const raw = localStorage.getItem(SAVE_KEY);
    if (!raw) return defaultSave();
    const parsed = JSON.parse(raw);
    return { ...defaultSave(), ...parsed };
  } catch {
    return defaultSave();
  }
}

function defaultSave() {
  return {
    started: false,
    knownMushrooms: [],
    storyProgress: 0,    // index into STORY_CARDS — cards [0..storyProgress] are unlocked
    revealAll: true       // for the prototype: show every entry so the player can see content
  };
}

function persistSave(save) {
  try { localStorage.setItem(SAVE_KEY, JSON.stringify(save)); } catch {}
}

const EDIBILITY_TINT = {
  edible: '#7ec38a',
  deadly: '#d36a5b',
  magic: '#a47bd0',
  medicinal: '#7da7c8',
  unknown: '#bbb190'
};

export class Menu {
  constructor({ onBeginGame, onResume, dev }) {
    this.onBeginGame = onBeginGame;
    this.onResume = onResume;
    this.dev = dev || null;
    this.save = loadSave();
    this.activeTab = 'story';
    this.modal = null;

    this.root = document.createElement('div');
    this.root.className = 'menu-root';
    this.root.style.display = 'none';
    document.body.appendChild(this.root);
  }

  // Show the title screen on first load
  showMain() {
    this.root.innerHTML = '';
    this.root.appendChild(this._renderMain());
    this.root.style.display = 'flex';
    this.root.dataset.mode = 'main';
  }

  // Show the in-game pause overlay (player presses ESC)
  showPause() {
    this.root.innerHTML = '';
    this.root.appendChild(this._renderPause());
    this.root.style.display = 'flex';
    this.root.dataset.mode = 'pause';
  }

  hide() {
    this.root.style.display = 'none';
    this.root.innerHTML = '';
    this._closeModal();
    this._closeFullpage();
    this._disposeWrenPortrait();
  }

  _disposeWrenPortrait() {
    if (this._wrenPortrait) {
      try { this._wrenPortrait.dispose(); } catch {}
      this._wrenPortrait = null;
    }
  }

  isOpen() {
    return this.root.style.display !== 'none';
  }

  // ---------- MAIN MENU ----------
  _renderMain() {
    const wrap = el('div', 'menu-main');
    wrap.innerHTML = `
      <div class="menu-main-bg"></div>
      <div class="menu-main-card">
        <div class="menu-main-eyebrow">Hollowfen</div>
        <h1 class="menu-main-title">The Failing Village</h1>
        <p class="menu-main-tagline">Wren returns home with a forager's eye and an empty pack. The village is smaller than she remembers, and hungrier.</p>
        <div class="menu-main-actions">
          <button class="menu-btn menu-btn-primary" data-action="begin">${this.save.started ? 'Continue' : 'Begin'}</button>
          <button class="menu-btn" data-action="story">Story</button>
          <button class="menu-btn" data-action="guide">Field Guide</button>
          <button class="menu-btn" data-action="wren">Wren</button>
          <button class="menu-btn" data-action="settings">Settings</button>
        </div>
      </div>
    `;

    wrap.querySelector('[data-action="begin"]').addEventListener('click', () => this._begin());
    wrap.querySelector('[data-action="story"]').addEventListener('click', () => this._openTabFromMain('story'));
    wrap.querySelector('[data-action="guide"]').addEventListener('click', () => this._openTabFromMain('guide'));
    wrap.querySelector('[data-action="wren"]').addEventListener('click', () => this._openTabFromMain('wren'));
    wrap.querySelector('[data-action="settings"]').addEventListener('click', () => this._openTabFromMain('settings'));
    return wrap;
  }

  _begin() {
    this.save.started = true;
    persistSave(this.save);
    this.hide();
    this.onBeginGame?.();
  }

  _openTabFromMain(tab) {
    // From the main screen, "Story / Field Guide / Wren" buttons open the
    // pause-menu UI directly (no game running yet). Closing returns to main.
    this.activeTab = tab;
    this.root.innerHTML = '';
    const pause = this._renderPause({ fromMain: true });
    this.root.appendChild(pause);
  }

  // ---------- PAUSE MENU ----------
  _renderPause({ fromMain = false } = {}) {
    const wrap = el('div', 'menu-pause');
    const tabs = [
      { id: 'story', label: 'Story', body: () => this._renderStoryTab() },
      { id: 'guide', label: 'Field Guide', body: () => this._renderGuideTab() },
      { id: 'wren',  label: 'Wren',        body: () => this._renderWrenTab() },
      { id: 'controls', label: 'Controls', body: () => this._renderControlsTab() },
      { id: 'settings', label: 'Settings', body: () => this._renderSettingsTab() }
    ];
    if (this.dev) {
      tabs.push({ id: 'dev', label: 'Developer', body: () => this._renderDevTab() });
    }

    const sidebar = el('div', 'menu-pause-side');
    const closeLabel = fromMain ? 'Back to title' : 'Resume game';
    const closeBtn = el('button', 'menu-btn menu-btn-resume');
    closeBtn.textContent = closeLabel;
    closeBtn.addEventListener('click', () => {
      if (fromMain) this.showMain();
      else this._closePause();
    });
    sidebar.appendChild(closeBtn);

    const tabButtons = {};
    for (const t of tabs) {
      const b = el('button', 'menu-tab');
      b.textContent = t.label;
      b.addEventListener('click', () => this._switchTab(t.id));
      sidebar.appendChild(b);
      tabButtons[t.id] = b;
    }

    // Bottom-left "Back to Title" — always available, reverts to the main
    // landing page (whether we're paused mid-game or browsing from the title).
    const spacer = el('div', 'menu-pause-spacer');
    sidebar.appendChild(spacer);
    const backBtn = el('button', 'menu-btn menu-btn-back');
    backBtn.textContent = '← Back to Title';
    backBtn.addEventListener('click', () => this.showMain());
    sidebar.appendChild(backBtn);

    const main = el('div', 'menu-pause-main');
    const renderActive = () => {
      main.innerHTML = '';
      for (const id in tabButtons) tabButtons[id].classList.toggle('active', id === this.activeTab);
      const tab = tabs.find((t) => t.id === this.activeTab) || tabs[0];
      main.appendChild(tab.body());
    };
    this._switchTab = (id) => {
      // Dispose the 3D portrait when leaving the Wren tab so we don't leak GPU
      if (this.activeTab === 'wren' && id !== 'wren') this._disposeWrenPortrait();
      this.activeTab = id;
      renderActive();
    };
    renderActive();

    wrap.appendChild(sidebar);
    wrap.appendChild(main);
    return wrap;
  }

  _closePause() {
    this.hide();
    this.onResume?.();
  }

  // ---------- STORY TAB ----------
  _renderStoryTab() {
    const wrap = el('div', 'tab-pane');
    const heading = el('div', 'tab-heading');
    const total = STORY_CARDS.length;
    const unlockedCount = this.save.revealAll ? total : Math.min(total, this.save.storyProgress + 1);
    heading.innerHTML = `
      <h2>Story</h2>
      <p>${unlockedCount} of ${total} memories recorded. Four acts and four possible endings — Hollowfen as Wren lives it.</p>
    `;
    wrap.appendChild(heading);

    // Group cards by act so the player sees the structure of the whole story
    // rather than a flat 28-cell grid.
    const groups = [];
    let lastAct = null;
    for (let i = 0; i < STORY_CARDS.length; i++) {
      const card = STORY_CARDS[i];
      if (card.act !== lastAct) {
        groups.push({ act: card.act, cards: [] });
        lastAct = card.act;
      }
      groups[groups.length - 1].cards.push({ card, idx: i });
    }

    for (const group of groups) {
      const sectionHeader = el('div', 'story-act-header');
      sectionHeader.innerHTML = `
        <span class="story-act-label">${group.act}</span>
        <span class="story-act-line"></span>
        <span class="story-act-count">${group.cards.length} ${group.cards.length === 1 ? 'card' : 'cards'}</span>
      `;
      wrap.appendChild(sectionHeader);

      const grid = el('div', 'card-grid');
      for (const { card, idx } of group.cards) {
        const unlocked = this.save.revealAll || idx <= this.save.storyProgress;
        const cell = el('button', `card-cell ${unlocked ? '' : 'locked'}`);
        cell.innerHTML = `
          <div class="card-cell-image" style="background-image: url('${card.image}')"></div>
          <div class="card-cell-body">
            <div class="card-cell-act">${card.scene || card.act}</div>
            <div class="card-cell-title">${unlocked ? card.title : 'Locked Memory'}</div>
            <div class="card-cell-subtitle">${unlocked ? card.subtitle : '—'}</div>
          </div>
        `;
        if (unlocked) cell.addEventListener('click', () => this._showStoryDetail(card));
        grid.appendChild(cell);
      }
      wrap.appendChild(grid);
    }
    return wrap;
  }

  _showStoryDetail(card) {
    // Full-page takeover — the image fills the viewport and the text sits over
    // a long horizontal gradient on the left, giving the card a "page-turn in
    // an illustrated novel" feel.
    const idx = STORY_CARDS.findIndex((c) => c.id === card.id);
    const prev = idx > 0 ? STORY_CARDS[idx - 1] : null;
    const next = idx < STORY_CARDS.length - 1 ? STORY_CARDS[idx + 1] : null;
    const sceneLabel = card.scene ? `${card.act} · ${card.scene}` : card.act;

    const fullpage = el('div', 'story-fullpage');
    fullpage.innerHTML = `
      <div class="story-fullpage-bg" style="background-image: url('${card.image}')"></div>
      <div class="story-fullpage-fade"></div>
      <button class="story-fullpage-close" aria-label="Close">✕</button>

      <div class="story-fullpage-content">
        <div class="story-fullpage-eyebrow">${escapeHtml(sceneLabel)}</div>
        <h1 class="story-fullpage-title">${escapeHtml(card.title)}</h1>
        <div class="story-fullpage-subtitle">${escapeHtml(card.subtitle)}</div>

        <p class="story-fullpage-body">${escapeHtml(card.body)}</p>

        <blockquote class="story-fullpage-note">${escapeHtml(card.wrenNote)}</blockquote>

        <ul class="story-fullpage-beats">
          ${card.beats.map((b) => `<li>${escapeHtml(b)}</li>`).join('')}
        </ul>
      </div>

      <div class="story-fullpage-nav">
        <button class="story-fullpage-navbtn" data-nav="prev" ${prev ? '' : 'disabled'}>
          <span class="story-fullpage-navarrow">←</span>
          <span class="story-fullpage-navlabel">
            <span class="story-fullpage-navhint">Previous</span>
            <span class="story-fullpage-navtitle">${prev ? escapeHtml(prev.title) : '—'}</span>
          </span>
        </button>
        <div class="story-fullpage-counter">${idx + 1} / ${STORY_CARDS.length}</div>
        <button class="story-fullpage-navbtn story-fullpage-navbtn-next" data-nav="next" ${next ? '' : 'disabled'}>
          <span class="story-fullpage-navlabel">
            <span class="story-fullpage-navhint">Next</span>
            <span class="story-fullpage-navtitle">${next ? escapeHtml(next.title) : '—'}</span>
          </span>
          <span class="story-fullpage-navarrow">→</span>
        </button>
      </div>
    `;

    fullpage.querySelector('.story-fullpage-close').addEventListener('click', () => this._closeFullpage());
    if (prev) fullpage.querySelector('[data-nav="prev"]').addEventListener('click', () => {
      this._closeFullpage();
      this._showStoryDetail(prev);
    });
    if (next) fullpage.querySelector('[data-nav="next"]').addEventListener('click', () => {
      this._closeFullpage();
      this._showStoryDetail(next);
    });

    this._closeFullpage();
    document.body.appendChild(fullpage);
    this.fullpage = fullpage;
    this._fullpageKey = (e) => {
      if (e.code === 'Escape') { e.preventDefault(); this._closeFullpage(); }
      if (e.code === 'ArrowLeft' && prev) { e.preventDefault(); this._closeFullpage(); this._showStoryDetail(prev); }
      if (e.code === 'ArrowRight' && next) { e.preventDefault(); this._closeFullpage(); this._showStoryDetail(next); }
    };
    window.addEventListener('keydown', this._fullpageKey, true);
  }

  _closeFullpage() {
    if (this.fullpage) {
      this.fullpage.remove();
      this.fullpage = null;
    }
    if (this._fullpageKey) {
      window.removeEventListener('keydown', this._fullpageKey, true);
      this._fullpageKey = null;
    }
  }

  // ---------- FIELD GUIDE TAB ----------
  _renderGuideTab() {
    const wrap = el('div', 'tab-pane');
    const knownCount = this.save.revealAll ? Object.keys(MUSHROOM_INFO).length : this.save.knownMushrooms.length;
    const total = Object.keys(MUSHROOM_INFO).length;
    const heading = el('div', 'tab-heading');
    heading.innerHTML = `
      <h2>Field Guide</h2>
      <p>${knownCount} of ${total} species recorded. Click a thumbnail to see ID features, habitat, and lookalikes.</p>
    `;
    wrap.appendChild(heading);

    const grid = el('div', 'mushroom-grid');
    for (const id of Object.keys(MUSHROOM_INFO)) {
      const info = MUSHROOM_INFO[id];
      if (!info) continue;
      const known = this.save.revealAll || this.save.knownMushrooms.includes(id);
      const cell = el('button', `mushroom-cell ${known ? '' : 'locked'}`);
      const tint = EDIBILITY_TINT[info.edibility] || EDIBILITY_TINT.unknown;
      cell.innerHTML = `
        <div class="mushroom-cell-image" style="background-image: url('${known ? info.photoUrl : ''}')">${known ? '' : '?'}</div>
        <div class="mushroom-cell-body">
          <div class="mushroom-cell-name">${known ? info.commonName : '—'}</div>
          <div class="mushroom-cell-edibility" style="color:${tint}">${known ? info.edibilityLabel : 'Unknown'}</div>
        </div>
      `;
      if (known) cell.addEventListener('click', () => this._showMushroomDetail(info));
      grid.appendChild(cell);
    }
    wrap.appendChild(grid);
    return wrap;
  }

  _showMushroomDetail(info) {
    const tint = EDIBILITY_TINT[info.edibility] || EDIBILITY_TINT.unknown;
    const modal = el('div', 'menu-modal');
    modal.innerHTML = `
      <div class="menu-modal-card menu-modal-mushroom">
        <button class="menu-modal-close" aria-label="Close">✕</button>
        <div class="mushroom-modal-image" style="background-image: url('${info.photoUrl}')"></div>
        <div class="mushroom-modal-body">
          <div class="mushroom-modal-edibility" style="color:${tint}">${escapeHtml(info.edibilityLabel)}</div>
          <h2 class="mushroom-modal-name">${escapeHtml(info.commonName)}</h2>
          <div class="mushroom-modal-latin">${escapeHtml(info.latinName)}</div>
          <p class="mushroom-modal-description">${escapeHtml(info.description)}</p>
          <div class="mushroom-modal-section">
            <h3>Identifying Features</h3>
            <ul>${info.idFeatures.map((f) => `<li>${escapeHtml(f)}</li>`).join('')}</ul>
          </div>
          <div class="mushroom-modal-meta">
            <div><span>Habitat</span><p>${escapeHtml(info.habitat)}</p></div>
            <div><span>Season</span><p>${escapeHtml(info.season)}</p></div>
            <div><span>Look-alikes</span><p>${escapeHtml(info.lookalikes)}</p></div>
          </div>
          <div class="mushroom-modal-notes">${escapeHtml(info.notes)}</div>
        </div>
      </div>
    `;
    this._showModal(modal);
  }

  // ---------- WREN TAB ----------
  _renderWrenTab() {
    const wrap = el('div', 'tab-pane wren-tab');
    wrap.innerHTML = `
      <div class="wren-hero">
        <div class="wren-hero-portrait" data-wren-portrait>
          <div class="wren-hero-loading" data-wren-loading>Loading portrait…</div>
        </div>
        <div class="wren-hero-info">
          <div class="wren-hero-eyebrow">Protagonist</div>
          <h2 class="wren-hero-name">Wren of the Mill</h2>
          <div class="wren-hero-tagline">The forager who came home.</div>
          <p class="wren-hero-lead">Daughter of the late village miller. Returns to Hollowfen at eighteen with a knife at her hip and her father's hidden journal in her pocket. She can read the woods. The village has forgotten how.</p>

          <div class="wren-stats">
            <div class="wren-stat">
              <div class="wren-stat-label">Age</div>
              <div class="wren-stat-value">18</div>
            </div>
            <div class="wren-stat">
              <div class="wren-stat-label">Hometown</div>
              <div class="wren-stat-value">Hollowfen</div>
            </div>
            <div class="wren-stat">
              <div class="wren-stat-label">Trade</div>
              <div class="wren-stat-value">Forager</div>
            </div>
            <div class="wren-stat">
              <div class="wren-stat-label">Lineage</div>
              <div class="wren-stat-value">Tobin · Miller</div>
            </div>
          </div>

          <div class="wren-hero-hint">Drag the model to rotate · Releases to auto-spin</div>
        </div>
      </div>

      <div class="wren-grid">
        <article class="wren-card">
          <div class="wren-card-eyebrow">Background</div>
          <p>Born above her father Tobin's mill on the River Wend. Her mother died when she was eight; Tobin raised her among grain dust, ledgers, and the mushrooms he collected in secret. At fifteen she was sent to apprentice as a kitchen girl in Veyrwick. Her father's letters got shorter. Then they stopped. She returned to find Hollowfen smaller, the mill silent, and her father four months gone.</p>
        </article>

        <article class="wren-card">
          <div class="wren-card-eyebrow">How she sees the world</div>
          <p>Wren reads habitat the way other people read faces. A single damp log tells her what season it is and what is about to fruit on it. She is patient with mushrooms, less patient with people who pretend the village is doing fine. She prefers small exact words. She apologises before correcting someone, and corrects them anyway.</p>
        </article>

        <article class="wren-card wren-card-list">
          <div class="wren-card-eyebrow">What she carries</div>
          <ul>
            <li><strong>A folding knife</strong> with a horn handle — her father's.</li>
            <li><strong>A wicker basket</strong> lined with damp moss for delicate caps.</li>
            <li><strong>A worn field journal</strong> half its pages still blank.</li>
            <li><strong>A wide-brimmed hat</strong> for sun, rain, and not being recognised.</li>
            <li><strong>A coin purse</strong> light enough to be cruel.</li>
          </ul>
        </article>

        <article class="wren-card wren-card-list">
          <div class="wren-card-eyebrow">Who she meets again</div>
          <ul class="wren-npc-list">
            <li><span class="wren-npc-name">Old Bram</span><span class="wren-npc-role">Innkeeper · gave her the mill key</span></li>
            <li><span class="wren-npc-name">Marra</span><span class="wren-npc-role">Cook · kept the inn's fire lit</span></li>
            <li><span class="wren-npc-name">Sister Almy</span><span class="wren-npc-role">Vine-tender · keeper of the old lore</span></li>
            <li><span class="wren-npc-name">Joren</span><span class="wren-npc-role">Smith · proud, slow to thaw</span></li>
            <li><span class="wren-npc-name">Edda</span><span class="wren-npc-role">The girl who stayed</span></li>
            <li><span class="wren-npc-name">Father Calden</span><span class="wren-npc-role">Priest · cautious ally</span></li>
            <li><span class="wren-npc-name">Theo</span><span class="wren-npc-role">Trader · the temptation</span></li>
            <li><span class="wren-npc-name">Hollin</span><span class="wren-npc-role">Stranger · long-game companion</span></li>
          </ul>
        </article>
      </div>

      <blockquote class="wren-pullquote">
        I thought coming home would feel like stepping backward. It feels more like opening a door no one has touched in years.
      </blockquote>
    `;

    // Mount the 3D portrait after the layout settles. queueMicrotask runs
    // before reflow — the container can be 0×0 then. requestAnimationFrame
    // gives the browser one frame to lay out the element.
    const portraitDiv = wrap.querySelector('[data-wren-portrait]');
    const loadingEl = wrap.querySelector('[data-wren-loading]');
    requestAnimationFrame(() => {
      if (this._wrenPortrait) {
        try { this._wrenPortrait.dispose(); } catch {}
        this._wrenPortrait = null;
      }
      try {
        this._wrenPortrait = new WrenPortrait3D(portraitDiv);
        if (loadingEl) loadingEl.style.display = 'none';
      } catch (err) {
        console.error('[Menu] failed to mount WrenPortrait3D:', err);
        if (loadingEl) loadingEl.textContent = 'Portrait failed to load — see console';
      }
    });

    return wrap;
  }

  // ---------- SETTINGS TAB ----------
  _renderSettingsTab() {
    const wrap = el('div', 'tab-pane');
    const settings = this.save.settings || {};
    const masterVol = Math.round((settings.masterVolume ?? 0.7) * 100);
    const ambientVol = Math.round((settings.ambientVolume ?? 0.6) * 100);
    const quality = settings.quality || 'high';
    const cameraInvertY = !!settings.cameraInvertY;

    wrap.innerHTML = `
      <div class="tab-heading">
        <h2>Settings</h2>
        <p>Adjust how Hollowfen looks, sounds, and saves your progress.</p>
      </div>

      <div class="settings-section">
        <h3>Audio</h3>
        <label class="settings-row">
          <span>Master volume</span>
          <input type="range" min="0" max="100" value="${masterVol}" data-setting="master">
          <span class="settings-value" data-display="master">${masterVol}%</span>
        </label>
        <label class="settings-row">
          <span>Ambient &amp; world</span>
          <input type="range" min="0" max="100" value="${ambientVol}" data-setting="ambient">
          <span class="settings-value" data-display="ambient">${ambientVol}%</span>
        </label>
        <p class="settings-note">Audio hooks are placeholders for now &mdash; values save and will drive the world ambience and music when audio is added.</p>
      </div>

      <div class="settings-section">
        <h3>Graphics</h3>
        <div class="settings-row settings-row-buttons">
          <span>Render quality</span>
          <div class="settings-btn-group" data-setting="quality">
            ${['low', 'medium', 'high'].map((q) => `
              <button data-quality="${q}" class="${q === quality ? 'active' : ''}">${q[0].toUpperCase() + q.slice(1)}</button>
            `).join('')}
          </div>
        </div>
      </div>

      <div class="settings-section">
        <h3>Camera</h3>
        <label class="settings-row settings-row-checkbox">
          <input type="checkbox" data-setting="invertY" ${cameraInvertY ? 'checked' : ''}>
          <span>Invert vertical drag</span>
        </label>
      </div>

      <div class="settings-section">
        <h3>Save</h3>
        <p class="settings-row-text">All progress, story unlocks, and field-guide finds are saved locally to this browser.</p>
        <div class="settings-btn-row">
          <button class="menu-btn menu-btn-danger" data-action="reset">Reset progress</button>
        </div>
      </div>
    `;

    // Wire up controls
    const updateSettings = (patch) => {
      this.save.settings = { ...settings, ...patch };
      persistSave(this.save);
    };

    wrap.querySelector('[data-setting="master"]').addEventListener('input', (e) => {
      const v = Number(e.target.value);
      wrap.querySelector('[data-display="master"]').textContent = `${v}%`;
      updateSettings({ masterVolume: v / 100 });
    });
    wrap.querySelector('[data-setting="ambient"]').addEventListener('input', (e) => {
      const v = Number(e.target.value);
      wrap.querySelector('[data-display="ambient"]').textContent = `${v}%`;
      updateSettings({ ambientVolume: v / 100 });
    });
    wrap.querySelectorAll('.settings-btn-group [data-quality]').forEach((btn) => {
      btn.addEventListener('click', () => {
        wrap.querySelectorAll('.settings-btn-group [data-quality]').forEach((b) => b.classList.remove('active'));
        btn.classList.add('active');
        updateSettings({ quality: btn.dataset.quality });
      });
    });
    wrap.querySelector('[data-setting="invertY"]').addEventListener('change', (e) => {
      updateSettings({ cameraInvertY: e.target.checked });
    });
    wrap.querySelector('[data-action="reset"]').addEventListener('click', () => {
      if (!confirm('Reset all progress? This wipes your save and re-locks story cards.')) return;
      this.reset();
      alert('Progress reset. The page will reload.');
      location.reload();
    });

    return wrap;
  }

  // ---------- DEVELOPER TAB ----------
  _renderDevTab() {
    const wrap = el('div', 'tab-pane');
    const dev = this.dev;
    const hours = dev.getHours();
    const speed = dev.getSpeed();

    wrap.innerHTML = `
      <div class="tab-heading">
        <h2>Developer</h2>
        <p>Tools for testing the world. Changes apply live and don't save.</p>
      </div>

      <div class="settings-section">
        <h3>Time of Day</h3>
        <div class="settings-btn-row" data-time-presets>
          <button class="menu-btn" data-hours="6">Dawn</button>
          <button class="menu-btn" data-hours="12">Noon</button>
          <button class="menu-btn" data-hours="18">Dusk</button>
          <button class="menu-btn" data-hours="0">Midnight</button>
        </div>
        <label class="settings-row" style="margin-top:14px;">
          <span>Hour</span>
          <input type="range" min="0" max="24" step="0.1" value="${hours}" data-setting="hours">
          <span class="settings-value" data-display="hours">${formatHours(hours)}</span>
        </label>
      </div>

      <div class="settings-section">
        <h3>Player</h3>
        <label class="settings-row">
          <span>Move speed</span>
          <input type="range" min="0.25" max="4" step="0.05" value="${speed}" data-setting="speed">
          <span class="settings-value" data-display="speed">${speed.toFixed(2)}×</span>
        </label>
        <label class="settings-row settings-row-checkbox">
          <input type="checkbox" data-setting="noclip" ${dev.getNoclip() ? 'checked' : ''}>
          <span>Disable collision (noclip)</span>
        </label>
      </div>

      <div class="settings-section">
        <h3>Visuals</h3>
        <label class="settings-row settings-row-checkbox">
          <input type="checkbox" data-setting="fog" ${dev.getFog() ? 'checked' : ''}>
          <span>Fog enabled</span>
        </label>
        <label class="settings-row settings-row-checkbox">
          <input type="checkbox" data-setting="wireframe" ${dev.getWireframe() ? 'checked' : ''}>
          <span>Wireframe overlay</span>
        </label>
        <label class="settings-row settings-row-checkbox">
          <input type="checkbox" data-setting="fps" ${dev.getShowFps() ? 'checked' : ''}>
          <span>Show FPS counter</span>
        </label>
      </div>
    `;

    // Time presets
    wrap.querySelectorAll('[data-time-presets] [data-hours]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const h = Number(btn.dataset.hours);
        dev.setHours(h);
        const slider = wrap.querySelector('[data-setting="hours"]');
        const display = wrap.querySelector('[data-display="hours"]');
        slider.value = h;
        display.textContent = formatHours(h);
      });
    });
    wrap.querySelector('[data-setting="hours"]').addEventListener('input', (e) => {
      const v = Number(e.target.value);
      dev.setHours(v);
      wrap.querySelector('[data-display="hours"]').textContent = formatHours(v);
    });
    wrap.querySelector('[data-setting="speed"]').addEventListener('input', (e) => {
      const v = Number(e.target.value);
      dev.setSpeed(v);
      wrap.querySelector('[data-display="speed"]').textContent = `${v.toFixed(2)}×`;
    });
    wrap.querySelector('[data-setting="noclip"]').addEventListener('change', (e) => dev.setNoclip(e.target.checked));
    wrap.querySelector('[data-setting="fog"]').addEventListener('change', (e) => dev.setFog(e.target.checked));
    wrap.querySelector('[data-setting="wireframe"]').addEventListener('change', (e) => dev.setWireframe(e.target.checked));
    wrap.querySelector('[data-setting="fps"]').addEventListener('change', (e) => dev.setShowFps(e.target.checked));

    return wrap;
  }

  // ---------- CONTROLS TAB ----------
  _renderControlsTab() {
    const wrap = el('div', 'tab-pane');
    wrap.innerHTML = `
      <div class="tab-heading">
        <h2>Controls</h2>
      </div>
      <div class="controls-grid">
        <div><kbd>W A S D</kbd><span>Walk</span></div>
        <div><kbd>Shift</kbd><span>Run</span></div>
        <div><kbd>Space</kbd><span>Jump</span></div>
        <div><kbd>Drag</kbd><span>Turn camera</span></div>
        <div><kbd>ESC</kbd><span>Open / close menu</span></div>
      </div>
      <h3 style="margin: 28px 0 10px; font-size: 12px; letter-spacing: 0.22em; text-transform: uppercase; color: #d9bd6d;">Menu shortcuts</h3>
      <div class="controls-grid">
        <div><kbd>T</kbd><span>Story</span></div>
        <div><kbd>J</kbd><span>Field Guide</span></div>
        <div><kbd>C</kbd><span>Wren</span></div>
        <div><kbd>K</kbd><span>Controls</span></div>
        <div><kbd>O</kbd><span>Settings</span></div>
      </div>
    `;
    return wrap;
  }

  // ---------- MODAL ----------
  _showModal(modal) {
    this._closeModal();
    modal.querySelector('.menu-modal-close')?.addEventListener('click', () => this._closeModal());
    modal.addEventListener('click', (e) => { if (e.target === modal) this._closeModal(); });
    document.body.appendChild(modal);
    this.modal = modal;
  }

  _closeModal() {
    if (this.modal) { this.modal.remove(); this.modal = null; }
  }

  // ---------- PROGRESS API ----------
  markMushroomKnown(id) {
    if (!this.save.knownMushrooms.includes(id)) {
      this.save.knownMushrooms.push(id);
      persistSave(this.save);
    }
  }

  setStoryProgress(idx) {
    if (idx > this.save.storyProgress) {
      this.save.storyProgress = idx;
      persistSave(this.save);
    }
  }

  reset() {
    this.save = defaultSave();
    persistSave(this.save);
  }
}

// ---------- helpers ----------
function el(tag, className) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  return node;
}

function formatHours(hours) {
  const h = Math.floor(hours);
  const m = Math.floor((hours - h) * 60);
  const ampm = h >= 12 ? 'pm' : 'am';
  const h12 = h === 0 ? 12 : h > 12 ? h - 12 : h;
  return `${h12}:${String(m).padStart(2, '0')} ${ampm}`;
}

function escapeHtml(str) {
  if (!str) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
