import storyMarkdown from '../docs/story.md?raw';
import { CHARACTER_IDENTITIES } from './data/CharacterIdentities.js';
import { MUSHROOM_INFO } from './data/mushroomIndex.js';

// Canonical mapping from each implemented species to its act/quest in the
// story. Order is the in-game journal order — five safe basics first, then
// five working mushrooms (Act II), then four of the Old Wood (Act III), then
// the two final lessons (Act IV). Keep this in sync with the
// "Mushroom Compendium" markdown section in docs/story.md.
const MUSHROOM_COMPENDIUM_ORDER = [
  // Act I — Five Safe Basics
  { id: 'fieldCap',         act: 'Act I',   tier: 'Tier 1 — Safe basics',   missionId: 'firstForage',     missionTitle: 'The First Forage', teacher: 'Bram',         buyer: 'Marra',             unlockedBy: 'firstForage' },
  { id: 'woodEar',          act: 'Act I',   tier: 'Tier 1 — Safe basics',   missionId: 'firstForage',     missionTitle: 'The First Forage', teacher: 'Bram',         buyer: 'Marra',             unlockedBy: 'firstForage' },
  { id: 'pinecrest',        act: 'Act I',   tier: 'Tier 1 — Safe basics',   missionId: 'firstForage',     missionTitle: 'The First Forage', teacher: "Tobin's journal", buyer: 'Theo',           unlockedBy: 'firstForage' },
  { id: 'goldfoot',         act: 'Act I',   tier: 'Tier 1 — Safe basics',   missionId: 'firstSale',       missionTitle: "Marra's Kitchen",  teacher: 'Edda',         buyer: 'Marra · Theo',      unlockedBy: 'firstForage' },
  { id: 'fieldMushroom',    act: 'Act I',   tier: 'Tier 1 — Safe basics',   missionId: 'firstSale',       missionTitle: "Marra's Kitchen",  teacher: 'Bram',         buyer: 'Marra',             unlockedBy: 'firstSale' },
  // Act II — Five Working Mushrooms
  { id: 'chanterelle',      act: 'Act II',  tier: 'Tier 2 — Working',       missionId: 'theoTrade',       missionTitle: "The Trader's Ledger", teacher: 'Tobin\'s journal', buyer: 'Theo',         unlockedBy: 'theoTrade' },
  { id: 'lacewig',          act: 'Act II',  tier: 'Tier 2 — Working',       missionId: 'almyTeach',       missionTitle: "The Vine-Tender's Lessons", teacher: 'Sister Almy', buyer: 'Cultivated',    unlockedBy: 'almyTeach' },
  { id: 'coppercup',        act: 'Act II',  tier: 'Tier 2 — Working',       missionId: 'theoTrade',       missionTitle: "Festival prep & market", teacher: 'Marra',     buyer: 'Festival',          unlockedBy: 'theoTrade' },
  { id: 'bonepale',         act: 'Act II',  tier: 'Tier 2 — Medicinal',     missionId: 'almyTeach',       missionTitle: "Almy's medicine intro",  teacher: 'Sister Almy', buyer: 'Inn (tonic)',     unlockedBy: 'almyTeach' },
  { id: 'brightspore',      act: 'Act II',  tier: 'Tier 2 — Medicinal',     missionId: 'edsGrandfather',  missionTitle: 'Brightspore at the Bedside', teacher: 'Sister Almy', buyer: "Edda's tonic",  unlockedBy: 'edsGrandfather' },
  // Act III — Four of the Old Wood
  { id: 'porcini',          act: 'Act III', tier: 'Tier 3 — Old Wood',      missionId: 'cottagesReopen',  missionTitle: 'Two Boards Come Down → Capital trade', teacher: 'Tobin\'s journal', buyer: 'Theo (Capital price)', unlockedBy: 'cottagesReopen' },
  { id: 'libertyCap',       act: 'Act III', tier: 'Tier 3 — Old Wood',      missionId: 'findWitchCottage', missionTitle: "The Witch's Cottage", teacher: 'Sable',       buyer: 'Lore only',         unlockedBy: 'findWitchCottage' },
  { id: 'flyAgaric',        act: 'Act III', tier: 'Tier 3 — Old Wood',      missionId: 'findWitchCottage', missionTitle: "The Witch's Cottage", teacher: 'Sable',       buyer: 'Lore only',         unlockedBy: 'findWitchCottage' },
  { id: 'deadlyGalerina',   act: 'Act III', tier: 'Tier 3 — Old Wood',      missionId: 'findWitchCottage', missionTitle: "Liberty / Galerina pairing", teacher: 'Sable',     buyer: 'Lore only',         unlockedBy: 'findWitchCottage' },
  // Act IV — Two Final Lessons
  { id: 'deathCap',         act: 'Act IV',  tier: 'Tier 4 — Final lesson',  missionId: 'aldricLetter',    missionTitle: "A Sealed Letter → Wren teaches", teacher: 'Wren',  buyer: 'Chapel notice board', unlockedBy: 'aldricLetter' },
  { id: 'destroyingAngel',  act: 'Act IV',  tier: 'Tier 4 — Final lesson',  missionId: 'meetAldric',      missionTitle: 'The Meeting → Closes the gill-color lesson', teacher: 'Wren', buyer: 'Chapel notice board', unlockedBy: 'meetAldric' }
];

