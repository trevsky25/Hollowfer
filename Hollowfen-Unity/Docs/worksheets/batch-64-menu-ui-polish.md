# Batch 64 — Menu UI audit and polish

**Date:** 2026-07-14 · **Status:** verified

## Goal

Audit the live menu family at the Steam Deck target and refine it into one modern, game-ready visual system. Remove the visible RenderTexture box behind Field Guide mushroom models, reduce border inflation, make controller focus consistent, and bring Settings into the same interaction language without flattening its cinematic main-menu identity.

## Plan

- [x] Audit Field Guide, mushroom detail, Story, Wren, and all Settings tabs in live Game View
- [x] Define shared surface, border, spacing, close, and focus rules
- [x] Implement transparent 3D specimen compositing and Field Guide cleanup
- [x] Apply the same rules to Story, Wren, story detail, and Settings
- [x] Play-mode verification
- [x] Docs updated + worksheet finalized

## Audit findings

| Finding | Severity | Resolution |
|---|---:|---|
| 3D previews rendered the camera clear color as a square black/green box | High | Camera now clears to alpha `0`; model `RawImage`s composite over the UI, with an optional procedural halo beneath |
| Nearly every journal card/leaf carried its own outline | Medium | Resting cards are borderless; only primary reading leaves/gallery frames retain a 10%-alpha structural line |
| Focus treatments varied between full gold fills, text tint, border tint, and scale | High | Selectable surfaces share a slim gold rail + 4.5%-alpha wash; sliders use rail + handle emphasis; tabs keep active underline |
| Field Guide model badge added another dark pill over the model | Low | Badge is now an unboxed, right-aligned eyebrow label |
| Mushroom detail stacked warm brown section boxes inside gold-outlined leaves | Medium | Reading sections moved to neutral forest surfaces; leaf outlines are restrained cream structural lines |
| Settings had no visible shared close affordance and used reflection to retarget focus | Medium | Added shared close control with explicit controller links; all focus setup uses `FocusHighlight.Configure(...)` |
| Main Menu `Forage` opened the save-slot picker without a visible way back | High | Added the shared top-right X to the scene-authored slot Canvas and wired it to `UIManager.Back()` |
| Wren dossier and Story cards used persistent outlines even when idle | Medium | Cards are separated by tonal surfaces and spacing; focus supplies the temporary edge signal |

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Surface hierarchy | Backdrop → base surface → raised/quiet surface | Tone and spacing carry grouping before borders do |
| Border budget | No resting card border; one faint outline only on primary structural frames | Prevents every element competing at the same visual weight |
| Focus | 3 px gold rail + restrained wash; small scale only on cards | Reads immediately on Deck/controller without turning the full UI ochre |
| Model staging | Transparent RenderTexture plus UI-native radial halo | Removes the camera-feed rectangle while keeping pale models grounded on dark leaves |
| Settings identity | Preserve cinematic hero/editorial column; share interaction tokens, not the journal layout | Uniform behavior and finish without erasing screen-specific purpose |
| Oyster photo | Keep localized missing-sketch state | The SO intentionally has no field photo; do not fabricate or substitute an unrelated image |

## Delivered

- Added menu surface/divider/focus tokens to `HollowfenPalette`.
- Added shared surface focus, structural-border, specimen-halo, and refined close treatments to `JournalChrome`.
- Added cached procedural `UICanvasUtil.RadialGlow`.
- Made `JournalMushroomModelPresenter` camera clears transparent.
- Removed Field Guide and Story resting card outlines; model cards blend into their surface and use an unboxed `3D STUDY` label.
- Refined mushroom detail leaves, neutral section surfaces, transparent model stage, and field-photo composition.
- Removed Wren dossier outlines, reduced the gallery to one structural line, and standardized plate focus.
- Reduced Story reader framing to one restrained structural line.
- Added the shared Settings close control, slider focus rails, quieter cycler surfaces, and explicit close navigation; removed reflection-based focus setup.
- Added the shared close control to the Forage/save-slot picker and an explicit Close → Slot0 → Slot1 → Slot2 → Slot3 controller chain.

## Verification evidence

