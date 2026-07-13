# Batch 40 — Opening polish: Bram fix, streak, loading anim, 2-image intro

**Date:** 2026-07-12 · **Status:** DONE (play-verified) · tag `batch-40` (pending)
**Directive:** Trevor — "Bram is still missing completely. The welcome has a streak of light near the top (dark
layer cut off). 'Gathering the last light' should look like it's loading. Maybe use 2 photos for the intro."

## 1. Bram missing → fixed (the big one)
Root cause: a **two-Animator conflict**. The batch-39 setup put an Animator (avatar + idle controller) on the
`NPC_Bram` parent while the `BramModel` FBX instance (which owns the rig) had its *own* Animator — the parent
retargeting a rig one level down under a child collapsed the mesh at runtime (bounds → invalid = invisible).
Fix: moved the idle controller + avatar onto **`BramModel`** (the FBX root that owns the matching rig) and
disabled/cleared the parent Animator. Play-verified: SMR bounds stay ~2.1 m and the mesh renders (textured
boots/coat visible) + the breathing idle plays (arms leave the T-pose).

## 2. Streak of light → fixed
The "cut-off band" was a **scrim rect edge mid-screen**: the bottom scrim was rect-sized (its top edge sat at
~55% up) so there was a visible line where its darkening ended. Replaced the two rect scrims (bottom + the
batch-40 top scrim) with **one full-screen vignette gradient** (dark bottom for text + a touch dark at the very
top for the letterbox blend, clear middle) in BOTH `LoadingScreen` and `NarrationOverlay` (kept identical for
the seamless handoff). Full-screen = no mid-screen edge. Play-verified clean.

## 3. Loading line → clearly animated
`gathering the last light` now **pulses** (alpha sine) in addition to the cycling dots, so it reads as an
active loading indicator.

## 4. 2-image intro
`NarrationOverlay.ShowCinematic` gained a 2-image overload: it **swaps to a second hero image at a switch
beat** (default beat 3, "The river was wrong") so the long voiced intro isn't one static picture. `StoryBeats`
exposes `_introHeroImage2` + `_introSwitchBeat`. Wired an **interim** image (`fathers-mill.png`) so it works
now — Trevor is generating a bespoke 2nd image via Codex (prompt handed off in chat). Swap it into
`_introHeroImage2` when ready.

## Verification
Compiles clean; lint 0/0. Bram renders + animates; welcome streak gone; motes + pulsing loading line; the
2-image field is wired. (Full narration 2-image swap trusts the one-line hard-swap; verify live.)

## Docs
Worksheet only (mechanical fixes on existing systems).
