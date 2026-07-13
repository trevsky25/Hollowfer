# Batch 55 — Font migration: Georgia → IM Fell English (titles) + EB Garamond (body)

**Date:** 2026-07-13 · **Status:** IN PROGRESS · tag `batch-55` (pending)
**Directive (Trevor):** Retire Georgia (MS-licensed ship blocker). Titles → **IM Fell English**,
body/UI → **EB Garamond** — both OFL (SIL Open Font License), free to embed/ship. Resolves the
Georgia licensing item flagged in batch-32/TODOS.

## Fonts acquired (OFL .ttf from github.com/google/fonts)
- `Assets/UI/Fonts/IMFellEnglish-Regular.ttf`  guid `bad1498e71a43486da173d474b26f2fa`
- `Assets/UI/Fonts/IMFellEnglish-Italic.ttf`   guid `9b675308fb0444019aed1f25b6052cf9`
- `Assets/UI/Fonts/EBGaramond-Regular.ttf`     guid `9468f4af628ba4713bc196e8fdd51815`
- `Assets/UI/Fonts/EBGaramond-Italic.ttf`      guid `53245e4576e9740a29a46a9d151502e7`
- `Assets/UI/Fonts/OFL-IMFellEnglish.txt`, `OFL-EBGaramond.txt` (license ships with the font — OFL requirement)
- **KEPT the source .ttf this time** (OFL allows redistribution; Georgia's source was nulled because MS-licensed).

## Discovery (ground truth)
- Georgia SDF asset GUID `0506267eb35a74117b4a9d04a8552389`, at `Assets/UI/Fonts/Georgia SDF.asset`.
- TMP default font (`TMP Settings.asset` `m_defaultFontAsset`) = **LiberationSans SDF** (`8f586378…`) — a SANS.
  So code-built body (`UICanvasUtil.BodyFont` → TMP default) currently renders **sans**, while scene text
  (menu) renders Georgia serif. Mixed state; migration unifies on EBG serif body.
- `UICanvasUtil.HeadingFont` loads Georgia via `AssetDatabase.LoadAssetAtPath` **under `#if UNITY_EDITOR` only**
  and nothing calls `SetHeadingFont`. **Latent P0**: in a player build, every code-built heading (SettingsScreen,
  ConfirmModal, InspectScreen titles) falls back to body font. Fixing via UIManager injection this batch.
- No serialized `TMP_FontAsset` fields in scripts except the UICanvasUtil statics → all fonts flow through
  the getters + scene GUID refs. No prefab references Georgia.
- Georgia scene refs (in-build scenes only): `Scene_MainMenu.unity` (6 TMP objects) + `Scene_Hollowfen.unity`
  (1 object). Classification by size/text:
  - `Hollowfen` size 128 (MainMenu fid 2007939393) → **IM Fell** title
  - `Hollowfen` size 64 (Hollowfen fid 1063859223) → **IM Fell** title
  - `THE FAILING VILLAGE` 20, `WREN TOBIN'S RETURN` 26 → eyebrows → **EBG**
  - `Forage the Edge Woods…` 26, `Traveling to Hollowfen…` 36i, `"New Game →"` 52.8i → nav/body/caption → **EBG**
  - (`Assets/Scenes/MainMenu.unity` is the retired legacy reference scene, NOT in build settings — left alone.)

## Plan
1. Bake `IMFellEnglish SDF.asset` + `EBGaramond SDF.asset` (Static, full Latin-1 + typographic punctuation,
   `ClearDynamicDataOnBuild:false`, LiberationSans-Fallback wired) in `Assets/UI/Fonts/`.
2. `TMP_Settings.defaultFontAsset` → EBGaramond SDF.
3. UIManager: add `[SerializeField] _headingFont/_bodyFont` → `UICanvasUtil.SetHeadingFont/SetBodyFont` in Awake
   (runtime-safe, fixes the latent heading-in-build bug). Assign the two SDFs on the scene UIManager.
4. Repoint scene title objects → IM Fell; other Georgia scene refs → EBG (via `.font` setter in-editor).
5. Re-tune UICanvasUtil metrics (Fell/EBG differ from Georgia in x-height/leading/weight).
6. Verify EVERY text surface in Play mode; zero boxed glyphs; titles = Fell, body = EBG.
7. Delete Georgia.ttf + Georgia SDF.asset; update Credits + conventions Fonts rule; mark licensing resolved in TODOS.

## What shipped
- Baked `IMFellEnglish SDF` (201 glyphs / 2 pages) + `EBGaramond SDF` (212 glyphs / 2 pages), both **Static**,
  `ClearDynamicDataOnBuild:false`, source `.ttf` KEPT + linked (OFL), LiberationSans-Fallback wired.
  Missing glyphs (served by fallback): IM Fell 9 (‐‒―′″←→○−, all non-title), EBG 1 (○).
- `TMP Settings.asset` `m_defaultFontAsset` → EBGaramond SDF.
- `UICanvasUtil`: HeadingFont → IM Fell (editor fallback path); heading metrics re-tuned for Fell
  (characterSpacing −10→−2, lineSpacing −8→−6). `UIManager` new `_headingFont/_bodyFont` serialized fields,
  injected in `Awake` (runtime-safe — **fixes the pre-existing editor-only heading-in-build latent bug**).
- Scene repoints (via `.font` setter → auto-fixes material): Scene_MainMenu 6 Georgia + 9 stray LiberationSans
  (nav row + dividers) → Fell/EBG; Scene_Hollowfen 1 Georgia title + 10 LiberationSans (compass + map chrome)
  → Fell/EBG; legacy Assets/Scenes/MainMenu.unity repointed too. Georgia SDF.asset + Georgia.ttf deleted.
- Credits: added `credits.fonts` (OFL attribution) to Localization + SettingsScreen render list.
- Stale "Georgia" code comments updated (kept one historical tuning-rationale note in UICanvasUtil).

## Verification evidence (Play mode, EditorApplication.Step frame-driver)
Screenshots in scratchpad — ZERO boxed/missing glyphs on every surface driven; live font census `georgia=0
fell=68 ebg=244 null=0`:
- **Main menu** (scene text): "Hollowfen" = IM Fell; eyebrow/subtitle/tagline/body/nav row/"New Game →"/Quit
  = EBG. Arrow → renders (baked in EBG). `b55_menu_nav.png`
- **Settings** (code-built): "Settings" title = IM Fell (proves runtime injection — not the body fallback);
  eyebrow/tabs/labels/values/hint = EBG; `·` `—` render. `b55_settings.png`
- **Story** (code-built): "Story" + every StoryCard title = IM Fell; subtitle/eyebrows/body = EBG; em-dash +
  curly apostrophe render. `b55_story.png`
- **Field Guide** (code-built): title + species titles = IM Fell; Latin-name italics + status eyebrows = EBG.
  `b55_fieldguide.png`
- **ConfirmModal** (parchment, batch-44 TMP): "Leave Hollowfen?" = IM Fell; body/buttons/hint = EBG italic.
  `b55_confirm.png`

## Known / flagged (feed Batch 2 audit — NOT regressions of this batch)
- **SaveSlotScreen** (and LoadingScreen) render in **legacy bold sans** — they still use `UnityEngine.UI.Text`,
  which the TMP font swap can't reach. This is the TMP-migration backlog (SaveSlot/Loading predate the
  migration). `b55_saveslot.png`. → Batch 2 P1 finding.
