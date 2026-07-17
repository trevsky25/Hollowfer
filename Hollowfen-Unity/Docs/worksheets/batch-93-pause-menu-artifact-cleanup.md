# Batch 93 — Pause-menu artifact cleanup

**Date:** 2026-07-16 · **Status:** verified; awaiting commit/tag

## Goal

Remove the wide blurred black rectangle across the top of the pause card and stop Save Game from appearing
selected when another pause action owns focus.

## Root causes

- `PauseScreen` added a generated `SoftShadow` as a sibling of its parchment card. In the live overlay
  composition that `PausePresentation/Shadow` image rendered through the top portion of the card as a wide,
  blurred black band.
- Save Game was the only row built with `accent: true`, giving its base `Image` a permanent gold wash. The
  actual focused row also painted its `FocusGlow`, so Story/Field Guide/etc. and Save Game could look selected
  simultaneously.

## Implementation

- Removed the pause card's generated shadow. The dim gameplay scrim, rounded parchment edge, and inner gold
  rule retain the intended depth without a detached blur layer.
- Removed the pause-row `accent` state. Every action now has a nearly transparent raycast fill at rest, and
  `FocusGlow` is the sole owner of the visible selection backing.
- Replaced PauseScreen's private-field reflection setup with `FocusHighlight.Configure`, ensuring each row
  captures the correct transparent base color and focused state through the supported API.
- Extended `ProductionUIVerifier` to fail if `PausePresentation` regains a detached shadow or if any pause
  button receives an opaque resting fill.

## Verification evidence

- Confirmed the artifact source live by disabling `PausePresentation/Shadow`; the black band disappeared.
- Forced Unity to rebuild `Assembly-CSharp.dll` after the editor initially retained the stale pre-fix assembly;
  the compiled assembly timestamp is newer than `PauseScreen.cs`.
- Repeated Main Menu → existing journal → gameplay → pause using the rebuilt assembly. The card header is clean,
  Save Game has no resting box, and only the active action shows the gold focus backing.
- Unity script compilation completed with zero C# errors.
- `git diff --check` passes for the changed source and documentation.

## Files changed

- `Assets/_Hollowfen/Scripts/UI/PauseScreen.cs`
- `Assets/_Hollowfen/Scripts/Editor/ProductionUIVerifier.cs`
- `Docs/systems/ui-framework.md`
- `Docs/worksheets/batch-93-pause-menu-artifact-cleanup.md`
