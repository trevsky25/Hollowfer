// Cinematic dialog overlay. Driven by data from src/data/Dialogs.js.
// main.js wires callbacks for animation triggers, outcomes, and per-line
// camera framing. This module owns presentation only — letterbox bars,
// vignette, typewriter text reveal, dialog panel, and the speaker-change
// signal that the cinematic camera listens to.
//
// Inputs:
//   - dialogs: the DIALOGS map
//   - onTrigger(trigger): animation cue tied to a specific line
//   - onOutcome(outcome): one-shot when the dialog ends naturally
//   - onLineChange({ speaker, lineIndex, line, def }): fires on start + each advance
//   - onClose(): when the dialog has fully faded out
//
// Public API:
//   start(id), advance(), close(), isOpen(), isAdvanceable()

// Speaker accent colors tuned for ink-on-paper readability — saturated
// enough to punch against the parchment, dark enough not to clash with
// the handwritten body text (#2a1d0e).
const SPEAKER_COLOR = {
  Bram: '#7a4a1a',     // walnut ink
  Wren: '#5b3a6a',     // dark plum
  Marra: '#8a3a2a',    // brick red
  default: '#3a2810'
};

// Characters per second for the typewriter reveal. Tuned to a comfortable
// reading pace — fast enough that you don't wait, slow enough that the line
// can be read along with the reveal. Punctuation adds natural beats.
const TYPEWRITER_CPS = 32;
const PAUSE_AFTER = { '.': 240, '!': 260, '?': 260, ',': 90, ';': 140, ':': 120, '\n': 280 };

export class DialogController {
  constructor({ dialogs, onTrigger, onOutcome, onLineChange, onClose }) {
    this.dialogs = dialogs || {};
    this.onTrigger = onTrigger || (() => {});
    this.onOutcome = onOutcome || (() => {});
    this.onLineChange = onLineChange || (() => {});
    this.onClose = onClose || (() => {});

    this.active = null;       // { id, def, lineIndex }
    this.fadingOut = false;
    this._typeTimer = null;
    this._typeFullText = '';
    this._typeRevealedCount = 0;
    // Pending close timers — cancelled when a chained dialog starts so
    // the letterbox/vignette/state don't get torn down mid-conversation.
    this._closeTimers = [];

    this._buildDom();
  }

