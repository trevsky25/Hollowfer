# Batch 44 — ConfirmModal parchment restyle + UI page-transition click

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-44` (pending)
**Directive:** Trevor — the Confirm popup (menu delete-save etc.) "feels stale and outdated from the rest
of the menu UI" — redo it to match. Add a subtle "click" SFX when moving between menu pages (settings ↔
field guide ↔ wren, etc.).

## 1. ConfirmModal → programmatic parchment card
Legacy scene-UI (uGUI Text, wired in Scene_MainMenu/Scene_UITest) rebuilt code-side per the batch-28
SettingsScreen template: on OnInitialize, wipe the legacy Canvas children and build the pause/journal
paper register — ink scrim, soft-shadow rounded parchment card, PaperGrain + top sheen, inset gold
hairline frame, Georgia serif title + ledger double-rule, italic body, Cancel (ink ghost) / Confirm
(gold accent) buttons with FocusHighlight, explicit pad navigation, DefaultSelected = Cancel (safe).
API (`ConfirmModal.Show(title, message, onConfirm, onCancel)`) unchanged — no call-site churn. No scene
edits needed (build happens at runtime).

## 2. UI click on screen transitions
`UISfx` (new, `Scripts/UI/UISfx.cs`): procedural 2-tone tick (~60ms, soft 1.7kHz tap over a 240Hz thump,
exponential decay — synthesized `AudioClip`, no external asset, consistent with the procedural UI-primitives
tier). Played from `UIManager.TransitionRoutine` on push/replace/pop, gated: skip the boot-time initial
open and any transition involving the loading screen (scene handoffs keep their own mood). Routed through
the SFX mixer group via a new serialized `_uiSfxOutput` on UIManager (wired in Scene_MainMenu by bridge)
→ respects the SFX volume slider.

## Verification (play mode, Scene_MainMenu)
- [x] Compiles clean; modal renders in the new register over the dimmed menu — screenshot
      `confirm_modal_restyled2.png` (parchment card, serif title + double-rule, italic body,
      Cancel[focused-gold]/Confirm, Esc hint).
- [x] Explicit left/right navigation wired (unchanged logic); DefaultSelected = Cancel (visible focus glow).
- [x] Click: `UISfx.Click()` → `_UISfx` source `isPlaying=true` asserted in the SAME bridge call;
      routed to the SFX mixer group (wired in Scene_MainMenu).
- Gotcha hit during verify: a stale bridge-injected keyboard event (Enter, from the batch-42 welcome-gate
  test) auto-confirmed the modal on the first unpaused run — re-verified under pause+Step, holds open.
  Lesson: always queue the RELEASE state event after injecting a key press.