const storyEl = document.querySelector('#story');
const tocEl = document.querySelector('#toc');
const tocPanel = document.querySelector('.toc-panel');
const tocToggle = document.querySelector('#toc-toggle');
const stopButton = document.querySelector('#stop-speech');
const voiceSelect = document.querySelector('#narrator-voice');
const rateInput = document.querySelector('#narrator-rate');
const rateValue = document.querySelector('#narrator-rate-value');

const VOICE_STORAGE_KEY = 'hollowfen.narratorVoice';
const RATE_STORAGE_KEY = 'hollowfen.narratorRate';

const speechState = {
  chunks: [],
  index: 0,
  activeButton: null,
  stopped: false,
  voices: [],
  selectedVoiceURI: localStorage.getItem(VOICE_STORAGE_KEY) || 'auto',
  rate: Number(localStorage.getItem(RATE_STORAGE_KEY)) || 0.86
};

init();

async function init() {
  try {
    const documentModel = parseStory(storyMarkdown);
    renderStory(documentModel);
    setupMobileToc();
    bindSpeechControls();
    setupNarratorControls();
    setupScrollSpy();
  } catch (error) {
    storyEl.innerHTML = `
      <section class="loading-panel">
        <p class="eyebrow">Error</p>
        <h2>The journal would not open.</h2>
        <p>${escapeHtml(error.message)}</p>
      </section>
    `;
  }
}

function setupMobileToc() {
  if (!tocPanel || !tocToggle) return;
  const media = window.matchMedia('(max-width: 880px)');
  const applyMode = () => {
    const mobile = media.matches;
    tocPanel.classList.toggle('is-mobile', mobile);
    tocPanel.classList.toggle('is-collapsed', mobile);
    tocToggle.setAttribute('aria-expanded', mobile ? 'false' : 'true');
  };

  tocToggle.addEventListener('click', () => {
    if (!media.matches) return;
    const collapsed = tocPanel.classList.toggle('is-collapsed');
    tocToggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
  });

  tocEl.addEventListener('click', (event) => {
    const link = event.target.closest('a');
    if (!link || !media.matches) return;
    tocPanel.classList.add('is-collapsed');
    tocToggle.setAttribute('aria-expanded', 'false');
  });

  media.addEventListener?.('change', applyMode);
  applyMode();
}

function parseStory(markdown) {
  const lines = markdown.split(/\r?\n/);
  const intro = [];
  const acts = [];
  const references = [];
  let currentAct = null;
  let currentReference = null;
  let currentScene = null;
  let currentBlock = null;

  const pushBlock = () => {
    if (!currentBlock) return;
    const block = { ...currentBlock, content: currentBlock.content.join('\n').trim() };
    if (currentScene) currentScene.blocks.push(block);
    else if (currentReference) currentReference.blocks.push(block);
    else if (currentAct) currentAct.blocks.push(block);
    else intro.push(block);
    currentBlock = null;
  };

  for (const line of lines) {
    if (line.startsWith('# Act ') && !line.includes('Completion State')) {
      pushBlock();
      currentAct = {
        title: stripHashes(line),
        id: slugify(stripHashes(line)),
        blocks: [],
        scenes: []
      };
      acts.push(currentAct);
      currentReference = null;
      currentScene = null;
      continue;
    }

    if (line.startsWith('# Reference') || line.startsWith('# Appendix') || line.startsWith('# Asset') || line.startsWith('# Production')) {
      pushBlock();
      currentReference = {
        title: stripHashes(line),
        id: slugify(stripHashes(line)),
        blocks: []
      };
      references.push(currentReference);
      currentAct = null;
      currentScene = null;
      continue;
    }

    if (line.startsWith('# Act ') && line.includes('Completion State')) {
      pushBlock();
      currentReference = {
        title: stripHashes(line),
        id: slugify(stripHashes(line)),
        blocks: []
      };
      references.push(currentReference);
      currentAct = null;
      currentScene = null;
      continue;
    }

    if (line.startsWith('## Scene ') || line.startsWith('## Ending ')) {
      pushBlock();
      currentScene = {
        title: stripHashes(line),
        id: slugify(`${currentAct?.title || 'story'}-${stripHashes(line)}`),
        image: '',
        blocks: []
      };
      if (currentAct) currentAct.scenes.push(currentScene);
      else if (currentReference) currentReference.blocks.push({
        type: stripHashes(line),
        content: ''
      });
      continue;
    }

    if (line.startsWith('![') && currentScene && !currentScene.image) {
      const match = line.match(/!\[[^\]]*]\(([^)]+)\)/);
      if (match) currentScene.image = normalizeImagePath(match[1]);
      continue;
    }

    if (line.startsWith('### ')) {
      pushBlock();
      currentBlock = {
        type: stripHashes(line),
        content: []
      };
      continue;
    }

    if (line.startsWith('## ') && !line.startsWith('## Scene ')) {
      pushBlock();
      currentBlock = {
        type: stripHashes(line),
        content: []
      };
      continue;
    }

    if (line.trim() === '---') continue;

    if (!currentBlock) {
      currentBlock = {
        type: 'Overview',
        content: []
      };
    }
    currentBlock.content.push(line);
  }

  pushBlock();
  return { intro, acts, references };
}

