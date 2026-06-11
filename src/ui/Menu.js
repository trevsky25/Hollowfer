// Hollowfen UI: main menu, pause menu, field guide, story, Wren bio.
// Single module so the tabs share state and DOM lifecycle. Pure DOM —
// no Three.js coupling. Persists "started" flag and reveal state in
// localStorage so the player picks up where they left off.

import { MUSHROOM_INFO } from '../data/mushroomIndex.js';
import { STORY_CARDS } from '../data/StoryCards.js';
import {
  HOLLOWFEN_LOCATION_OPTIONS,
  LOCATION_OPTION_BY_ID,
  LOCATION_PIN_STORAGE_KEY,
  mergeLocationPins
} from '../data/HollowfenLocations.js';

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

function loadLocationPins() {
  try {
    const raw = localStorage.getItem(LOCATION_PIN_STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function persistLocationPins(pins) {
  try { localStorage.setItem(LOCATION_PIN_STORAGE_KEY, JSON.stringify(pins, null, 2)); } catch {}
}

const EDIBILITY_TINT = {
  edible: '#7ec38a',
  deadly: '#d36a5b',
  magic: '#a47bd0',
  medicinal: '#7da7c8',
  unknown: '#bbb190'
};

export class Menu {
  constructor({ onBeginGame, onResume, dev, awaitVillageReady, onRequestCameraLock, setQuality, getQuality }) {
    this.onBeginGame = onBeginGame;
    this.onResume = onResume;
    this.dev = dev || null;
    this.awaitVillageReady = awaitVillageReady || null;
    this.onRequestCameraLock = onRequestCameraLock || null;
    // Live-apply render quality from the Settings tab — the renderer owner
    // (main.js) provides setter/getter so this UI module stays decoupled.
    this.setQuality = setQuality || null;
    this.getQuality = getQuality || null;
    this.save = loadSave();
    this.localLocationPins = loadLocationPins();
    this.locationPins = mergeLocationPins(this.localLocationPins);
    this.activeTab = 'story';
    this.modal = null;
    this.loadingOverlay = null;

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
    if (this.loadingOverlay) {
      this.loadingOverlay.remove();
      this.loadingOverlay = null;
    }
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
        <div class="menu-main-eyebrow">Wren Tobin's Return</div>
        <h1 class="menu-main-title">Hollowfen</h1>
        <div class="menu-main-subtitle">The Failing Village</div>
        <p class="menu-main-tagline">Forage the Edge Woods, learn what grows in the damp, and keep a hungry village alive.</p>
        <div class="menu-main-actions">
          <button class="menu-main-cta" data-action="begin">
            <span class="menu-main-cta-label">${this.save.started ? 'Continue' : 'Begin Foraging'}</span>
            <span class="menu-main-cta-arrow" aria-hidden="true">→</span>
          </button>
          <nav class="menu-main-links" aria-label="Menu sections">
            <button class="menu-main-link" data-action="story"><span>Story</span></button>
            <button class="menu-main-link" data-action="guide"><span>Field Guide</span></button>
            <button class="menu-main-link" data-action="wren"><span>Wren</span></button>
            <button class="menu-main-link" data-action="settings"><span>Settings</span></button>
          </nav>
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

  async _begin() {
    this.save.started = true;
    persistSave(this.save);
    // Request pointer lock SYNCHRONOUSLY — must run inside the same call
    // stack as the click event, before any await suspends the function, or
    // the browser drops the user-gesture context and rejects the request.
    // This is what gives the player camera control immediately when the
    // loading screen fades to game, with no need to click the canvas.
    this.onRequestCameraLock?.();
    // The loading overlay handles the title-menu teardown internally, in this
    // order: append overlay (covers screen instantly) → hide menu underneath
    // (no flicker of the 3D world) → hold + wait → fade out → reveal game.
    await this._showLoadingTransition();
    this.onBeginGame?.();
  }

  // ---------- LOADING TRANSITION ----------
  async _showLoadingTransition({ minHold = 2500, fadeOut = 520 } = {}) {
    const overlay = el('div', 'menu-loading');
    overlay.innerHTML = `
      <div class="menu-loading-bg"></div>
      <div class="menu-loading-vignette"></div>
      <div class="menu-loading-content">
        <div class="menu-loading-eyebrow">Hollowfen</div>
        <div class="menu-loading-title">Entering the Edge Woods</div>
        <div class="menu-loading-dots"><span></span><span></span><span></span></div>
      </div>
    `;
    // Append overlay FIRST so it covers the screen at opacity 1 the instant
    // it mounts. Only after the cover is up do we tear down the title menu
    // — otherwise there's a 1–2 frame flicker where the 3D world peeks
    // through between menu hide and overlay fade-in.
    document.body.appendChild(overlay);
    this.loadingOverlay = overlay;

    // Tear down the title menu under the overlay. Avoid this.hide() — that
    // path clears this.loadingOverlay too and would remove the cover.
    this.root.style.display = 'none';
    this.root.innerHTML = '';
    this._disposeWrenPortrait();

    const start = performance.now();
    if (this.awaitVillageReady) {
      try { await this.awaitVillageReady(); } catch {}
    }
    const elapsed = performance.now() - start;
    if (elapsed < minHold) {
      await new Promise((r) => setTimeout(r, minHold - elapsed));
    }

    // Fade overlay out → reveal the 3D world.
    overlay.classList.add('fading');
    await new Promise((r) => setTimeout(r, fadeOut));
    overlay.remove();
    if (this.loadingOverlay === overlay) this.loadingOverlay = null;
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
    // Two layouts:
    //   - fromMain (browsing tabs from title): full-screen sidebar layout
    //   - in-game (ESC or HUD hotkey): compact parchment popup centred on the
    //     dimmed game world. Same tab content, smaller frame, "in-world" feel.
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
    return fromMain ? this._renderPauseFull(tabs) : this._renderPausePopup(tabs);
  }

  _renderPauseFull(tabs) {
    const wrap = el('div', 'menu-pause');
    const sidebar = el('div', 'menu-pause-side');

    const brand = el('div', 'menu-pause-brand');
    brand.innerHTML = `
      <div class="menu-pause-eyebrow">Wren Tobin's Return</div>
      <div class="menu-pause-wordmark">Hollowfen</div>
    `;
    sidebar.appendChild(brand);

    const backBtn = el('button', 'menu-pause-back');
    backBtn.innerHTML = `
      <span class="menu-pause-back-arrow" aria-hidden="true">←</span>
      <span class="menu-pause-back-label">Back to title</span>
    `;
    backBtn.addEventListener('click', () => this.showMain());
    sidebar.appendChild(backBtn);

    const nav = el('nav', 'menu-pause-nav');
    nav.setAttribute('aria-label', 'Menu sections');
    sidebar.appendChild(nav);

    const tabButtons = {};
    for (const t of tabs) {
      const b = el('button', 'menu-pause-tab');
      b.innerHTML = `<span class="menu-pause-tab-label">${t.label}</span>`;
      b.addEventListener('click', () => this._switchTab(t.id));
      nav.appendChild(b);
      tabButtons[t.id] = b;
    }

    const main = el('div', 'menu-pause-main');
    const renderActive = () => {
      main.innerHTML = '';
      for (const id in tabButtons) tabButtons[id].classList.toggle('active', id === this.activeTab);
      const tab = tabs.find((t) => t.id === this.activeTab) || tabs[0];
      main.appendChild(tab.body());
    };
    this._switchTab = (id) => {
      if (this.activeTab === 'wren' && id !== 'wren') this._disposeWrenPortrait();
      this.activeTab = id;
      renderActive();
    };
    renderActive();

    wrap.appendChild(sidebar);
    wrap.appendChild(main);
    return wrap;
  }

  _renderPausePopup(tabs) {
    // Wren's open journal — dark brown leather covers wrap a two-page spread.
    // Left page: hand-numbered "Contents" index of sections.
    // Right page: active section heading + scrollable body (existing tab.body()).
    // Click-outside, ESC, or the corner ✕ all dismiss; hotkeys (T/J/C/K/O) jump tabs.
    const wrap = el('div', 'menu-pause-popup');
    wrap.addEventListener('click', (e) => {
      if (e.target === wrap) this._closePause();
    });

    // Animation container (runs the "book opens" keyframe once on mount).
    const journal = el('div', 'journal');
    const book = el('div', 'journal-book');

    // -------- LEFT PAGE: Contents index --------
    const leftPage = el('div', 'journal-page journal-page--left');
    const leftInner = el('div', 'journal-page-inner');

    const eyebrow = el('div', 'journal-eyebrow');
    eyebrow.textContent = 'Hollowfen';
    const title = el('div', 'journal-title');
    title.textContent = "Wren's Journal";
    const rule = el('div', 'journal-rule');
    leftInner.appendChild(eyebrow);
    leftInner.appendChild(title);
    leftInner.appendChild(rule);

    const indexHeading = el('div', 'journal-index-heading');
    indexHeading.textContent = 'Contents';
    leftInner.appendChild(indexHeading);

    const indexList = el('ul', 'journal-index');
    const TAB_NUMERALS = ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII'];
    const tabButtons = {};
    tabs.forEach((t, i) => {
      const item = el('li', 'journal-index-item');
      const btn = el('button', 'journal-index-btn');
      btn.setAttribute('type', 'button');
      btn.innerHTML = `
        <span class="journal-index-numeral">${TAB_NUMERALS[i] || String(i + 1)}.</span>
        <span class="journal-index-label">${t.label}</span>
      `;
      btn.addEventListener('click', () => this._switchTab(t.id));
      item.appendChild(btn);
      indexList.appendChild(item);
      tabButtons[t.id] = item; // toggle .active on the <li>
    });
    leftInner.appendChild(indexList);

    // Footer at the bottom of the left page — resume hint + back to title
    const foot = el('div', 'journal-foot');
    const hint = el('span', 'journal-hint');
    hint.textContent = 'Press ESC to resume';
    const back = el('button', 'journal-back');
    back.textContent = '← Back to Title';
    back.addEventListener('click', () => this.showMain());
    foot.appendChild(hint);
    foot.appendChild(back);
    leftInner.appendChild(foot);

    leftPage.appendChild(leftInner);

    // -------- SPINE --------
    const spine = el('div', 'journal-spine');

    // -------- RIGHT PAGE: active content --------
    const rightPage = el('div', 'journal-page journal-page--right');
    const rightInner = el('div', 'journal-page-inner');

    const closeX = el('button', 'journal-close');
    closeX.setAttribute('aria-label', 'Close');
    closeX.setAttribute('type', 'button');
    closeX.innerHTML = '&times;';
    closeX.addEventListener('click', () => this._closePause());

    const pageHead = el('div', 'journal-page-head');
    // Body keeps the legacy `.pause-panel-body` class so all the existing
    // content overrides (cards, settings rows, kbd, etc.) keep applying.
    const body = el('div', 'journal-page-body pause-panel-body');

    rightInner.appendChild(closeX);
    rightInner.appendChild(pageHead);
    rightInner.appendChild(body);
    rightPage.appendChild(rightInner);

    const renderActive = () => {
      body.innerHTML = '';
      const activeIdx = tabs.findIndex((t) => t.id === this.activeTab);
      const tab = activeIdx >= 0 ? tabs[activeIdx] : tabs[0];
      // Detail views blank/hide the page-head — switching tabs always restores
      // both visibility and the normal text content.
      pageHead.classList.remove('journal-page-head--blank');
      pageHead.style.display = '';
      // Roman numerals stay on the left-page contents index (a journal-style
      // table of contents) but the right-page heading is just the section
      // name — the numerals there were redundant noise.
      pageHead.textContent = tab.label;
      this._journalStoryBaseHead = null;
      this._journalGuideBaseHead = null;
      // Remove story-detail leftovers if a previous render left them hanging
      // — switching tabs should never carry the back button across to a
      // different section.
      rightInner.querySelector('.story-page-back')?.remove();
      for (const id in tabButtons) tabButtons[id].classList.toggle('active', id === tab.id);
      body.appendChild(tab.body());
      // Reset scroll to top when switching sections — feels like turning to a
      // fresh page rather than landing mid-paragraph.
      body.scrollTop = 0;
    };
    this._switchTab = (id) => {
      if (this.activeTab === 'wren' && id !== 'wren') this._disposeWrenPortrait();
      this.activeTab = id;
      renderActive();
    };
    renderActive();

    book.appendChild(leftPage);
    book.appendChild(spine);
    book.appendChild(rightPage);
    journal.appendChild(book);
    wrap.appendChild(journal);
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

  _showStoryDetail(card, transitionDirection = null) {
    // Two render paths:
    //   1. Pause journal open  → inline detail mounted into .journal-page-body
    //      (right page of the open book) — keeps the player in the journal
    //      metaphor and avoids a jarring full-screen takeover mid-game.
    //   2. Main menu (no pause popup) → existing full-screen takeover.
    const journalBody = document.querySelector('.menu-pause-popup .journal-page-body');
    if (journalBody) {
      this._showStoryDetailInJournal(card, journalBody, transitionDirection);
      return;
    }

    // Full-page takeover with a gallery-first image and a low reading tray.
    const idx = STORY_CARDS.findIndex((c) => c.id === card.id);
    const prev = idx > 0 ? STORY_CARDS[idx - 1] : null;
    const next = idx < STORY_CARDS.length - 1 ? STORY_CARDS[idx + 1] : null;
    const sceneLabel = card.scene ? `${card.act} · ${card.scene}` : card.act;

    const fullpage = el('div', 'story-fullpage');
    fullpage.innerHTML = `
      <div class="story-fullpage-bg" style="background-image: url('${card.image}')"></div>
      <div class="story-fullpage-fade"></div>
      <button class="story-fullpage-close" aria-label="Close">
        <span aria-hidden="true">✕</span>
      </button>

      <div class="story-fullpage-content">
        <div class="story-fullpage-heading">
          <div class="story-fullpage-eyebrow">${escapeHtml(sceneLabel)}</div>
          <h1 class="story-fullpage-title">${escapeHtml(card.title)}</h1>
          <div class="story-fullpage-subtitle">${escapeHtml(card.subtitle)}</div>
        </div>

        <div class="story-fullpage-copy">
          <p class="story-fullpage-body">${escapeHtml(card.body)}</p>
        </div>

        <div class="story-fullpage-aside">
          <blockquote class="story-fullpage-note">${escapeHtml(card.wrenNote)}</blockquote>
          <ul class="story-fullpage-beats">
            ${card.beats.map((b) => `<li>${escapeHtml(b)}</li>`).join('')}
          </ul>
        </div>

        <nav class="story-fullpage-nav" aria-label="Story card navigation">
          <button class="story-fullpage-navbtn" data-nav="prev" ${prev ? '' : 'disabled'}>
            <span class="story-fullpage-navarrow">←</span>
            <span class="story-fullpage-navlabel">
              <span class="story-fullpage-navhint">Previous</span>
              <span class="story-fullpage-navtitle">${prev ? escapeHtml(prev.title) : '—'}</span>
            </span>
          </button>
          <div class="story-fullpage-counter">
            <span class="story-fullpage-counter-num">${idx + 1}</span>
            <span class="story-fullpage-counter-sep">of</span>
            <span class="story-fullpage-counter-total">${STORY_CARDS.length}</span>
          </div>
          <button class="story-fullpage-navbtn story-fullpage-navbtn-next" data-nav="next" ${next ? '' : 'disabled'}>
            <span class="story-fullpage-navlabel">
              <span class="story-fullpage-navhint">Next</span>
              <span class="story-fullpage-navtitle">${next ? escapeHtml(next.title) : '—'}</span>
            </span>
            <span class="story-fullpage-navarrow">→</span>
          </button>
        </nav>
      </div>
    `;

    fullpage.querySelector('.story-fullpage-close').addEventListener('click', () => this._closeFullpage());
    if (prev) fullpage.querySelector('[data-nav="prev"]').addEventListener('click', () => {
      this._showStoryDetail(prev, 'prev');
    });
    if (next) fullpage.querySelector('[data-nav="next"]').addEventListener('click', () => {
      this._showStoryDetail(next, 'next');
    });

    const previousFullpage = this.fullpage;
    const isPageTurn = transitionDirection && previousFullpage;
    if (this._fullpageKey) {
      window.removeEventListener('keydown', this._fullpageKey, true);
      this._fullpageKey = null;
    }
    if (!isPageTurn) this._closeFullpage();

    if (isPageTurn) {
      fullpage.classList.add(`story-fullpage-enter-${transitionDirection}`);
      previousFullpage.classList.add(`story-fullpage-exit-${transitionDirection}`);
    }

    document.body.appendChild(fullpage);
    if (isPageTurn) {
      window.setTimeout(() => previousFullpage.remove(), 280);
    }
    this.fullpage = fullpage;
    this._fullpageKey = (e) => {
      if (e.code === 'Escape') { e.preventDefault(); this._closeFullpage(); }
      if (e.code === 'ArrowLeft' && prev) { e.preventDefault(); this._showStoryDetail(prev, 'prev'); }
      if (e.code === 'ArrowRight' && next) { e.preventDefault(); this._showStoryDetail(next, 'next'); }
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

  // Inline story-card detail for the pause journal — fills the right page
  // (.journal-page-body) instead of taking over the whole screen. Stays in
  // the open-book metaphor, supports prev/next swaps, and a "back" button
  // restores the act-grouped story grid.
  _showStoryDetailInJournal(card, body, transitionDirection = null) {
    const idx = STORY_CARDS.findIndex((c) => c.id === card.id);
    const prev = idx > 0 ? STORY_CARDS[idx - 1] : null;
    const next = idx < STORY_CARDS.length - 1 ? STORY_CARDS[idx + 1] : null;
    const sceneLabel = card.scene ? `${card.act} · ${card.scene}` : card.act;

    // Blank out the page-head text but KEEP its vertical space — that gives
    // the back/✕ buttons a "header zone" of paper to sit on, and pushes the
    // body's top edge (where overflow clipping happens) safely below the
    // buttons so scrolled content can't creep up behind them. Cache the
    // original text for restoration on back-nav.
    const head = document.querySelector('.menu-pause-popup .journal-page-head');
    if (head) {
      if (!this._journalStoryBaseHead) this._journalStoryBaseHead = head.textContent || '';
      head.textContent = '';
      head.classList.add('journal-page-head--blank');
    }

    const detail = el('div', 'story-page');
    detail.innerHTML = `
      <div class="story-page-image" style="background-image: url('${card.image}')"></div>

      <div class="story-page-heading">
        <div class="story-page-eyebrow">${escapeHtml(sceneLabel)}</div>
        <h2 class="story-page-title">${escapeHtml(card.title)}</h2>
        <div class="story-page-subtitle">${escapeHtml(card.subtitle)}</div>
      </div>

      <p class="story-page-body">${escapeHtml(card.body)}</p>

      <blockquote class="story-page-note">${escapeHtml(card.wrenNote)}</blockquote>

      <ul class="story-page-beats">
        ${card.beats.map((b) => `<li>${escapeHtml(b)}</li>`).join('')}
      </ul>

      <div class="story-page-nav">
        <button class="story-page-navbtn" data-nav="prev" ${prev ? '' : 'disabled'}>
          <span class="story-page-navarrow">←</span>
          <span class="story-page-navlabel">
            <span class="story-page-navhint">Previous</span>
            <span class="story-page-navtitle">${prev ? escapeHtml(prev.title) : '—'}</span>
          </span>
        </button>
        <div class="story-page-counter">${idx + 1} / ${STORY_CARDS.length}</div>
        <button class="story-page-navbtn story-page-navbtn-next" data-nav="next" ${next ? '' : 'disabled'}>
          <span class="story-page-navlabel">
            <span class="story-page-navhint">Next</span>
            <span class="story-page-navtitle">${next ? escapeHtml(next.title) : '—'}</span>
          </span>
          <span class="story-page-navarrow">→</span>
        </button>
      </div>
    `;

    if (prev) detail.querySelector('[data-nav="prev"]').addEventListener('click', () => {
      this._showStoryDetailInJournal(prev, body, 'prev');
    });
    if (next) detail.querySelector('[data-nav="next"]').addEventListener('click', () => {
      this._showStoryDetailInJournal(next, body, 'next');
    });

    body.innerHTML = '';
    if (transitionDirection) {
      detail.classList.add(`story-page-enter-${transitionDirection}`);
    }
    body.appendChild(detail);
    body.scrollTop = 0;

    // Click the card image to enlarge it to fill the journal book (covers
    // both pages + spine, leather frame still visible). Click again to close.
    const imageEl = detail.querySelector('.story-page-image');
    if (imageEl) {
      imageEl.addEventListener('click', () => this._showStoryImageZoom(card.image));
    }

    // Mount the back button as a sibling of `.journal-close` inside
    // `.journal-page-inner`. No header bar — buttons sit directly on the
    // paper texture (matches the Wren tab's plain page-head treatment). The
    // body's `overflow: auto` already clips scrolled content at its edges,
    // so we don't need a covering bar to hide content scrolling past.
    const inner = document.querySelector('.menu-pause-popup .journal-page--right .journal-page-inner');
    if (inner) {
      // Clean up any leftover button from a previous detail render.
      inner.querySelector('.story-page-back')?.remove();

      const backBtn = el('button', 'story-page-back');
      backBtn.setAttribute('aria-label', 'Back to story list');
      backBtn.innerHTML = '<span class="story-page-back-arrow">←</span><span>Back</span>';
      backBtn.addEventListener('click', () => {
        // Restore the page-head (cached when we hid it) and re-render the story
        // tab grid via _switchTab — same effect as clicking "I. Story" in the
        // contents index.
        if (head) {
          head.classList.remove('journal-page-head--blank');
          if (this._journalStoryBaseHead) head.textContent = this._journalStoryBaseHead;
          this._journalStoryBaseHead = null;
        }
        backBtn.remove();
        this._switchTab('story');
      });
      inner.appendChild(backBtn);
    }
  }

  // Enlarge the story-card image to fill the open journal book (both pages
  // + spine), leaving the leather frame visible. Click anywhere on the
  // overlay or hit ESC to close — the underlying detail view is untouched.
  _showStoryImageZoom(imageUrl) {
    const journalBook = document.querySelector('.menu-pause-popup .journal-book');
    if (!journalBook) return;
    // Wipe any existing zoom (defensive — clicking the image twice fast).
    journalBook.querySelector('.story-image-zoom')?.remove();

    const zoom = el('div', 'story-image-zoom');
    zoom.style.backgroundImage = `url('${imageUrl}')`;

    const close = el('button', 'story-image-zoom-close');
    close.setAttribute('aria-label', 'Close image');
    close.setAttribute('type', 'button');
    close.innerHTML = '&times;';
    zoom.appendChild(close);

    const dismiss = () => {
      zoom.remove();
      window.removeEventListener('keydown', escHandler, true);
    };
    const escHandler = (e) => {
      if (e.code === 'Escape') {
        // Stop the journal's outer ESC handler so closing the zoom doesn't
        // also close the entire pause menu.
        e.preventDefault();
        e.stopPropagation();
        dismiss();
      }
    };
    // Click anywhere on the overlay (or the ✕) to dismiss.
    zoom.addEventListener('click', dismiss);
    close.addEventListener('click', (e) => { e.stopPropagation(); dismiss(); });
    window.addEventListener('keydown', escHandler, true);

    journalBook.appendChild(zoom);
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
    // Same two-path branching as story cards: in the pause journal we render
    // an inline detail page (matches the story-page UX); from the main menu
    // (no pause popup) we fall back to the legacy dark-themed modal.
    const journalBody = document.querySelector('.menu-pause-popup .journal-page-body');
    if (journalBody) {
      this._showMushroomDetailInJournal(info, journalBody);
      return;
    }
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

  // Inline mushroom-species detail for the pause journal — matches the
  // story-page UX: sticky header bar with back + close ✕, full-width photo
  // (click to zoom), then handwritten copy in the journal aesthetic. The
  // sticky bar and back-button DOM are shared with the story flow (same
  // .story-page-headerbar / .story-page-back classes) so the two pages
  // feel like the same UI surface.
  _showMushroomDetailInJournal(info, body) {
    const tint = EDIBILITY_TINT[info.edibility] || EDIBILITY_TINT.unknown;

    // Blank the page-head but keep its vertical space (same pattern as story
    // detail) so the back/✕ buttons sit on a tall enough paper "header zone"
    // and scrolled content stays below them.
    const head = document.querySelector('.menu-pause-popup .journal-page-head');
    if (head) {
      if (!this._journalGuideBaseHead) this._journalGuideBaseHead = head.textContent || '';
      head.textContent = '';
      head.classList.add('journal-page-head--blank');
    }

    const detail = el('div', 'species-page');
    detail.innerHTML = `
      <div class="species-page-image" style="background-image: url('${info.photoUrl}')"></div>

      <div class="species-page-heading">
        <div class="species-page-edibility" style="color:${tint}">${escapeHtml(info.edibilityLabel)}</div>
        <h2 class="species-page-name">${escapeHtml(info.commonName)}</h2>
        <div class="species-page-latin">${escapeHtml(info.latinName)}</div>
      </div>

      <p class="species-page-body">${escapeHtml(info.description)}</p>

      <div class="species-page-section">
        <h3>Identifying features</h3>
        <ul class="species-page-list">
          ${info.idFeatures.map((f) => `<li>${escapeHtml(f)}</li>`).join('')}
        </ul>
      </div>

      <div class="species-page-meta">
        <div><span>Habitat</span><p>${escapeHtml(info.habitat)}</p></div>
        <div><span>Season</span><p>${escapeHtml(info.season)}</p></div>
        <div><span>Look-alikes</span><p>${escapeHtml(info.lookalikes)}</p></div>
      </div>

      <blockquote class="species-page-notes">${escapeHtml(info.notes)}</blockquote>
    `;

    body.innerHTML = '';
    body.appendChild(detail);
    body.scrollTop = 0;

    // Click the species photo to enlarge it inside the journal. Reuses the
    // story-detail zoom — generic image-overlay scoped to .journal-book.
    const imageEl = detail.querySelector('.species-page-image');
    if (imageEl) imageEl.addEventListener('click', () => this._showStoryImageZoom(info.photoUrl));

    // Mount the back button alongside the close ✕ — same plain treatment as
    // story detail. No header bar; buttons sit on the natural paper.
    const inner = document.querySelector('.menu-pause-popup .journal-page--right .journal-page-inner');
    if (inner) {
      inner.querySelector('.story-page-back')?.remove();

      const backBtn = el('button', 'story-page-back');
      backBtn.setAttribute('aria-label', 'Back to species list');
      backBtn.innerHTML = '<span class="story-page-back-arrow">←</span><span>Back</span>';
      backBtn.addEventListener('click', () => {
        if (head) {
          head.classList.remove('journal-page-head--blank');
          if (this._journalGuideBaseHead) head.textContent = this._journalGuideBaseHead;
          this._journalGuideBaseHead = null;
        }
        backBtn.remove();
        this._switchTab('guide');
      });
      inner.appendChild(backBtn);
    }
  }

  // ---------- WREN TAB ----------
  _renderWrenTab() {
    const wrap = el('div', 'tab-pane wren-tab');
    wrap.innerHTML = `
      <div class="wren-hero">
        <div class="wren-profile-art">
          <img src="/ui/wren-profile.png" alt="Wren Tobin with her foraging tools, journal, basket, and mushrooms">
        </div>
        <div class="wren-hero-info">
          <div class="wren-hero-eyebrow">Protagonist</div>
          <h2 class="wren-hero-name">Wren Tobin</h2>
          <div class="wren-hero-tagline">The miller's daughter who came home with a forager's eye.</div>
          <p class="wren-hero-lead">Wren returns to Hollowfen after three winters away and finds the mill silent, the village hungry, and her father's hidden journal waiting in a drawer. She can read damp bark, shadow, season, and soil. The woods have not forgotten her family, even if the village nearly has.</p>

          <div class="wren-stats">
            <div class="wren-stat">
              <div class="wren-stat-label">Age</div>
              <div class="wren-stat-value">Late twenties</div>
            </div>
            <div class="wren-stat">
              <div class="wren-stat-label">Home</div>
              <div class="wren-stat-value">Hollowfen</div>
            </div>
            <div class="wren-stat">
              <div class="wren-stat-label">Work</div>
              <div class="wren-stat-value">Forager</div>
            </div>
            <div class="wren-stat">
              <div class="wren-stat-label">Keepsake</div>
              <div class="wren-stat-value">Tobin's journal</div>
            </div>
          </div>
        </div>
      </div>

      <div class="wren-kit-strip">
        <div><span>Knife</span> Horn-handled, sharp enough for clean cuts.</div>
        <div><span>Basket</span> Wicker, moss-lined, never quite empty.</div>
        <div><span>Journal</span> Her father's notes, half confession.</div>
        <div><span>Brightspore</span> The woods' smallest impossible light.</div>
      </div>

      <div class="wren-grid">
        <article class="wren-card">
          <div class="wren-card-eyebrow">Background</div>
          <p>Born above Tobin's mill on the River Wend, Wren grew up among grain dust, ledgers, and the quiet habits of a father who knew more about mushrooms than he ever admitted aloud. She left for kitchen work in Veyrwick and came back to a village smaller than memory, with boarded windows and a dead mill wheel.</p>
        </article>

        <article class="wren-card">
          <div class="wren-card-eyebrow">How she sees the world</div>
          <p>Wren reads habitat the way other people read faces. A damp birch log tells her what season it is, what will fruit next, and whether the woods are safe to trust. She is patient with mushrooms, less patient with people who pretend Hollowfen is doing fine.</p>
        </article>

        <article class="wren-card wren-card-list">
          <div class="wren-card-eyebrow">What she carries</div>
          <ul>
            <li><strong>A foraging knife</strong> with a horn handle and a dark leather sheath.</li>
            <li><strong>A wicker basket</strong> with a leather strap and room for the day's risk.</li>
            <li><strong>Tobin's field journal</strong> filled with sketches, warnings, and omissions.</li>
            <li><strong>A belt pouch</strong> for coins, twine, oilcloth, and careful scraps.</li>
            <li><strong>Found mushrooms</strong> goldfoots, field caps, wood-ear, pinecrest, and Brightspore.</li>
          </ul>
        </article>

        <article class="wren-card wren-card-list">
          <div class="wren-card-eyebrow">People around her</div>
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

    // Wren's portrait is clickable in the pause journal — opens the same
    // full-journal zoom overlay used by story cards and species photos.
    // The image inside .wren-profile-art is targeted directly so the click
    // works on the visible artwork.
    const portrait = wrap.querySelector('.wren-profile-art img');
    if (portrait) {
      portrait.style.cursor = 'zoom-in';
      portrait.addEventListener('click', () => this._showStoryImageZoom(portrait.getAttribute('src')));
    }

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
        const level = btn.dataset.quality;
        updateSettings({ quality: level });
        // Live-apply: renderer DPR cap, shadow map size, shadow filter type,
        // and camera far update immediately. No reload required.
        this.setQuality?.(level);
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
    const fly = dev.getFlyMode ? dev.getFlyMode() : false;
    const locationOptions = HOLLOWFEN_LOCATION_OPTIONS.map((group) => `
      <optgroup label="${escapeHtml(group.group)}">
        ${group.items.map((item) => `<option value="${item.id}">${escapeHtml(item.name)}</option>`).join('')}
      </optgroup>
    `).join('');

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
        <label class="settings-row settings-row-checkbox">
          <input type="checkbox" data-setting="fly" ${fly ? 'checked' : ''}>
          <span>Fly mode for mapping</span>
        </label>
        <p class="settings-note">Fly mode uses WASD to move, Shift to speed up, Space/E to rise, and Q to lower. It also disables collision while active.</p>
      </div>

      <div class="settings-section">
        <h3>Location Mapping</h3>
        <p class="settings-row-text">Choose a story location, move Wren to the doorway or NPC standing spot, then pin the current position. Pins save locally and can be copied into the canon map data later.</p>
        <label class="settings-row">
          <span>Location</span>
          <select data-location-select>
            ${locationOptions}
          </select>
        </label>
        <div class="location-option-help" data-location-help></div>
        <div class="settings-btn-row">
          <button class="menu-btn" data-action="pin-location">Pin at Wren</button>
          <button class="menu-btn" data-action="copy-pins">Copy JSON</button>
          <button class="menu-btn" data-action="download-pins">Download JSON</button>
        </div>
        <div class="location-pin-readout" data-pin-readout></div>
        <div class="location-pin-list" data-pin-list></div>
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
    wrap.querySelector('[data-setting="fly"]').addEventListener('change', (e) => {
      dev.setFlyMode?.(e.target.checked);
      if (e.target.checked) {
        const noclip = wrap.querySelector('[data-setting="noclip"]');
        noclip.checked = true;
      }
    });
    wrap.querySelector('[data-setting="fog"]').addEventListener('change', (e) => dev.setFog(e.target.checked));
    wrap.querySelector('[data-setting="wireframe"]').addEventListener('change', (e) => dev.setWireframe(e.target.checked));
    wrap.querySelector('[data-setting="fps"]').addEventListener('change', (e) => dev.setShowFps(e.target.checked));

    const readout = wrap.querySelector('[data-pin-readout]');
    const pinList = wrap.querySelector('[data-pin-list]');
    const locationSelect = wrap.querySelector('[data-location-select]');
    const locationHelp = wrap.querySelector('[data-location-help]');

    const renderLocationHelp = () => {
      const option = LOCATION_OPTION_BY_ID.get(locationSelect.value);
      if (!option) {
        locationHelp.innerHTML = '';
        return;
      }
      locationHelp.innerHTML = `
        <strong>${escapeHtml(option.name)}</strong>
        <span>${escapeHtml(option.type || 'location')}</span>
        <p>${escapeHtml(option.description || '')}</p>
      `;
    };

    const renderPins = () => {
      if (!this.locationPins.length) {
        pinList.innerHTML = '<p class="settings-note">No locations pinned yet.</p>';
        this.dev.setLocationPins?.(this.locationPins);
        return;
      }
      pinList.innerHTML = this.locationPins.map((pin) => `
        <div class="location-pin-row ${pin.source === 'canon' ? 'is-canon' : ''}">
          <div>
            <strong>${escapeHtml(pin.name)}</strong>
            <span>${escapeHtml(pin.source === 'canon' ? 'canon' : 'local')} · ${escapeHtml(pin.type || 'location')} · x ${pin.position.x.toFixed(2)}, y ${pin.position.y.toFixed(2)}, z ${pin.position.z.toFixed(2)}</span>
          </div>
          ${pin.source === 'canon'
            ? '<button type="button" disabled>Canon</button>'
            : `<button type="button" data-clear-pin="${pin.id}">Clear</button>`}
        </div>
      `).join('');
      pinList.querySelectorAll('[data-clear-pin]').forEach((btn) => {
        btn.addEventListener('click', () => {
          this.localLocationPins = this.localLocationPins.filter((pin) => pin.id !== btn.dataset.clearPin);
          persistLocationPins(this.localLocationPins);
          this.locationPins = mergeLocationPins(this.localLocationPins);
          this.dev.setLocationPins?.(this.locationPins);
          renderPins();
        });
      });
      this.dev.setLocationPins?.(this.locationPins);
    };

    wrap.querySelector('[data-action="pin-location"]').addEventListener('click', () => {
      const option = LOCATION_OPTION_BY_ID.get(locationSelect.value);
      if (!option) return;
      const snapshot = this.dev.getPlayerSnapshot?.();
      if (!snapshot) {
        readout.textContent = 'Wren is not ready yet. Start the game world first.';
        return;
      }
      const pin = {
        id: option.id,
        name: option.name,
        type: option.type,
        description: option.description,
        position: snapshot.position,
        facing: snapshot.facing,
        camera: snapshot.camera,
        updatedAt: new Date().toISOString()
      };
      this.localLocationPins = [
        ...this.localLocationPins.filter((existing) => existing.id !== pin.id),
        pin
      ].sort((a, b) => a.name.localeCompare(b.name));
      persistLocationPins(this.localLocationPins);
      this.locationPins = mergeLocationPins(this.localLocationPins);
      this.dev.setLocationPins?.(this.locationPins);
      readout.textContent = `Pinned ${pin.name} at x ${pin.position.x.toFixed(2)}, y ${pin.position.y.toFixed(2)}, z ${pin.position.z.toFixed(2)}.`;
      renderPins();
    });

    locationSelect.addEventListener('change', renderLocationHelp);

    wrap.querySelector('[data-action="copy-pins"]').addEventListener('click', async () => {
      const json = JSON.stringify(this.locationPins, null, 2);
      try {
        await navigator.clipboard.writeText(json);
        readout.textContent = 'Copied location pins JSON to clipboard.';
      } catch {
        readout.textContent = json;
      }
    });

    wrap.querySelector('[data-action="download-pins"]').addEventListener('click', () => {
      const blob = new Blob([JSON.stringify(this.locationPins, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'hollowfen-location-pins.json';
      a.click();
      URL.revokeObjectURL(url);
    });

    renderPins();
    renderLocationHelp();

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
      <h3 class="controls-subhead">Menu shortcuts</h3>
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
