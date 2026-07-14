# Batch 61 — Wren Page Redesign ("Forager's Dossier")

**Date:** 2026-07-13 · **Status:** verified

## Goal
Trevor asked (interactive session) for a full redesign of the main-menu Wren page: the hero painting (`wren-profile.png`) as the full-size background, the complete character identity presented as a character sheet, and five newly supplied study images (turnaround sheet, front/back/three-quarter figures, knife plate) worked into the design.

## Plan
- [x] Extract the 5 chat-attached images from the session transcript, convert webp→png, import as Sprite (2D and UI) at `_Hollowfen/UI/Characters/wren-*.png`
- [x] Extend `CharacterProfileData` with 5 plate sprite fields + getters; wire sprites on `Character_WrenTobin.asset` via SerializedObject
- [x] Rebuild `WrenScreen.BuildLayout` as the dossier design (see below)
- [x] Play-mode verification (screenshots top/mid/bottom, pad waypoint scroll, Back)
- [x] Docs updated + worksheet finalized

## Design
Fixed backdrop: full-bleed hero via `AspectRatioFitter` EnvelopeParent (no distortion on 16:10 Deck), slow sine Ken Burns drift + scroll parallax in `Update`, left/bottom/top gradient scrims, plus a "deep scrim" whose alpha follows scroll offset so the plate gallery reads on near-dark. Scroll content (1680 col): identity column (eyebrow → 118pt IM Fell name → tagline → gold rule → lead → hairline-divided stat strip), Background/Perspective cards, SHE CARRIES kit strip, FIELD STUDY plate gallery (Plate I study sheet, Plates II–IV figure row, Plate V knife + pullquote panel), footer rule.

Gamepad-first fix: the old page had ZERO selectables (pad could not scroll it at all). Now every section is an invisible `Selectable` waypoint with an explicit Navigation chain (close ↕ identity ↕ cards(↔) ↕ kit ↕ plate I ↕ figures ↕ knife ↕ close); focusing a section lights its gold hairline via `FocusHighlight` (re-pointed by reflection, `_baseColor` re-cached — the Awake trap) and `ScrollFocusFollower` glides it into view. Mouse wheel now works too (the old Scroll bg image had `raycastTarget=false`; it must be `true` for the ScrollRect to receive wheel events).

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Where plate sprites live | `CharacterProfileData` fields, not WrenScreen serialized refs | Menu pages are SO-driven per this system's convention; screen stays data-blind |
| Attached images on disk | Extracted from session JSONL (base64 blocks) → sips webp→png | Chat attachments aren't files; transcript is the only on-disk source |
| Plate captions | Non-canon descriptive labels ("Plate I", "Front", "The forager's knife") | No invented canon; knife text ties to the existing kit item |
| Figure row focus | One waypoint per row, highlight on first tile's hairline | Keeps the pad chain strictly vertical; tiles aren't individually actionable |
| Text sizing | Pin everything with LayoutElement (old screen's approach) | Avoids TMP-preferred-height-in-VLG first-frame flakiness |

## Verification evidence
Play mode driven by `EditorApplication.Step()` via the bridge, `OpenScreen("wren")`:
- `b61_final_top.png` — hero full-bleed, identity column, stat strip, cards peeking (scroll affordance)
- `b61_wren_kit_fixed.png` / `b61_final_cards.png` — dossier cards + kit strip + study sheet over deepened scrim
- `b61_wren_figures_focus.png` — `EventSystem.SetSelectedGameObject(Plate_Figures)` scrolled 1.0 → 0.258 smoothly; focused row hairline visibly brighter
- `b61_wren_bottom.png` — knife plate + pullquote pair, footer
- `UIManager.Back()` → top=`main-menu` ✓; console clean (only URP memoryless-depth info lines)
- Content height 4084 px; screenshots at scratchpad `/private/tmp/claude-501/…/baffe35a…/scratchpad/`

## Docs updated
- `Docs/systems/menu-pages.md` — header (status + waypoint gotcha), WrenScreen row rewritten, CharacterProfileData row notes plate fields

## Test script for Trevor (Play mode)
1. Play `Scene_MainMenu` → main menu → open **Wren** from the nav row.
2. Confirm the painting fills the screen and slowly drifts; title column reads over it.
3. Scroll with mouse wheel AND with pad (dpad/stick down walks sections; each section's gold outline lights as it focuses).
4. Walk to the bottom: study sheet → three figures → knives + quote. Background should be near-dark down here.
5. Press B (pad) or click ✕ → back to main menu.

## Unfinished / handoff
None dirty. Deferred (pre-existing): localization wiring for menu pages. Optional polish idea: per-tile zoom-on-focus for the figure plates.
