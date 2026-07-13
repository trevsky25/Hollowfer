# Batch 46 — Dialogue jump fix, moving loading screen, Bram off Voss's chair

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-46` (pending)
**Directive:** Trevor — (1) Wren jumps non-stop during the Bram dialogue. (2) After pressing any button
on a new game the screen freezes until the VO starts — replace "press any button" with a MOVING loading
screen. (3) Bram is standing on a chair next to the table — nudge him clear.

## 1. Wren jumping during dialogue
Space advances dialogue AND is Player/Jump. DialogueScreen.Open set `Suspended` but never disabled the
player's `PlayerInput` (NarrationOverlay/InspectScreen do — the exact bug class documented on
`SetPlayerInputEnabled`). Invisible pre-45 because the frozen animator hid the hop; batch-45's
UnscaledTime animators made every advance a visible jump. Fix: Open → `SetPlayerInputEnabled(false)`,
Close → `(true)`. Verified: `PlayerInput.enabled == false` while open.

## 2. "Press any key" → moving loading screen (batch-42 gate reworked)
The press gate meant keypress → straight into the scene-integration stall = "I pressed and it froze."
Removed the gate (`AnyBeginPressed` deleted): the welcome now stays a visibly animated loading card the
whole way — pulsing loading line + drifting motes + a NEW **gold marquee streak** sweeping a thin
RectMask2D-clipped track — then at `progress≥0.89` (+0.8s breath) the line flips to "entering Hollowfen",
one frame renders, activation runs. The hitch reads as arrival, not a hang. Verified: load auto-advanced
MainMenu→Scene_Hollowfen with zero keypresses; marquee x 363→4 over 15 stepped frames (moving), screenshot
`welcome_loading_marquee.png`.

## 3. Bram off the chair
His pill spot (batch-43) was **Voss's placeholder AT the tax table** — `Tax_Chair` sat 0.23m from him
(`_VossTaxTable` cluster). Moved `NPC_Bram` → **(284.8, 37.03, 158.4)** (terrain-grounded, facing the
south approach): 1.84m from the chair, 3.0m from the table, 2.0m from the well, 0 renderers within 1m.
Scene saved.

## Gotcha logged
Mid-play domain reload corrupted a verify session (statics wiped, `DialogueScreen.Instance` null while
its GO stayed active): a `refresh_unity` compile finished AFTER play entered. Rule: **confirm
`isCompiling == false` before entering play mode** — never enter play with a compile in flight.
