# Batch 41 — Four-image cinematic intro (bespoke opening paintings)

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-41` (pending)
**Directive:** Trevor — generated 4 bespoke opening paintings in Codex (ridge → wrong river → boarded
well lane → silent square with Bram). "Get them wired up in the first opening scene sequence. Impress me
with the cinematic design."

## Goal
Replace the 2-image hard-swap homecoming intro with a **4-image sequence** that plays the 6-beat bible
passage as a film: **crossfade dissolves** between paintings + a **per-image, motivated Ken Burns** move,
with the image change leading the caption slightly. Keep the loading→narration handoff seamless.

## Assets (imported as Sprite, 1672×941, batch-41)
- `intro-01-ridge.png`    — valley intact from the ridge (beats 0–1)
- `intro-02-river.png`    — drowned fields + dead mill (beats 2–3)
- `intro-03-cottages.png` — boarded well lane (beat 4)
- `intro-04-square.png`   — silent square, Bram at the lantern-lit inn door (beat 5)

Beat→image map: `{0,0,1,1,2,3}` (image changes at beats 2, 4, 5).

## Camera moves (per-image Ken Burns, motivated)
| Img | Beat(s) | Move |
|---|---|---|
| Ridge | 0–1 | slow push-out — Wren → whole valley ("looked as it always had") |
| River | 2–3 | drift across the flood → the dead mill ("the river was wrong") |
| Cottages | 4 | creep down the abandoned lane → the lone far lantern |
| Square | 5 | push in → Bram's lantern-lit doorway (the one warm light) |

Image-0 KB A-state MUST equal LoadingScreen's held A-state (scale 1.20, pos 150,46) for the seamless
loading→narration dissolve. LoadingScreen hero repointed homecoming.png → intro-01-ridge.png.

## Changes
- `UI/NarrationOverlay.cs` — new `ShowCinematic(captions, clips, Sprite[] heroes, int[] beatImage, onDone)`
  overload; two hero layers (`_heroA`/`_heroB`) that crossfade; per-image `KbMove` table; back-compat
  overloads retained.
- `Quests/StoryBeats.cs` — pass the 4-image array + beat map; new `_introImages` field.
- `UI/LoadingScreen.cs` — hero sprite → intro-01-ridge.png.

## Verification (play mode, Scene_Hollowfen, pause+Step driven)
- [x] Compiles clean (no CS errors on NarrationOverlay/StoryBeats).
- [x] beat 0 (ridge) renders at scale 1.200 / pos (149,46) = LoadingScreen's held A-state → seamless handoff confirmed; heroB preloads the river sprite while inactive.
- [x] ridge→river crossfade caught mid-dissolve: heroA(ridge) sib 1 α1.0 under heroB(river) sib 2 α0.57 — both painted, correct z-order; caption "Then the road dipped, and the old picture came apart" lands on the dissolve.
- [x] beat 3 (river) active, KB scale mid-move (1.16→1.07); beat 4 (cottages, river→cottages dissolve visible); beat 5 (square/Bram) pushing in toward the lantern-lit door.
- Screenshots: scratchpad `intro_beat0_ridge.png`, `intro_crossfade_ridge_river.png`, `intro_beat3_river.png`, `intro_beat4_cottages.png`, `intro_beat5_square.png`.

## Notes
- Runtime timing fields were reflected-down for step-based capture only (not serialized) — revert on play exit. No SDF/font churn produced this run.
- Legacy 2-image path (`_introHeroImage`/`_introHeroImage2`/`_introSwitchBeat`) retained as fallback; now also routes through the dissolve.
- Follow-ups: hook the 6 HomecomingIntro VO clips back onto `_introVoiceClips` (unchanged); consider a bespoke fewer-images-per-beat pacing once VO lengths are final.