function renderStory(model) {
  // The Mushroom Compendium markdown section is parsed as a "reference" by
  // parseStory, but it gets a dedicated visual treatment (image-led cards)
  // and we insert it directly under the Character Identity Cards. Pull it
  // out of the references list so it doesn't render twice.
  const compendiumRef = model.references.find((ref) => /mushroom compendium/i.test(ref.title));
  const filteredRefs = model.references.filter((ref) => ref !== compendiumRef);

  const html = [
    renderTitlePage(),
    renderIntro(model.intro),
    renderMapPage(),
    renderCharacterIdentityPage(),
    renderMushroomCompendiumPage(compendiumRef),
    ...model.acts.map(renderAct),
    ...filteredRefs.map(renderReference)
  ].filter(Boolean).join('');

  storyEl.innerHTML = html;

  // Build a single TOC where every parent that has children is a `.toc-group`.
  // The scroll-spy expands only the group that contains the currently-active
  // section so the menu stays compact while reading. Unused legacy pill tabs
  // and the "Story Acts" label have been removed in favor of a single flat
  // text-based menu.
  const tocParts = [];
  tocParts.push(tocLeaf('title-card', 'Title Card'));
  tocParts.push(tocLeaf('hollowfen-map', 'Hollowfen Map'));

  // Character Identity Cards — expandable, one child per character
  tocParts.push(tocGroup(
    'character-identities',
    'Character Identity Cards',
    CHARACTER_IDENTITIES.map((c) => ({ id: `character-${c.id}`, label: c.name }))
  ));

  // Mushroom Compendium — expandable, one child per species in journal order
  tocParts.push(tocGroup(
    'mushroom-compendium',
    'Mushroom Compendium',
    MUSHROOM_COMPENDIUM_ORDER
      .map((entry) => MUSHROOM_INFO[entry.id] ? { id: `mushroom-${entry.id}`, label: MUSHROOM_INFO[entry.id].commonName } : null)
      .filter(Boolean)
  ));

  // Each act — expandable, one child per scene
  for (const act of model.acts) {
    tocParts.push(tocGroup(
      act.id,
      act.title,
      act.scenes.map((scene) => ({ id: scene.id, label: cleanSceneTitle(scene.title) }))
    ));
  }

  // Reference pages — flat leaves at the bottom
  for (const ref of filteredRefs) {
    tocParts.push(tocLeaf(ref.id, ref.title));
  }

  tocEl.innerHTML = tocParts.join('');
}

function tocLeaf(id, label) {
  return `<a class="toc-link toc-link-act" href="#${id}" data-section-link="${id}">${escapeHtml(label)}</a>`;
}

function tocGroup(parentId, parentLabel, children) {
  const childIds = children.map((c) => c.id);
  const childMarkup = children.map((c) =>
    `<a class="toc-link toc-link-scene" href="#${c.id}" data-section-link="${c.id}">${escapeHtml(c.label)}</a>`
  ).join('');
  return `
    <div class="toc-group" data-toc-group="${parentId}" data-toc-children="${childIds.join(',')}">
      <a class="toc-link toc-link-act toc-group-header" href="#${parentId}" data-section-link="${parentId}">${escapeHtml(parentLabel)}</a>
      <div class="toc-group-children">${childMarkup}</div>
    </div>
  `;
}

function renderMapPage() {
  return `
    <section id="hollowfen-map" class="map-page">
      <div class="map-page-header">
        <div>
          <p class="eyebrow">World Reference</p>
          <h2>Hollowfen Map</h2>
          <p>A stylized storybook map created from the in-game layout, showing the village, woods, roads, and major story locations.</p>
        </div>
      </div>
      <figure class="story-map-art-plate">
        <div class="story-map-stage story-map-stage-image">
          <img class="story-map-image" src="/story/hollowfen-stylized-map.png" alt="Illustrated map of Hollowfen with labeled locations">
        </div>
      </figure>
    </section>
  `;
}

function renderCharacterIdentityPage() {
  return `
    <section id="character-identities" class="character-page">
      <div class="character-page-header">
        <p class="eyebrow">Visual Development</p>
        <h2>Character Visual Identity Cards</h2>
        <p>These cards collect the current canon art direction for each character or character group. The story-card thumbnails are the visual continuity anchors; the model-sheet notes define the turnarounds and prop views still needed for production.</p>
      </div>
      <div class="character-grid">
        ${CHARACTER_IDENTITIES.map(renderCharacterIdentityCard).join('')}
      </div>
    </section>
  `;
}

