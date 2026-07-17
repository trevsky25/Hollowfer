# Batch 65 — Gameplay story-card redesign

**Date:** 2026-07-14 · **Status:** DONE (play-verified)
**Directive:** Redo the in-game story card shown after the new-game homecoming narration, when Wren is sent to find Bram in the town square; carry the redesign across gameplay story-card notifications.

## Audit

The previous first-steps card stacked a parchment slab, inset frame, double rule, task box, three route bullets, six control groups, a button, and a second dismiss prompt. It covered most of the first playable view while the full gameplay HUD remained visible around it. The result was legible, but it read as a control reference/settings sheet immediately after a cinematic story opening.

## Delivered

- Rebuilt `IntroGuide` as a compact ink-glass **story/objective handoff** using the same quiet forest surfaces and restrained gold hierarchy as the batch-64 menu family.
- Preserved the homecoming voice clip, once-per-save flag, live `QuestManager.ActiveQuest` copy, input grace window, and Set Out flow.
- Reduced onboarding to the two controls needed now: Move and Interact. Journal/Pause remains a quiet footer hint.
- Temporarily disables lower-order gameplay canvases while the card is open, then restores the exact enabled set on dismiss or scene teardown. The world remains visible and player input remains suspended.
- Added eased slide/fade/scale entrance and exit motion; controller focus lands on the filled Set Out action.
- Rebuilt `StoryCardToast` in the same image-led ink-glass family, localized its title/subtitle and chrome, cover-cropped the art through `RectMask2D`, ensured it paints above the HUD, and lowered it to `-400` so the day/time chip remains clear.

## Verification

- Unity script compile: 0 console errors.
- Exact 1280×800 visual pass for the opening card and story-unlock notification.
- Runtime opening-card path: `showing=True`, alpha `1.00`, player suspended, 4 lower-order HUD canvases hidden, EventSystem selection = `SetOutButton`.
- Runtime Set Out path: card closed, player suspension cleared, hidden-canvas list empty, all 4 gameplay canvases restored, once-per-save flag set.
- Save slots were backed up before the stateful dismissal test and restored byte-for-byte afterward.
- `StoryCardToast` data was populated directly for visual QA; no story unlock or persistence state was changed.

## Evidence

- `Docs/screenshots/batch-65/intro-guide-final-1280x800.png`
- `Docs/screenshots/batch-65/story-unlock-toast-final-1280x800.png`

## Files

- `Assets/_Hollowfen/Scripts/UI/IntroGuide.cs`
- `Assets/_Hollowfen/Scripts/UI/StoryCardToast.cs`
- `Assets/_Hollowfen/Scripts/Localization.cs`
- `Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity`
- `Docs/systems/ui-framework.md`
