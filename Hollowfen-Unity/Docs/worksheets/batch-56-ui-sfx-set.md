# Batch 56 — UI SFX set (menu-ui-audit fix #1)

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-56` (pending)
**Audit item:** menu-ui-audit.md A1 (P0) — the only UI sound was a single page-transition click; no
per-control feedback. Signed off by Trevor ("all four, suggested order").

## What shipped
Extended `UISfx` (still fully procedural — no audio assets) from one click to a five-cue set, and
hooked each to the right interaction:

| Cue | When | Sound (synth) |
|-----|------|---------------|
| **Move** | EventSystem selection changes by player nav | soft high 2.3 kHz tick, ~35 ms |
| **Select** | screen push / replace (open/advance) | 1.7 kHz tap + 240 Hz thump (the old click) |
| **Back** | screen pop (close/retreat) | 760→420 Hz downward glide + thump |
| **Confirm** | ConfirmModal affirmative | two rising notes C5→G5, ~160 ms |
| **Error** | Submit on a non-interactable / Continue with no save | muted 160+168 Hz double-tone |

Hooks (all in `UIManager` unless noted):
- `Update()` watches `EventSystem.currentSelectedGameObject`; plays Move on change, gated by
  `!_transitioning` + a post-transition cooldown; `SetFocusToTop` seeds `_lastSelected` so the
  programmatic focus after a transition is silent.
- `TransitionRoutine` sound block: push/replace → `Select`, pop → `Back` (replaces the flat `Click`);
  still quiet on boot-open + loading transitions; honors a one-shot `_suppressNextTransitionSfx`.
- `OnSubmitAudio` (new `_input.UI.Submit` subscription) → `Error` when the selected control is
  non-interactable.
- `ConfirmModal.HandleConfirm` → `UISfx.Confirm()` + `UIManager.SuppressNextTransitionSfx()` so the
  modal's pop doesn't double the Confirm with a Back.
- `MainMenuScreen.OnContinue` → `UISfx.Error()` on the no-save guard.
- `Click()` kept as a `Select` alias for older call sites.

## Verification (Play mode, EditorApplication.Step)
Compile clean, zero console errors through heavy menu navigation. Because the audio DSP clock does
not advance under `EditorApplication.Step()` (`isPlaying` never returns to false — see audio.md),
verified **deterministically**:
- All 5 clips synthesize with correct sample counts + non-zero peaks, routed to the **SFX** mixer
  group (`outputGroup=SFX` → confirms audit A4, UI SFX respects the SFX slider):
  `_move(1543smp,0.27) _select(2645,0.41) _back(3307,0.41) _confirm(7055,0.29) _error(5732,0.39)`.
- **Move hook**: forcing a selection change (Quit→Story) flipped the source to playing; a **fresh idle
  menu ran 30 frames with 0 selection changes and 0 playing frames** — no spurious/machine-gun Move.
- **Push/pop**: OpenScreen and Back each triggered a transition cue.
- **Suppress**: `_suppressNextTransitionSfx` sets on `SuppressNextTransitionSfx()` and clears on the
  very next transition frame (reflection frame-trace `F→_` at frame 1) — the pop after a Confirm is
  silent.

## New gotcha (added to audio.md)
`AudioSource.isPlaying` is a phantom under `EditorApplication.Step()` — the DSP clock is frozen, so a
one-shot reads `isPlaying=true` forever and never reports silence. Prove cues via clip sample data +
output group + flag lifecycles, not by watching isPlaying fall.

## Test script for Trevor
1. Play from `Scene_MainMenu`, headphones on. Arrow the nav row → a soft tick per move (Move).
2. Open Settings (Select), press Back/B (Back — note it's a lower, downward sound vs Select).
3. Quit → the modal Confirm plays a warm two-note; Cancel plays Back.
4. With no save present, focus Continue and press it → the muted Error tone.
5. Open the SFX slider in Settings and lower it → all UI cues quieten with it.

## Next (audit order): batch-57 = menu ambience bed (procedural) + mixer group + settings slider.