function renderCharacterIdentityCard(character) {
  const sheetImage = character.modelSheet || character.heroImage;
  return `
    <article class="character-card" id="character-${character.id}">
      <div class="character-card-media">
        <img class="character-card-hero" src="${escapeHtml(sheetImage)}" alt="${escapeHtml(character.name)} character sheet">
      </div>
      <div class="character-card-body">
        <div class="character-card-summary">
          <div>
            <div class="character-card-kicker">${escapeHtml(character.priority)}</div>
            <h3>${escapeHtml(character.name)}</h3>
            <p class="character-role">${escapeHtml(character.role)} · ${escapeHtml(character.age)}</p>
          </div>
          <div class="character-swatches" aria-label="${escapeHtml(character.name)} palette">
            ${character.palette.map((color, index) => `<span style="--swatch:${paletteColor(color, index)}" title="${escapeHtml(color)}"></span>`).join('')}
          </div>
        </div>

        <div class="character-card-section character-silhouette">
          <h4>Visual Read</h4>
          <p>${escapeHtml(character.silhouette)}</p>
        </div>

        <div class="character-card-columns">
          <div class="character-card-section">
            <h4>Props</h4>
            <ul>${character.props.map((item) => `<li>${escapeHtml(item)}</li>`).join('')}</ul>
          </div>
          <div class="character-card-section">
            <h4>Views Needed</h4>
            <ul>${character.views.map((item) => `<li>${escapeHtml(item)}</li>`).join('')}</ul>
          </div>
        </div>

        <div class="character-card-section character-model-notes">
          <h4>3D Model Notes</h4>
          <p>${escapeHtml(character.modelNotes)}</p>
        </div>
      </div>
    </article>
  `;
}

function renderMushroomCompendiumPage(compendiumRef) {
  // Pull Compendium Function + Tier Notes + Edibility Legend from the markdown
  // so writers can edit copy in docs/story.md without touching JS. Only the
  // grid + table are generated programmatically from the species data.
  const blocks = compendiumRef ? compendiumRef.blocks : [];
  const intro = blocks.find((b) => /compendium function/i.test(b.type));
  const tierNotes = blocks.find((b) => /tier notes/i.test(b.type));
  const legend = blocks.find((b) => /edibility legend/i.test(b.type));
  const indexBlock = blocks.find((b) => /compendium index/i.test(b.type));

  const cards = MUSHROOM_COMPENDIUM_ORDER.map((entry, i) => {
    const info = MUSHROOM_INFO[entry.id];
    if (!info) return '';
    return renderMushroomCard(info, entry, i + 1);
  }).join('');

  return `
    <section id="mushroom-compendium" class="mushroom-page">
      <div class="mushroom-page-header">
        <p class="eyebrow">Foraging System</p>
        <h2>Mushroom Compendium</h2>
        <p>Sixteen species — four per act. The journal fills out one mushroom at a time, and the village's recovery is measured by how many entries are inked in. Every species below is mapped to at least one canonical mission.</p>
        ${intro ? `<div class="prose mushroom-intro">${markdownToHtml(intro.content)}</div>` : ''}
      </div>

      <div class="mushroom-grid">
        ${cards}
      </div>

      ${indexBlock ? `
        <div class="mushroom-index-block">
          <h3>Mission Index</h3>
          <div class="prose">${markdownToHtml(indexBlock.content)}</div>
        </div>
      ` : ''}

      ${(tierNotes || legend) ? `
        <div class="mushroom-meta">
          ${tierNotes ? `
            <div class="mushroom-meta-block">
              <h3>Tier Notes</h3>
              <div class="prose">${markdownToHtml(tierNotes.content)}</div>
            </div>
          ` : ''}
          ${legend ? `
            <div class="mushroom-meta-block">
              <h3>Edibility Legend</h3>
              <div class="prose">${markdownToHtml(legend.content)}</div>
            </div>
          ` : ''}
        </div>
      ` : ''}
    </section>
  `;
}