  _buildDom() {
    // Letterbox bars (top + bottom). Slide in when dialog opens, slide out
    // when it closes. Combined with vignette they cue "scene mode."
    const barTop = document.createElement('div');
    barTop.id = 'dialog-bar-top';
    barTop.style.cssText = [
      'position:fixed',
      'top:0',
      'left:0',
      'right:0',
      'height:0',
      'background:#05060a',
      'z-index:190',
      'pointer-events:none',
      'transition:height 600ms cubic-bezier(0.22, 0.61, 0.36, 1)'
    ].join(';');

    const barBottom = document.createElement('div');
    barBottom.id = 'dialog-bar-bottom';
    barBottom.style.cssText = [
      'position:fixed',
      'bottom:0',
      'left:0',
      'right:0',
      'height:0',
      'background:#05060a',
      'z-index:190',
      'pointer-events:none',
      'transition:height 600ms cubic-bezier(0.22, 0.61, 0.36, 1)'
    ].join(';');

    // Vignette over the playable middle area. Subtle radial darkening that
    // pulls focus to the center of the frame.
    const vignette = document.createElement('div');
    vignette.id = 'dialog-vignette';
    vignette.style.cssText = [
      'position:fixed',
      'inset:0',
      'pointer-events:none',
      'opacity:0',
      'transition:opacity 600ms ease',
      'background:radial-gradient(ellipse at center, rgba(0,0,0,0) 38%, rgba(0,0,0,0.55) 78%, rgba(0,0,0,0.78) 100%)',
      'z-index:191'
    ].join(';');

    const root = document.createElement('div');
    root.id = 'dialog-root';
    root.setAttribute('aria-live', 'polite');
    root.style.cssText = [
      'position:fixed',
      'left:0',
      'right:0',
      'bottom:0',
      'padding:0 0 64px',
      'display:none',
      'pointer-events:none',
      'z-index:200',
      'opacity:0',
      'transition:opacity 360ms ease 120ms'  // slight delay so letterbox lands first
    ].join(';');

    const panel = document.createElement('div');
    panel.id = 'dialog-panel';
    panel.style.cssText = [
      'max-width:840px',
      'margin:0 auto',
      // Parchment paper — same texture + ivory gradient as the journal pages,
      // with multiply blend so the texture darkens the ivory rather than
      // overlaying noise on top.
      "background:url('/ui/journal/journal-paper.webp'), linear-gradient(180deg, #fbf6e8 0%, #f3ead0 100%)",
      'background-size:cover, 100% 100%',
      'background-blend-mode:multiply, normal',
      'border:1px solid rgba(80,52,20,0.35)',
      'border-radius:5px',
      'padding:30px 44px 30px',
      // Outer shadow + a subtle inner top highlight so the paper looks lifted,
      // not just pasted on. Warm brown shadow rather than pure black.
      'box-shadow:0 26px 60px rgba(20,12,4,0.55), 0 0 0 1px rgba(60,40,18,0.18) inset, 0 1px 0 rgba(255,255,255,0.45) inset',
      'pointer-events:auto',
      'cursor:pointer',
      'font-family:"Walter Turncoat", "Bradley Hand", "Segoe Script", cursive',
      'color:#2a1d0e',
      'transform:translateY(12px)',
      'transition:transform 360ms cubic-bezier(0.22, 0.61, 0.36, 1), opacity 240ms ease',
      'position:relative',
      'overflow:hidden'
    ].join(';');
    panel.addEventListener('click', () => this.advance());

    // Subtle paper-grain noise on top of the texture (matches .journal-page::after).
    const grain = document.createElement('div');
    grain.style.cssText = [
      'position:absolute',
      'inset:0',
      'pointer-events:none',
      "background-image:url(\"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='220' height='220'><filter id='n'><feTurbulence type='fractalNoise' baseFrequency='1.1' numOctaves='2' stitchTiles='stitch'/><feColorMatrix values='0 0 0 0 0.32  0 0 0 0 0.22  0 0 0 0 0.10  0 0 0 0.16 0'/></filter><rect width='100%' height='100%' filter='url(%23n)'/></svg>\")",
      'opacity:0.18',
      'mix-blend-mode:multiply'
    ].join(';');
    panel.appendChild(grain);

    // Inner content sits above the grain layer.
    const inner = document.createElement('div');
    inner.style.cssText = 'position:relative;z-index:1';

    // Speaker bar — handwritten name with a thin ink accent line.
    const speakerRow = document.createElement('div');
    speakerRow.style.cssText = 'display:flex;align-items:baseline;gap:14px;margin-bottom:10px';

    const speakerAccent = document.createElement('div');
    speakerAccent.id = 'dialog-speaker-accent';
    speakerAccent.style.cssText = 'width:42px;height:2px;background:#7a4a1a;opacity:0.7;align-self:center';

    const speaker = document.createElement('div');
    speaker.id = 'dialog-speaker';
    speaker.style.cssText = [
      'font-family:"Walter Turncoat", "Bradley Hand", "Segoe Script", cursive',
      'font-size:22px',
      'letter-spacing:0.5px',
      'color:#7a4a1a',
      'transform:rotate(-0.5deg)',  // matches the journal heading tilt
      'transform-origin:left center'
    ].join(';');

    speakerRow.appendChild(speakerAccent);
    speakerRow.appendChild(speaker);

    const text = document.createElement('div');
    text.id = 'dialog-text';
    text.style.cssText = [
      'font-family:"Walter Turncoat", "Bradley Hand", "Segoe Script", cursive',
      'font-size:24px',
      'line-height:1.55',
      'min-height:92px',
      'letter-spacing:0.1px',
      'color:#2a1d0e',
      'white-space:pre-wrap'
    ].join(';');

    // Caret — a thin ink stroke instead of the blocky terminal pipe; reads as
    // a pen poised on the page.
    const caret = document.createElement('span');
    caret.id = 'dialog-caret';
    caret.textContent = '|';
    caret.style.cssText = [
      'display:inline-block',
      'margin-left:1px',
      'opacity:0.7',
      'animation:dialogCaretBlink 900ms step-end infinite',
      'color:#3a2810',
      'font-weight:400'
    ].join(';');

    const footer = document.createElement('div');
    footer.id = 'dialog-footer';
    footer.style.cssText = [
      'display:flex',
      'justify-content:space-between',
      'align-items:center',
      'margin-top:18px',
      'font-family:"Walter Turncoat", "Bradley Hand", cursive',
      'font-size:14px',
      'letter-spacing:0.5px',
      'color:rgba(58,40,16,0.7)'
    ].join(';');

    const progress = document.createElement('span');
    progress.id = 'dialog-progress';

    const hint = document.createElement('span');
    hint.id = 'dialog-hint';
    hint.innerHTML = '<b style="color:#3a2810">[Space]</b> or <b style="color:#3a2810">[E]</b> to continue';

    footer.appendChild(progress);
    footer.appendChild(hint);

    inner.appendChild(speakerRow);
    inner.appendChild(text);
    text.appendChild(caret);
    inner.appendChild(footer);
    panel.appendChild(inner);
    root.appendChild(panel);

    // Inject caret keyframes once.
    if (!document.getElementById('dialog-keyframes')) {
      const style = document.createElement('style');
      style.id = 'dialog-keyframes';
      style.textContent = `
        @keyframes dialogCaretBlink { 0%, 50% { opacity: 0.85 } 50.01%, 100% { opacity: 0 } }
      `;
      document.head.appendChild(style);
    }

    document.body.appendChild(barTop);
    document.body.appendChild(barBottom);
    document.body.appendChild(vignette);
    document.body.appendChild(root);

    this.dom = { root, panel, speaker, speakerAccent, text, caret, progress, hint, barTop, barBottom, vignette };
  }

