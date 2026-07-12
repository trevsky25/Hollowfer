# Batch 28 — Settings screen UX rebuild (production menu polish)

**Date:** 2026-07-12 · **Status:** IN PROGRESS · tag `batch-28` (pending)
**Directive:** Trevor — "improve the MainMenu settings … in prep for production ready launch"; model switched to Fable 5 for this task.

## Survey findings (screenshots in scratchpad: menu_00..04)
The main menu is the polished editorial design (Georgia serif, left column, sage/gold/cream).
The settings screen is pre-idiom programmer UI and the odd one out:
1. **Typography clash** — title/labels are default LiberationSans over the Georgia-serif menu; legacy `Text`, not TMP.
2. **No scrim/panel** — raw full-width Unity sliders float over the busy hero; poor contrast.
3. **Raw controls** — default slider visuals; legacy `Dropdown` (pad-hostile); fullscreen is an unlabeled solid square (state unreadable); quality shows Unity's internal name "PC".
4. **No value readouts** on volume sliders.
5. **Controls tab overflows** the screen — the binding table's last rows collide with the footer hint (real bug).
6. **Localization violation** — every label is a raw scene string (non-negotiable requires `Localization.Get`).
7. **Resolution list duplicated** per refresh rate (Screen.resolutions raw).
8. `MainMenuScreen.OnCredits()` is `// TODO` — dead path.

## Approach
Rebuild SettingsScreen to the canonical **code-built idiom** (ui-framework.md: "screen GameObjects are
empty Canvas hosts; OnInitialize constructs everything from code") — same direction as the queued
TMP-migration backlog item. **Echo the main menu's left editorial column** so settings reads as the
same "page": sage eyebrow, Georgia serif title, gold rule, text-nav tab row, content rows below.
- All strings via `Localization.Get` (new `settings.*` keys); TMP throughout (Georgia headings).
- Sliders: slim gold track + circular handle + % readout; FocusHighlight; explicit vertical nav.
- Dropdowns → **‹ value › cyclers** (gamepad-first; Submit or stick-left/right cycles). Resolution
  deduped by w×h; quality names localized (fallback raw), row hidden if <2 levels.
- Fullscreen: cycler showing localized On/Off.
- Controls tab: sensitivity slider (1–10 whole steps, "5 / 10") + the binding table in a ScrollRect
  (RectMask2D per gotcha; content preserved verbatim from the legacy panel).
- Credits tab: editorial hierarchy reusing the SHIPPED copy verbatim (no canon invention); final
  credits copy remains Trevor's open backlog item.
- MainMenu Credits button → opens settings pre-switched to Credits (static NextOpenTab handoff).
- **Preserve exactly:** PlayerPrefs keys, mixer params + dB math, GameSettings 1–10 mapping,
  Tab enum + LB/RB cycling, per-tab DefaultSelected, ScreenId "settings" (pause menu path unchanged).

## Scene surgery
Delete the legacy `Canvas` subtree under `UIManager/ScreenRoot/SettingsScreen` in Scene_MainMenu
(new EnsureCanvas builds on the host, StoryScreen-style); clear stale `UIScreen._defaultSelected`.
Legacy copy (controls table, credits lines) dumped from the scene FIRST and preserved in this sheet.

## Out of scope (parked)
- Input rebinding (blocked on action-map consolidation backlog item), language selector (loc pass
  not wired), VSync/framerate cap (Deck-cert pass), reset-to-defaults.
- Main menu hero/nav visual design — intentional, untouched (memory: reference, not disposable).

## FABLE REVIEW
**Verdict: SHIP WITH CHANGES** (independent fable reviewer, fresh context; verified pause-path
timing, lifecycle, pad completeness, UIManager contract, FocusHighlight reflection, scene hygiene —
all clean). Findings applied pre-commit:
| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | MED | UIManager PUSH deactivates covered screens WITHOUT OnClose → settings' LB/RB/Navigate handlers stay live under a stacked screen (reachable via Pause over settings; legacy had the same latent bug) | All three handlers gate on `TopScreen == this`; gotcha promoted to ui-framework.md |
| 2 | MED-LOW | Pre-28 resolution prefs indexed the RAW list; a stale in-range index mislabels current res and first cycle jumps from the wrong base | Reality wins: index distrusted when it disagrees with `Screen.width/height` |
| 3 | LOW | `NextOpenTab` could leak if OpenScreen drops during a transition → next plain Settings open lands on Credits | Cleared in `MainMenuScreen.OnOpen` |
| 4 | LOW | `settings.quality.*` keys were never added — names always fell back to raw English | Key added for the sole "PC" level |
| 5 | LOW | 45° diagonal stick (x≈0.71) both navigated AND cycled a focused cycler | Axis-dominance guard: `abs(x) > abs(y)` required |
| 7 | info | PlayerPrefs never flushed (legacy parity) — settings lost on crash | `PlayerPrefs.Save()` in OnClose |
(6: %-composition value strings noted for the localization pass — legacy parity, acceptable.)

## Verification evidence
**Play-mode (bridge), Scene_MainMenu — all green**, integrity `ERRORS=0 WARNINGS=0`, lint 0/0,
0 new console errors (only URP Metal memoryless-depth notices):
- **Behavior contract:** master slider 0.5 → pref `audio.master=0.5` + mixer `MasterVolume=-6.02dB`
  (exact 20·log10); fullscreen cycler Off→On + pref write; sensitivity 8→1.150, 5→1.000 (GameSettings
  mapping intact); quality row correctly hidden (1 level).
- **Pad focus:** per-tab DefaultSelected lands on master slider / fullscreen row / sensitivity
  slider / Credits tab button; focused controls show the gold FocusHighlight (visible in shots).
- **Credits handoff:** `NextOpenTab=Credits` → opens on Credits, consumed (next plain open = Audio).
- **Post-fix regression:** plain open lands Audio (leak guard), TabRight switches when top,
  resolution cycler shows native (editor game-view size never matches monitor modes — build-only
  concern, noted in settings.md).
- **Screenshots:** scratchpad `final_01..04` (all four tabs over the hero) vs `menu_01..04` (legacy).
- Scene_MainMenu net −10.7k lines (legacy settings canvas deleted); `_audioMixer`/`_backgroundSprite`
  scene refs verified; incidental Georgia/TMP font re-serializations reverted; saves untouched
  (menu-only sessions).

## Docs updated
- `systems/settings.md` — header rebuilt (batch-28 idiom, gotchas incl. push-without-OnClose,
  deduped-resolution semantics, editor game-view quirk); inventory table per-control; new
  SettingsScreen structure section.
- `systems/ui-framework.md` — push-without-OnClose gotcha added; status notes SettingsScreen
  code-built + remaining legacy chrome (MainMenu/SaveSlot/Loading/ConfirmModal).

## Unfinished / follow-ups
- **Remaining legacy menu chrome:** MainMenu, SaveSlot, Loading, ConfirmModal still scene-UI —
  the TMP-migration backlog item now has a proven template (this batch).
- **Credits copy** is still the shipped 7 lines (presentation improved; words unchanged) —
  QUESTIONS Q9 asks whether to expand for launch (asset-pack legal names, font licenses, music-TBD).
- **Georgia font licensing** for distribution flagged to the pre-EA checklist (it's a licensed
  Microsoft font — verify redistribution rights before EA).
- Deferred by scope: input rebinding (blocked on action-map consolidation), language selector
  (loc pass), VSync/framerate cap (Deck-cert pass), reset-to-defaults.