function renderMushroomCard(info, entry, ordinal) {
  const edibilityClass = `edibility-${(info.edibility || 'unknown').replace(/\s+/g, '-')}`;
  return `
    <article class="mushroom-card ${edibilityClass}" id="mushroom-${entry.id}">
      <div class="mushroom-card-media">
        <span class="mushroom-card-ordinal">${String(ordinal).padStart(2, '0')}</span>
        <img class="mushroom-card-photo" src="${escapeHtml(info.photoUrl)}" alt="${escapeHtml(info.commonName)}" loading="lazy">
        <span class="mushroom-card-edibility">${escapeHtml(info.edibilityLabel || info.edibility || '')}</span>
      </div>
      <div class="mushroom-card-body">
        <header class="mushroom-card-head">
          <div>
            <div class="mushroom-card-kicker">${escapeHtml(entry.act)} · ${escapeHtml(entry.tier)}</div>
            <h3>${escapeHtml(info.commonName)}</h3>
            <p class="mushroom-card-latin"><em>${escapeHtml(info.latinName)}</em></p>
          </div>
        </header>

        <p class="mushroom-card-description">${escapeHtml(info.description)}</p>

        <div class="mushroom-card-mission">
          <span class="mushroom-card-mission-label">Mission</span>
          <span class="mushroom-card-mission-id"><code>${escapeHtml(entry.missionId)}</code></span>
          <span class="mushroom-card-mission-title">${escapeHtml(entry.missionTitle)}</span>
        </div>

        <div class="mushroom-card-roles">
          <div><span>Teacher</span><strong>${escapeHtml(entry.teacher)}</strong></div>
          <div><span>Buyer / Use</span><strong>${escapeHtml(entry.buyer)}</strong></div>
        </div>

        <div class="mushroom-card-section">
          <h4>ID Features</h4>
          <ul>${(info.idFeatures || []).map((f) => `<li>${escapeHtml(f)}</li>`).join('')}</ul>
        </div>

        <div class="mushroom-card-columns">
          <div class="mushroom-card-section">
            <h4>Habitat</h4>
            <p>${escapeHtml(info.habitat || '—')}</p>
          </div>
          <div class="mushroom-card-section">
            <h4>Season</h4>
            <p>${escapeHtml(info.season || '—')}</p>
          </div>
        </div>

        ${info.lookalikes ? `
          <div class="mushroom-card-section mushroom-card-warning">
            <h4>Lookalikes</h4>
            <p>${escapeHtml(info.lookalikes)}</p>
          </div>
        ` : ''}

        ${info.notes ? `
          <div class="mushroom-card-section mushroom-card-notes">
            <h4>Notes</h4>
            <p>${escapeHtml(info.notes)}</p>
          </div>
        ` : ''}
      </div>
    </article>
  `;
}

function renderTitlePage() {
  return `
    <section id="title-card" class="title-page">
      <img src="/story/cards/main-menu-wren.png" alt="Wren foraging in the Edge Woods">
      <div class="title-page-copy">
        <p class="eyebrow">Story and Game Design Book</p>
        <h2>Hollowfen</h2>
        <p>The Failing Village</p>
        <nav class="title-page-tabs" aria-label="Story book quick links">
          <a href="#hollowfen-map">Map</a>
          <a href="#character-identities">Characters</a>
          <a href="#mushroom-compendium">Mushrooms</a>
          <a href="#act-i-arrival">Story</a>
        </nav>
      </div>
    </section>
  `;
}

function paletteColor(name, index) {
  const known = {
    'warm linen': '#d8c6aa',
    'deep rust': '#7b3f26',
    'olive grey': '#59614d',
    'dark leather': '#3d2418',
    'mushroom gold': '#c69b42',
    'warm hearth brown': '#684028',
    'faded cream linen': '#d9ccb3',
    'stained apron grey': '#8c867a',
    'old oak': '#6c4a2f',
    'beer amber': '#b1742e',
    'flour white': '#efe5d0',
    'smoked umber': '#4b3225',
    'kerchief red-brown': '#833f2c',
    'copper pot': '#a95f33',
    'stew gold': '#c2974e',
    'garden black': '#1e211d',
    'weathered grey': '#8a8a7d',
    'dry sage': '#8a936d',
    'seed-paper tan': '#c6b58d',
    'dried herb green': '#596a45',
    'homespun brown': '#75523a',
    'mud grey': '#6f6960',
    'washed linen': '#d6c7ad',
    'candle amber': '#c78b38',
    'young birch green': '#758854',
    'forge black': '#171512',
    'coal grey': '#41403d',
    'leather brown': '#6a4128',
    'iron blue': '#46535c',
    'fire orange': '#c4632c',
    'drab grey-brown': '#655b50',
    'paper cream': '#d9ceb6',
    'ink black': '#181611',
    'cold pewter': '#7d8586',
    'worn leather': '#60412c',
    'good dark wool': '#2b2c2c',
    'brass clasp': '#b58a3b',
    'wagon leather': '#6b432a',
    'ledger tan': '#c0ab82',
    'wine red': '#683033',
    'wet bark': '#3c332c',
    'soft charcoal': '#31302d',
    'moss grey': '#6a725e',
    'faded umber': '#78543c',
    'cold cream': '#d8d0bd',
    'cassock black': '#171717',
    'aged linen': '#d7cbb6',
    'chapel stone': '#8b8a80',
    'old paper brown': '#b49b73',
    'apple-leaf green': '#667a4d',
    'old ink': '#24231f',
    'ledger brown': '#5c3e2a',
    'dust grey': '#777068',
    'muted green': '#647357',
    'lantern amber': '#d09a3a',
    'black wool': '#1f1d1e',
    'wine velvet': '#4d2029',
    'polished oak': '#714c2e',
    'old gold': '#ae8438',
    'seal wax red': '#7b2421',
    'quilt blue-grey': '#6a7480',
    'bone white': '#ddd5c2',
    'soup brown': '#8a5c32',
    'tonic green': '#8aa56d',
    'worn wool': '#6e6255',
    'boot brown': '#4d3526',
    'ashen hearth': '#827a70',
    'washed blue': '#6f8191',
    'dull brass': '#a07c3b',
    'field brown': '#725237',
    'linen cream': '#e1d3b8',
    'dull green': '#68714e',
    'weathered grey': '#86837a'
  };
  return known[name] || ['#7b3f26', '#536548', '#d9bd6d', '#74685d', '#241b16'][index % 5];
}