  isOpen() { return !!this.active && !this.fadingOut; }
  isAdvanceable() { return this.isOpen() && !this._suspended; }

  // Hide the dialog overlay without touching its state — used when the
  // pause menu opens so the menu isn't covered. Resume restores visibility.
  suspend() {
    if (this._suspended) return;
    this._suspended = true;
    this.dom.root.style.visibility = 'hidden';
    this.dom.barTop.style.visibility = 'hidden';
    this.dom.barBottom.style.visibility = 'hidden';
    this.dom.vignette.style.visibility = 'hidden';
  }
  resume() {
    if (!this._suspended) return;
    this._suspended = false;
    this.dom.root.style.visibility = '';
    this.dom.barTop.style.visibility = '';
    this.dom.barBottom.style.visibility = '';
    this.dom.vignette.style.visibility = '';
  }

  start(id) {
    const def = this.dialogs[id];
    if (!def) {
      console.warn(`[Dialog] unknown id: ${id}`);
      return;
    }
    // If we were mid-fade-out (chained dialog), cancel the pending close
    // teardown so letterbox/vignette/active-state don't get clobbered.
    if (this._closeTimers.length) {
      this._closeTimers.forEach(clearTimeout);
      this._closeTimers = [];
    }
    this.active = { id, def, lineIndex: 0 };
    this.fadingOut = false;

    // Letterbox + vignette in (or stay in if already up from previous chain).
    this.dom.barTop.style.height = '12vh';
    this.dom.barBottom.style.height = '12vh';
    this.dom.vignette.style.opacity = '1';

    this.dom.root.style.display = 'block';
    void this.dom.root.offsetHeight;
    this.dom.root.style.opacity = '1';
    this.dom.panel.style.transform = 'translateY(0)';
    this.dom.panel.style.opacity = '1';

    this._renderCurrentLine();
    this._fireTriggersForLine(0);
    this._emitLineChange();
  }

  advance() {
    if (!this.active || this.fadingOut) return;
    // First press completes the typewriter; second press advances.
    if (this._typeTimer) {
      this._completeTypewriter();
      return;
    }
    const next = this.active.lineIndex + 1;
    if (next >= this.active.def.lines.length) {
      this._finishDialog();
      return;
    }
    this.active.lineIndex = next;
    this._renderCurrentLine();
    this._fireTriggersForLine(next);
    this._emitLineChange();
  }

