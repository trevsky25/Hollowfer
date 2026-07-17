# Batch 67 — Cinematic mushroom-cutting challenge

**Date:** 2026-07-14 · **Status:** complete
**Directive:** Replace one-button mushroom pickup with a controller-first, skill-based cutting interaction using Wren's kneeling animation, her authored knife model, cinematic framing, and haptic feedback.

## Goal

Turn every standard `MushroomNode` harvest into a tactile micro-challenge: Wren kneels, the camera cuts to the mushroom stem and knife, the player braces with the left stick and performs controlled alternating saw strokes with the right stick, and only a completed clean cut commits discovery/inventory/quest state.

## Plan

- [x] Audit existing Inspect → harvest → inventory/save flow and supplied assets
- [x] Import and retarget the kneeling animation; validate the knife model
- [x] Build controller/keyboard cutting challenge, cinematic rig, UI, and haptics
- [x] Integrate clean success/cancel lifecycle with all mushroom nodes
- [x] Play-mode gameplay, visual, controller, save, lint, and integrity verification
- [x] Update system docs and finalize worksheet

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Core gesture | Hold left stick down to brace; alternate right stick left/right to saw | Uses both hands and mirrors stabilizing a stem while making controlled knife strokes. |
| Difficulty | Recoverable precision challenge, not a fail-state gate | Bad angle stalls progress and gives feedback; it does not destroy a specimen or force a reload. |
| Keyboard parity | Hold `S`; alternate `A` / `D` | Keeps the same brace-and-saw mental model without requiring a controller. |
| Cinematic grammar | Side kneeling shot → macro knife/stem shot → quiet collection payoff | Shows Wren's authored performance before handing control to the tactile close-up. |
| Controller feedback | Continuous low/high motor resistance plus stroke and cut pulses | Works through Unity Input System on standard gamepads and DualSense-compatible rumble without a platform-exclusive dependency. |
| Commit boundary | Inventory/discovery/quest events fire only after the cut completes | Canceling or leaving mid-challenge cannot grant or lose a mushroom. |

## Verification evidence

- Unity script refresh/compile: 0 errors.
- Supplied kneeling FBX copied byte-identically, imported Humanoid, copied Wren's avatar, non-looping 2.767s clip, and added as `Base Layer.Forage Kneel` in `WrenAnimator.controller`.
- Supplied knife FBX loads from Resources with its authored diffuse + normal material. In Play Mode its normalized renderer bounds measured `(0.338, 0.093, 0.170)m` on the test Field Cap, confirming the centimeter-scale correction.
- Play Mode cinematic inspection validated the side-profile kneel, time-scale-independent Cinemachine blend, black match cut, Wren-isolated macro shot, live six-notch HUD, and knife/mushroom composition.
- Simulated gamepad tests: vertical error held progress at 0; brace + alternating horizontal states advanced exactly `0 → 1 → 2 → 3 → 4 → 5 → 6`; direction/rhythm gates prevented duplicate strokes.
- Sixth stroke changed inventory exactly `0 → 1` while the node remained active during the release shot. After teardown: challenge absent, node inactive, Wren renderer enabled, PlayerInput enabled, interactor unsuspended, `Time.timeScale = 1`, Brain `IgnoreTimeScale = false`, and focus-camera cinematic ownership false.
- All four user save-slot files were backed up before the success test and restored afterward with matching SHA-256 hashes.

## Docs updated

- `Docs/systems/foraging.md`
- `Docs/systems/input.md`

## Unfinished / handoff

- DualSense uses Unity Input System's standard dual-motor rumble. Platform-exclusive adaptive trigger resistance remains a later console-integration layer, not a dependency for this interaction.
- The same six-stroke tuning currently applies to every species. Species-specific resistance, stroke count, or poisonous-specimen modifiers can be layered onto `MushroomFieldGuideData` later without changing the commit boundary.

## Feedback to Trevor

The old lift-spin-shrink pickup is gone from the player path. Harvesting now has a complete authored arc: Wren commits physically, the camera match-cuts into the knife work, the controller communicates resistance, and the mushroom only enters the satchel after the player performs a clean release.