function renderIntro(blocks) {
  if (!blocks.length) return '';
  return `
    <section class="doc-intro">
      <p class="eyebrow">Narrative Design</p>
      <h2>Hollowfen Story Book</h2>
      ${blocks.map(renderBlock).join('')}
    </section>
  `;
}

function renderAct(act) {
  if (!act) return '';
  const actName = act.title.split(' - ')[0] || act.title;
  return `
    <section id="${act.id}" class="act-panel">
      <p class="eyebrow">${escapeHtml(actName)}</p>
      <h2>${escapeHtml(act.title.replace(/^Act [IVX]+ - /, ''))}</h2>
      ${act.blocks.map(renderBlock).join('')}
    </section>
    ${act.scenes.map(renderScene).join('')}
  `;
}

function renderScene(scene) {
  const narrative = scene.blocks.find((block) => block.type === 'Narrative Passage');
  const narrativeText = narrative ? markdownToPlainText(narrative.content) : '';
  return `
    <section id="${scene.id}" class="scene-card">
      ${scene.image ? `<img class="scene-hero" src="${scene.image}" alt="${escapeHtml(scene.title)}">` : ''}
      <div class="scene-body">
        <p class="scene-kicker">${escapeHtml(scene.title.split(' - ')[0] || 'Scene')}</p>
        <div class="scene-title-row">
          <h2>${escapeHtml(cleanSceneTitle(scene.title))}</h2>
          ${narrativeText ? listenButton(narrativeText, 'Read narrative') : ''}
        </div>
        ${scene.blocks.map(renderBlock).join('')}
      </div>
    </section>
  `;
}

function renderReference(reference) {
  if (!reference) return '';
  return `
    <section id="${reference.id}" class="completion-card reference-card">
      <p class="eyebrow">Reference</p>
      <h2>${escapeHtml(reference.title)}</h2>
      ${reference.blocks.map(renderBlock).join('')}
    </section>
  `;
}

function cleanSceneTitle(title) {
  return title
    .replace(/^Scene \d+\s*(?:-|--|—)\s*/, '')
    .replace(/^Scene \d+\s*/, '')
    .replace(/^Ending [A-Z]\s*(?:-|--|—)\s*/, '');
}

function renderBlock(block) {
  const isNarrative = block.type === 'Narrative Passage';
  const text = markdownToPlainText(block.content);
  return `
    <section class="section-block ${isNarrative ? 'narrative-block' : ''}">
      <div class="section-heading">
        <h3>${escapeHtml(block.type)}</h3>
        ${isNarrative ? listenButton(text, 'Listen') : ''}
      </div>
      <div class="prose ${isNarrative ? 'narrative' : ''}">
        ${markdownToHtml(block.content)}
      </div>
    </section>
  `;
}

function listenButton(text, label) {
  return `
    <button class="listen-button" type="button" data-speak="${escapeHtml(text)}" title="${escapeHtml(label)}">
      <span aria-hidden="true">▶</span>
      <span>${escapeHtml(label)}</span>
    </button>
  `;
}

function bindSpeechControls() {
  document.querySelectorAll('[data-speak]').forEach((button) => {
    button.addEventListener('click', () => {
      const text = button.dataset.speak || '';
      if (speechState.activeButton === button && window.speechSynthesis.speaking) {
        stopSpeech();
        return;
      }
      speakText(text, button);
    });
  });

  stopButton.addEventListener('click', stopSpeech);
}

function setupNarratorControls() {
  if (!('speechSynthesis' in window)) {
    voiceSelect.disabled = true;
    rateInput.disabled = true;
    return;
  }

  rateInput.value = String(speechState.rate);
  rateValue.textContent = `${speechState.rate.toFixed(2)}x`;
  rateInput.addEventListener('input', () => {
    speechState.rate = Number(rateInput.value);
    rateValue.textContent = `${speechState.rate.toFixed(2)}x`;
    localStorage.setItem(RATE_STORAGE_KEY, String(speechState.rate));
  });

  voiceSelect.addEventListener('change', () => {
    speechState.selectedVoiceURI = voiceSelect.value;
    localStorage.setItem(VOICE_STORAGE_KEY, speechState.selectedVoiceURI);
    if (window.speechSynthesis.speaking) {
      const activeButton = speechState.activeButton;
      const activeText = activeButton?.dataset.speak || '';
      if (activeButton && activeText) speakText(activeText, activeButton);
    }
  });

  populateVoiceSelect();
  window.speechSynthesis.onvoiceschanged = populateVoiceSelect;
}

