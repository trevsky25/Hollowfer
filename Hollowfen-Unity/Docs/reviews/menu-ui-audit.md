# Menu / UI Front-End Audit (batch-55 follow-on)

**Date:** 2026-07-13 · driven in Play mode against the new IM Fell + EB Garamond fonts.
**Status:** findings only — awaiting Trevor's sign-off on scope/priority before any fixes (Batch 3+).
**Scope:** front-end shell only (fonts/typography, menu + settings screens, transitions, interaction
feedback). No gameplay/quest/cinematic changes.

Priority key: **P0** broken / blocks ship · **P1** clear polish gap · **P2** nice-to-have / taste.
Screens driven live this pass: MainMenu, Settings, Story, Field Guide, ConfirmModal, SaveSlot.
NOT yet driven live: in-game HUD/dialogue/inspect/narration, Pause, Loading welcome, Wren, full map
(all build through verified code paths; flagged below for a live pass during fixes).

---

## 1. AUDIO / FEEDBACK  — the biggest gap

The only UI sound in the entire shell is a single synthesized "tick" on page push/pop. There is no
per-control feedback and no menu ambience — the menu is **dead silent** until a screen transition.

- **P0-A1 · No UI SFX set beyond the transition click.**
  `UISfx` (`Assets/_Hollowfen/Scripts/UI/UISfx.cs`) exposes only `Click()`, called only from
  `UIManager.TransitionRoutine` (`UIManager.cs:282`). Missing: **nav-move** (focus change while arrowing
  the nav row / a list), **select/confirm**, **back/cancel**, **error** (e.g. pressing the disabled
  Continue, or an invalid action).
  *Fix:* extend `UISfx` with a small procedural set matching the existing synth tier — `Move` (soft, high,
  very short), `Select`/`Confirm` (warmer two-note), `Back` (descending), `Error` (muted low buzz). Hook
  **nav-move** off `EventSystem` selection changes (a global watcher or a hook in `UIScreen`/`FocusHighlight`,
  which already tracks focus), and **select/back** off the UI `Submit`/`Cancel` actions in `UIManager`.

- **P0-A2 · No menu ambience bed.**
  `Scene_MainMenu` has **no** `MusicManager`/`AudioSource` (grep: 0). This is Cinematic-Pass item #2, never
  built (top of TODOS.md). The forest hero art wants a soft bed.
  *Fix:* add a low forest-ambience loop on the menu (reuse `MusicManager`, or a sibling `AmbienceManager`),
  fade-in, routed to a Music/Ambience group so a slider governs it. Needs an actual ambience clip (asset).

- **P1-A3 · No dedicated Ambience/Voice mixer group.**
  `MainMixer.mixer` has only **Master / Music / SFX** groups; VO (`DialogueScreen`/`NarrationOverlay`) and
  `UISfx` both route to **SFX**. Settings exposes Master/Music/SFX sliders only.
  *Fix:* add an **Ambience** group (child of Master) for A2, and optionally a **Voice** group so VO has its
  own fader; expose the param(s) + add settings slider(s). Requires `.mixer` surgery — no public API (see
  `Docs/systems/audio.md`); done via SerializedObject on the mixer asset.

- **P1-A4 · Verify UISfx routing.** Confirm `_uiSfxOutput` is actually assigned on the scene `UIManager`
  instance — if null, even the transition click plays unrouted and ignores the SFX slider.

---

## 2. TYPOGRAPHY  — legacy sans holdouts (TMP-migration backlog)

The new fonts landed cleanly on every code-built + scene-serialized **TMP** surface (menu, Settings, Story,
Field Guide, ConfirmModal — all verified: Fell titles, EBG body, zero boxed glyphs). The remaining issues
are screens still on **legacy `UnityEngine.UI.Text`**, which TMP fonts cannot reach.

- **P1-T1 · SaveSlotScreen is legacy uGUI Text → renders Arial sans.**
  `SaveSlotScreen.cs:14-15` (`_slotLabels`, `_slotMetas` = `Text[]`) plus the scene-serialized "Choose a
  Slot" title and footer. Screenshot: `b55_saveslot.png` — bold sans amid an otherwise all-serif shell.
  *Fix:* rebuild to the code-built TMP idiom (the SettingsScreen template): `UICanvasUtil.NewHeading` for the
  title, `NewBody`/`NewEyebrow` for slot labels/metas; drop the serialized `Text` refs.

- **P1-T2 · LoadingScreen rolling caption is legacy Text.**
  `LoadingScreen.cs:25` (`_label = Text`) — the "Traveling to Hollowfen…" line is sans. (The welcome title
  block is already TMP/Fell.) So the New-Game → game handoff shows a sans caption over a Fell title.
  *Fix:* migrate `_label` to `TextMeshProUGUI` via `UICanvasUtil.NewBody`.