  close() { this._finishDialog({ force: true }); }

  _renderCurrentLine() {
    const { def, lineIndex } = this.active;
    const line = def.lines[lineIndex];
    const speakerName = line.speaker || def.speaker || '';
    const color = SPEAKER_COLOR[speakerName] || SPEAKER_COLOR.default;
    this.dom.speaker.textContent = speakerName;
    this.dom.speaker.style.color = color;
    this.dom.speakerAccent.style.background = color;
    this.dom.progress.textContent = `${lineIndex + 1} / ${def.lines.length}`;
    this._startTypewriter(line.text);
  }

  _startTypewriter(fullText) {
    this._clearTypewriter();
    this._typeFullText = fullText;
    this._typeRevealedCount = 0;
    // Reset text content to caret so partial fills are appended.
    this.dom.text.textContent = '';
    this.dom.text.appendChild(this.dom.caret);
    this.dom.caret.style.display = 'inline-block';

    const stepMs = 1000 / TYPEWRITER_CPS;
    const tick = () => {
      if (this._typeRevealedCount >= fullText.length) {
        this._completeTypewriter();
        return;
      }
      const ch = fullText[this._typeRevealedCount];
      this._typeRevealedCount += 1;
      // Insert character before the caret span.
      this.dom.text.insertBefore(document.createTextNode(ch), this.dom.caret);
      const pause = PAUSE_AFTER[ch] || 0;
      this._typeTimer = setTimeout(tick, stepMs + pause);
    };
    this._typeTimer = setTimeout(tick, stepMs);
  }

  _completeTypewriter() {
    this._clearTypewriter();
    // Replace text node contents with full string, keep caret hidden.
    this.dom.text.textContent = this._typeFullText;
    this.dom.text.appendChild(this.dom.caret);
    this.dom.caret.style.display = 'none';
  }

  _clearTypewriter() {
    if (this._typeTimer) {
      clearTimeout(this._typeTimer);
      this._typeTimer = null;
    }
  }

  _fireTriggersForLine(lineIndex) {
    const triggers = this.active.def.triggers || [];
    for (const t of triggers) if (t.atLine === lineIndex) this.onTrigger(t);
  }

  _emitLineChange() {
    const { def, lineIndex } = this.active;
    const line = def.lines[lineIndex];
    this.onLineChange({
      speaker: line.speaker || def.speaker,
      shot: line.shot || 'wide',
      lineIndex,
      line,
      def
    });
  }

  _finishDialog({ force = false } = {}) {
    if (!this.active || this.fadingOut) return;
    const def = this.active.def;
    this._clearTypewriter();
    this.dom.caret.style.display = 'none';

    // Chain into the next dialog without tearing down the cinematic
    // (letterbox + vignette stay up; only the panel content fades).
    if (!force && def.outcome?.nextDialog && this.dialogs[def.outcome.nextDialog]) {
      this.onOutcome(def.outcome);
      const nextId = def.outcome.nextDialog;
      this.dom.panel.style.opacity = '0';
      this.dom.panel.style.transform = 'translateY(8px)';
      setTimeout(() => {
        this.start(nextId);
      }, 360);
      return;
    }

    this.fadingOut = true;

    // Final close — panel fades, letterbox retracts after a beat, dialog
    // unmounts. Timers are tracked so a late chain (or a fast re-open) can
    // cancel them and keep the cinematic alive.
    this.dom.root.style.opacity = '0';
    this.dom.panel.style.transform = 'translateY(12px)';
    const t1 = setTimeout(() => {
      this.dom.barTop.style.height = '0';
      this.dom.barBottom.style.height = '0';
      this.dom.vignette.style.opacity = '0';
    }, 120);
    const t2 = setTimeout(() => {
      this.dom.root.style.display = 'none';
      this.fadingOut = false;
      this.active = null;
      this._closeTimers = [];
      this.onClose();
    }, 720);
    this._closeTimers = [t1, t2];

    if (!force && def.outcome) this.onOutcome(def.outcome);
  }
}