function setupScrollSpy() {
  const links = Array.from(document.querySelectorAll('[data-section-link]'));
  const sections = links
    .map((link) => ({
      id: link.dataset.sectionLink,
      link,
      el: document.getElementById(link.dataset.sectionLink)
    }))
    .filter((entry) => entry.el);

  if (!sections.length) return;

  // Build a quick lookup of which TOC group "owns" each section id, so we
  // can expand the right group when scrolling into a child section. A group
  // owns its header id AND every id listed in data-toc-children.
  const groups = Array.from(document.querySelectorAll('.toc-group'));
  const groupByOwnedId = new Map();
  for (const group of groups) {
    const header = group.dataset.tocGroup;
    const childIds = (group.dataset.tocChildren || '').split(',').filter(Boolean);
    if (header) groupByOwnedId.set(header, group);
    for (const id of childIds) groupByOwnedId.set(id, group);
  }

  const setActive = (id) => {
    for (const entry of sections) {
      const active = entry.id === id;
      entry.link.classList.toggle('is-active', active);
      if (active) {
        entry.link.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      }
    }
    // Expand only the group that contains the active section; collapse the rest.
    const activeGroup = groupByOwnedId.get(id) || null;
    for (const group of groups) {
      group.classList.toggle('is-expanded', group === activeGroup);
    }
  };

  const observer = new IntersectionObserver((entries) => {
    const visible = entries
      .filter((entry) => entry.isIntersecting)
      .sort((a, b) => Math.abs(a.boundingClientRect.top) - Math.abs(b.boundingClientRect.top));
    if (visible[0]) setActive(visible[0].target.id);
  }, {
    root: null,
    rootMargin: '-18% 0px -68% 0px',
    threshold: [0, 0.1, 0.25, 0.5]
  });

  sections.forEach((entry) => observer.observe(entry.el));
  setActive(sections[0].id);
}

function populateVoiceSelect() {
  speechState.voices = window.speechSynthesis.getVoices();
  const englishVoices = speechState.voices
    .filter((voice) => voice.lang?.toLowerCase().startsWith('en'))
    .sort((a, b) => scoreVoice(b) - scoreVoice(a) || a.name.localeCompare(b.name));

  const preferred = chooseBestVoice(englishVoices);
  const options = [
    `<option value="auto">Best available${preferred ? ` (${escapeHtml(preferred.name)})` : ''}</option>`,
    ...englishVoices.map((voice) => {
      const tag = voice.localService ? 'local' : 'cloud';
      return `<option value="${escapeHtml(voice.voiceURI)}">${escapeHtml(voice.name)} - ${escapeHtml(voice.lang)} (${tag})</option>`;
    })
  ];

  voiceSelect.innerHTML = options.join('');
  const hasStoredVoice = englishVoices.some((voice) => voice.voiceURI === speechState.selectedVoiceURI);
  voiceSelect.value = hasStoredVoice ? speechState.selectedVoiceURI : 'auto';
  if (!hasStoredVoice) speechState.selectedVoiceURI = 'auto';
}

function speakText(text, button) {
  if (!('speechSynthesis' in window)) {
    alert('Your browser does not support built-in text-to-speech.');
    return;
  }

  stopSpeech();
  speechState.chunks = chunkText(text);
  speechState.index = 0;
  speechState.activeButton = button;
  speechState.stopped = false;
  button.classList.add('is-speaking');
  button.querySelector('span:last-child').textContent = 'Stop';
  speakNextChunk();
}

function speakNextChunk() {
  if (speechState.stopped || speechState.index >= speechState.chunks.length) {
    resetActiveButton();
    return;
  }

  const utterance = new SpeechSynthesisUtterance(speechState.chunks[speechState.index]);
  const voice = getNarratorVoice();
  if (voice) utterance.voice = voice;
  utterance.lang = voice?.lang || 'en-US';
  utterance.rate = speechState.rate;
  utterance.pitch = 0.94;
  utterance.volume = 1;
  utterance.onend = () => {
    speechState.index += 1;
    const pause = speechState.index < speechState.chunks.length ? 170 : 0;
    window.setTimeout(speakNextChunk, pause);
  };
  utterance.onerror = resetActiveButton;
  window.speechSynthesis.speak(utterance);
}

function stopSpeech() {
  speechState.stopped = true;
  if ('speechSynthesis' in window) window.speechSynthesis.cancel();
  resetActiveButton();
}

function resetActiveButton() {
  if (speechState.activeButton) {
    speechState.activeButton.classList.remove('is-speaking');
    const label = speechState.activeButton.closest('.scene-title-row') ? 'Read narrative' : 'Listen';
    speechState.activeButton.querySelector('span:last-child').textContent = label;
  }
  speechState.activeButton = null;
}

