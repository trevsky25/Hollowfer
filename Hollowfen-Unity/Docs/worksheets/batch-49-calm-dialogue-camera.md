# Batch 49 — Calm Bram's dialogue camera

**Date:** 2026-07-13 · **Status:** IN PROGRESS · tag `batch-49` (pending)
**Directive:** Trevor — batch-45's `DialogueCinematics` glides to a fresh over-the-shoulder single on
EVERY speaker change, so a rapid Bram↔Wren exchange whips the camera constantly. Reduce the back-and-forth:
hold the two-shot (or a loose favor framing) for short/rapid lines; only glide to a tight single on long
lines or `isCloseup` beats; raise the re-glide threshold; damp the sway/push.

## Problem (batch-45 `OnLine`)
Every non-closeup line calls `OverShoulder(wren)` and glides to it. Alternating short lines = a full
OTS pan on each one → seasick. The film-grammar mistake: real coverage holds a wide/favor frame through
rapid exchanges and cuts to singles only for the lines that earn them.

## Fix — shot-mode selection in `OnLine`
- New `FavorShot(bool wrenSpeaks)`: the establishing two-shot, but the LOOK target panned ~34% toward the
  speaker and FOV tightened a touch → both stay in frame, the speaker gets the favored read. No big move.
- `wantSingle = closeup || estSeconds >= LongLineSeconds`. Short line → **favor two-shot**; long/closeup →
  **tight single** (OTS / Closeup as before).
- Track `_shotMode` (Two vs Single):
  - Short line while already in Two mode → gentle `FavorGlideSeconds` (0.7s) pan of the favor toward the
    new speaker; **same speaker → hold** (no re-glide, push-drift continues). Kills the whip.
  - Mode change (Two→Single / Single→Two / single subject changes on a long line) → the deliberate
    `GlideSeconds` move. These are now the only big camera moves.
- Damped feel: `SwayPos` 0.012→0.008, `SwayDeg` 0.25→0.15, `PushInFraction` 0.045→0.028.
- `LongLineSeconds = 4.2`, `FavorGlideSeconds = 0.7`.
- `OnChoices` / `Begin` set `_shotMode = Two`.

## Verification (play mode, Scene_Hollowfen — drove `DialogueCinematics` directly on the real player +
NPC_Bram transforms, `EditorApplication.Step()` frame driver, `manage_camera` game_view captures)
- [x] Compiles clean (0 errors).
- [x] Establishing → first short Bram line = favor two-shot: cam (276.06, 38.88, 155.27) FOV 37, both
      Wren & Bram in frame (`b49_A_bram_favor.png`, since deleted).
- [x] **Rapid Wren→Bram→Wren short exchange HOLDS** — cam pos identical (275.9, 38.9, 155.2) FOV 37 across
      all three lines; only the look-target favor rotates. No per-line OTS whip. **This is the fix.**
- [x] Long line (est 5.2s) commits to a single — cam glides to (284.09, 38.68, 151.60) FOV 34,
      over-the-shoulder tight on Bram (`b49_C_bram_single.png`, since deleted).
- [x] `End()` restore: cam handed back to gameplay follow pose, CinemachineBrain re-enabled, timeScale 1.
- Damped feel applied (sway ±8mm/±0.15°, push 0.028). No SDF/font churn this run.
