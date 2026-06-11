// Cinematic narration beats — full-screen black overlay with serif caption
// fades. Used for scene introductions where prose from docs/story.md sets
// the mood before gameplay begins. Lines are quoted verbatim from the
// narrative passages so the on-screen text and the story doc stay locked.
//
// Each beat is gated by a persistent localStorage flag so reloads don't
// replay it. Reset via clearNarrationFlag(id) for re-testing.

const FLAG_KEY_PREFIX = 'hollowfen.narration.';

function wasShown(id) {
  try { return localStorage.getItem(FLAG_KEY_PREFIX + id) === '1'; }
  catch { return false; }
}

function markShown(id) {
  try { localStorage.setItem(FLAG_KEY_PREFIX + id, '1'); } catch {}
}

let overlay = null;
let textEl = null;
function ensureOverlay() {
  if (overlay) return;
  overlay = document.createElement('div');
  overlay.id = 'narration-root';
  overlay.style.cssText = [
    'position:fixed',
    'inset:0',
    'background:#05060a',
    'z-index:250',
    'display:none',
    'align-items:center',
    'justify-content:center',
    'padding:0 12vw',
    'opacity:0',
    'pointer-events:none',
    'transition:opacity 700ms ease'
  ].join(';');

  textEl = document.createElement('div');
  textEl.id = 'narration-text';
  textEl.style.cssText = [
    'font-family:"Cormorant Garamond","Iowan Old Style","Georgia",serif',
    'font-size:30px',
    'line-height:1.55',
    'color:#efe4cc',
    'font-style:italic',
    'letter-spacing:0.4px',
    'text-align:center',
    'max-width:760px',
    'opacity:0',
    'transition:opacity 1100ms ease'
  ].join(';');

  overlay.appendChild(textEl);
  document.body.appendChild(overlay);
}

function showOverlay() {
  ensureOverlay();
  overlay.style.display = 'flex';
  // Force layout flush so the display:flex commits before opacity transitions.
  void overlay.offsetHeight;
  return new Promise((res) => {
    overlay.style.opacity = '1';
    setTimeout(res, 750);
  });
}

function hideOverlay() {
  return new Promise((res) => {
    overlay.style.opacity = '0';
    setTimeout(() => {
      overlay.style.display = 'none';
      res();
    }, 750);
  });
}

function showCaption(text, holdMs = 4500) {
  return new Promise((res) => {
    textEl.textContent = text;
    textEl.style.opacity = '0';
    // Cross-fade: brief gap, then fade in
    setTimeout(() => {
      textEl.style.opacity = '1';
      setTimeout(() => {
        textEl.style.opacity = '0';
        setTimeout(res, 1100);
      }, holdMs);
    }, 200);
  });
}

// ---------------- Public scenes ----------------

// Act I, Scene 1 — Homecoming opener. Two captions pulled verbatim from the
// narrative passage in docs/story.md. Plays once per save (localStorage).
export async function playHomecomingIntro({ force = false } = {}) {
  const id = 'homecoming.intro';
  if (!force && wasShown(id)) return false;

  await showOverlay();
  await showCaption('It had been three years since Wren Tobin walked the east road into Hollowfen.', 4800);
  await showCaption('The village did not greet her.', 3500);
  await hideOverlay();
  markShown(id);
  return true;
}

// Force-reset for re-testing (call from console).
export function clearNarrationFlag(id) {
  try { localStorage.removeItem(FLAG_KEY_PREFIX + id); } catch {}
}

// Whether a narration is currently on-screen — used by main.js to freeze
// player input + mouse-look.
export function isNarrationActive() {
  return overlay?.style?.display === 'flex';
}