function chunkText(text) {
  const cleaned = text
    .replace(/[“”]/g, '"')
    .replace(/[‘’]/g, "'")
    .replace(/—/g, ', ')
    .replace(/;/g, '.')
    .split(/(?<=[.!?])\s+/)
    .filter(Boolean);
  const chunks = [];
  let current = '';
  for (const sentence of cleaned) {
    const normalized = sentence.replace(/\s+/g, ' ').trim();
    if (!normalized) continue;
    if ((current + ' ' + normalized).trim().length > 520 && current) {
      chunks.push(current.trim());
      current = normalized;
    } else {
      current = `${current} ${normalized}`.trim();
    }
  }
  if (current) chunks.push(current.trim());
  return chunks;
}

function getNarratorVoice() {
  const voices = speechState.voices.length ? speechState.voices : window.speechSynthesis.getVoices();
  if (speechState.selectedVoiceURI !== 'auto') {
    const selected = voices.find((voice) => voice.voiceURI === speechState.selectedVoiceURI);
    if (selected) return selected;
  }
  return chooseBestVoice(voices.filter((voice) => voice.lang?.toLowerCase().startsWith('en')));
}

function chooseBestVoice(voices) {
  if (!voices.length) return null;
  return voices.slice().sort((a, b) => scoreVoice(b) - scoreVoice(a) || a.name.localeCompare(b.name))[0];
}

function scoreVoice(voice) {
  const name = `${voice.name} ${voice.voiceURI}`.toLowerCase();
  let score = 0;
  if (voice.lang?.toLowerCase() === 'en-us') score += 14;
  if (voice.lang?.toLowerCase().startsWith('en-')) score += 10;
  if (voice.default) score += 4;
  if (voice.localService) score += 2;
  if (/(enhanced|premium|natural|neural|online)/.test(name)) score += 16;
  if (/(ava|samantha|victoria|susan|moira|karen|tessa|serena|kate|jenny|aria)/.test(name)) score += 14;
  if (/(alex|daniel|oliver|jamie|fred)/.test(name)) score += 8;
  if (/google/.test(name)) score += 6;
  if (/microsoft/.test(name)) score += 8;
  if (/(compact|novelty|whisper|zarvox|trinoids|bells|boing|bubbles|cellos|hysterical|pipe|organ)/.test(name)) score -= 40;
  return score;
}

function markdownToHtml(markdown) {
  const lines = markdown.split('\n');
  const parts = [];
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];
    if (!line.trim()) {
      i += 1;
      continue;
    }

    if (isTableStart(lines, i)) {
      const tableLines = [];
      while (i < lines.length && lines[i].trim().startsWith('|')) {
        tableLines.push(lines[i]);
        i += 1;
      }
      parts.push(renderTable(tableLines));
      continue;
    }

    if (line.trim().startsWith('- ')) {
      const items = [];
      while (i < lines.length && lines[i].trim().startsWith('- ')) {
        items.push(lines[i].trim().slice(2));
        i += 1;
      }
      parts.push(`<ul>${items.map((item) => `<li>${inlineMarkdown(item)}</li>`).join('')}</ul>`);
      continue;
    }

    const paragraph = [];
    while (
      i < lines.length &&
      lines[i].trim() &&
      !lines[i].trim().startsWith('- ') &&
      !isTableStart(lines, i)
    ) {
      paragraph.push(lines[i]);
      i += 1;
    }
    parts.push(`<p>${inlineMarkdown(paragraph.join(' '))}</p>`);
  }

  return parts.join('');
}

function renderTable(lines) {
  const rows = lines
    .filter((line, index) => index !== 1 || !/^\|\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$/.test(line))
    .map((line) => line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map((cell) => cell.trim()));

  if (!rows.length) return '';
  const [head, ...body] = rows;
  return `
    <div class="table-wrap">
      <table>
        <thead><tr>${head.map((cell) => `<th>${inlineMarkdown(cell)}</th>`).join('')}</tr></thead>
        <tbody>${body.map((row) => `<tr>${row.map((cell) => `<td>${inlineMarkdown(cell)}</td>`).join('')}</tr>`).join('')}</tbody>
      </table>
    </div>
  `;
}

function isTableStart(lines, index) {
  return lines[index]?.trim().startsWith('|') && lines[index + 1]?.includes('---');
}

function inlineMarkdown(value) {
  return escapeHtml(value)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\*([^*]+)\*/g, '<em>$1</em>');
}

function markdownToPlainText(markdown) {
  return markdown
    .replace(/\|/g, ' ')
    .replace(/`/g, '')
    .replace(/\*\*/g, '')
    .replace(/\*/g, '')
    .replace(/#+\s/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function stripHashes(line) {
  return line.replace(/^#+\s*/, '').trim();
}

function slugify(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
}

function normalizeImagePath(path) {
  return path.replace('../public', '').replace(/^public/, '');
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