- **Compass cardinal letters (N/E/S/W…)** now render EBG at 13px (converted from sans for consistency + the
  cartographer aesthetic). Trevor: eyeball legibility; reverting those specific labels to sans is a one-line
  change if you prefer.
- **Italic** uses TMP synthetic skew (Regular-only SDF assets, matching how Georgia worked); the real italic
  `.ttf` faces are in-repo (OFL) but not baked into separate assets — bake them later if true italics are wanted.
- In-game HUD / dialogue / inspect / narration were NOT live-driven this batch (game-scene load is heavy; it
  dropped the bridge once). They build through the same verified `UICanvasUtil` factory, so they inherit the
  fonts — but confirm them during the Batch 2 "drive every screen" pass.

## Test script for Trevor
1. Play from `Scene_MainMenu` → the "Hollowfen" wordmark is the tall old-style serif (IM Fell); nav row +
   tagline are the warmer body serif (EB Garamond). No boxes/tofu anywhere.
2. Settings → the "Settings" title is IM Fell (confirms the build-safe injection); Credits tab shows
   "Typeset in IM Fell English and EB Garamond (SIL Open Font License)."
3. Story / Field Guide → card titles are IM Fell, body + Latin names + status chips are EB Garamond.
4. Quit modal → "Leave Hollowfen?" reads in IM Fell on the parchment card.
5. (Backlog check) Save-slot screen still shows a bold sans — that's the legacy uGUI screen, first item of the
   Batch 2 audit.