| Check | Result |
|---|---|
| Unity compile / console | Clean after both implementation passes |
| Gotcha lint | `ERRORS=0 WARNINGS=0 WAIVED=1` |
| Data integrity | `ERRORS=0 WARNINGS=0`; 21 mushrooms, 30 story cards, one profile, three journal models |
| Runtime smoke | PASS; stable play at 4141 frames, zero pre-play/in-play errors |
| Steam Deck visual audit | Final captures are 1280×800 in `Docs/screenshots/batch-64/` |
| 3D composition | Fly Agaric, Field Mushroom, and temporarily hydrated Oyster all rendered without a rectangular camera background; discovery snapshot restored to the original six IDs |
| Settings navigation | `row.up=Tab_Graphics; tab.up=CloseButton; tab.down=Cycler_settings.graphics.fullscreen; close.down=Tab_Graphics` |
| Forage/save-slot return | X invocation popped `save-slot` and restored `main-menu`; graph reports `slot0.up=CloseButton; close.down=Slot0` |
| Cross-screen hierarchy | Field Guide, mushroom detail, Story index, Wren top, Settings Audio, and Settings Graphics inspected live after final styling |

Key final evidence:

- `field-guide-final-1280x800.png`
- `mushroom-detail-final-1280x800.png`
- `mushroom-detail-field-final-1280x800.png`
- `mushroom-detail-oyster-final-1280x800.png`
- `story-index-final-1280x800.png`
- `wren-top-final-1280x800.png`
- `settings-audio-final-1280x800.png`
- `settings-graphics-final-normal-nav-1280x800.png`
- `save-slot-close-final-1280x800.png`

## Docs updated

- `Docs/systems/menu-pages.md` — visual hierarchy, transparent model treatment, and evidence path
- `Docs/systems/ui-framework.md` — shared tokens/helpers and RenderTexture gotcha
- `Docs/systems/settings.md` — shared close/focus behavior and navigation graph
- `Docs/systems/save.md` — Forage slot-picker close behavior and navigation graph
- `Docs/tests.md` — batch-64 validation status and manual visual coverage boundary

## Unfinished / handoff

- Dedicated model coverage remains intentionally 3/21. Extend `_journalPreviewPrefab` + `DataImporter.JournalModelPath` only when a genuinely authored species model is approved.
- Oyster and Aldermark intentionally lack field photos. Oyster therefore shows the 3D study left and localized missing-sketch block right.
- A physical controller/Deck feel pass is still recommended before a public build; the explicit navigation graph and keyboard-equivalent tab path were verified here.
- Pre-existing unrelated worktree changes in `Docs/dashboard.html`, `src/story-page.js`, and local picture-book/output folders were preserved.

## Suggested Play-mode test (6 steps)

1. Set Game View to `Steam Deck (1280x800)`, enter Play mode, and open Field Guide. Confirm the selected card has a slim left rail, resting cards have no outlines, and Fly Agaric's model has no rectangular background.
2. Open Fly Agaric and Field Mushroom. Rotate each with Left/Right and confirm the model stays over the soft halo while the realistic photo remains beside the description.
3. Using a QA discovery snapshot, open Oyster. Confirm its model renders left and the right photo area says `No field sketch recorded`; restore the snapshot without saving the QA unlock.
4. Open Story and Wren. Traverse their cards/sections with D-pad or stick; focus should use the same rail/wash language and remain readable against both flat and photographic backgrounds.
5. From Main Menu choose Forage. Confirm the X is visible; move Up from Journal 1 to X and press A, then reopen and press B. Both paths should restore Main Menu without changing a slot.
6. Open Settings. Move Audio → Graphics → Controls → Credits with LB/RB or Q/E; verify slider/cycler focus, then move content → tab → Close → tab with Up/Down. Return to Main Menu, watch the Console for errors, and exit Play mode without changing the QA save.

## Feedback to Trevor

The menu now has a clear border budget: borders indicate structure, focus indicates interaction, and tonal surfaces indicate grouping. Keeping those roles separate is the rule that will make future screens feel like the same game even when one uses a flat journal backdrop and another uses full-bleed cinematic art.