- **P2-T3 · Compass cardinal letters now EBG @13px (batch-55 change).** Converted from sans for consistency
  + the cartographer aesthetic. Confirm legibility on-device; reverting those specific labels to sans is a
  one-line change if undesired. (`CompassStrip`.)

- **P2-T4 · Italic is TMP synthetic skew.** The SDF assets are Regular-only (matches how Georgia worked); the
  real italic `.ttf` faces are in-repo (OFL) but unbaked. If true italics are wanted for dialogue emphasis /
  card subtitles, bake them as style/fallback assets.

- **P2-T5 · `CompassStrip.cs:13` still declares a legacy `Text _markPrefab`.** The live compass renders TMP
  (converted in batch-55), so confirm this field is dead and remove it, or migrate the prefab.

---

## 3. TRANSITIONS

- **P1-X1 · Font-pop through the New-Game flow.** MainMenu (Fell/EBG) → SaveSlot (**sans**, T1) → Loading
  (mixed, T2) → game. The mid-flow font swap is jarring; resolved once T1/T2 land.
- **P2-X2 · Screen push/pop fades looked clean** on every screen driven (no double-fades/flashes into
  Settings/Story/Field Guide/Modal). Not yet verified: **pause↔menu**, **menu↔game**, and stacked-overlay
  teardown (NarrationOverlay/IntroGuide) — drive these during the fix pass to rule out stuck overlays / HUD
  bleed.

---

## 4. LAYOUT / POLISH

- **Positive · Gamepad default focus is present on every screen** — all 11 screens override `DefaultSelected`
  (MainMenu→Continue/NewGame, Settings→per-tab control, etc.); focus observed applying on open.
- **P2-L1 · 1280×800 Steam-Deck pass not yet run** with the new font metrics (x-height/leading differ from
  Georgia). Verify no clipping/crowding of titles + nav at Deck resolution and inside safe areas.
- **P2-L2 · In-game surfaces not live-driven this batch** (HUD, dialogue, inspect, narration). They build via
  the verified `UICanvasUtil` factory, so they inherit Fell/EBG — but confirm layout + zero boxed glyphs in a
  live game-scene pass (the scene load is heavy; it dropped the bridge once this session).
- **P2-L3 · Rounded-chrome + FocusHighlight** looked consistent on the driven screens (batch-47 sweep holds);
  spot-check focus glow contrast against EBG's lighter weight vs Georgia during the fix pass.

---

## Suggested fix sequencing (Batch 3+, one coherent fix per batch)

1. **UI SFX set** (A1) — extend `UISfx` + hook focus/submit/cancel. Pure code, no assets. *(highest value)*
2. **Menu ambience + mixer group + settings slider** (A2/A3/A4) — needs an ambience clip + `.mixer` surgery.
3. **TMP migration: SaveSlot + Loading._label** (T1/T2/X1) — kills the last sans + the font-pop.
4. **Transition + layout polish** (X2/L1/L2/L3) — pause/menu/game transitions, 1280×800, live in-game pass.

---

## Resolution log (Trevor signed off "all four, suggested order" 2026-07-13)

- **A1 — UI SFX set** → **DONE `batch-56`**. Move/Select/Back/Confirm/Error, all routed to SFX group.
- **A2/A3/A4 — menu ambience** → **DONE `batch-57`**. Procedural forest bed on the menu + Ambience
  settings slider. Note: source-level trim (routed to Master), NOT a dedicated mixer node — deliberate,
  to avoid risky `.mixer` surgery on the shipping asset (flagged; a true node can be added later).
- **T1/T2/X1 — TMP migration** → **DONE `batch-58`**. SaveSlot + Loading legacy `Text` → TMP; `liveLegacyUIText=0`
  everywhere; New-Game flow is one consistent typeface.
- **X2/L2 — transitions + live in-game pass** → **VERIFIED `batch-59`** (no fixes needed). Drove
  menu→save-slot→loading→game live: clean fades, no font-pop, no stuck overlays. In-game census
  fell=5 ebg=27 **other=0** — HUD/quest tracker/prompts/inventory = EBG, quest name + loading title =
  IM Fell, **zero boxed glyphs, zero legacy sans**. Loading cinematic welcome + intro narration verified.
- **L1 — 1280×800 Steam Deck** → verified-by-design (CanvasScaler 1920×1080 ref, MatchWidthOrHeight 0.5 →
  uniform scale; no clipping at reference res). Recommend a quick manual Deck-resolution glance.
- **Open taste items for Trevor** (no change made): (a) **compass** cardinal letters now EBG @13px — keep
  (his call) or revert to sans; (b) **intro/act-break narration** renders in IM Fell italic (the display
  face, as it always used the heading font) — atmospheric, but could switch to EBG body for longer-passage
  readability if preferred (one-line change in NarrationOverlay); (c) T3–T5 minor (synthetic italic, stale
  `_markPrefab`); (d) a full code-built rebuild of MainMenu/SaveSlot/Loading remains optional cleanup.
