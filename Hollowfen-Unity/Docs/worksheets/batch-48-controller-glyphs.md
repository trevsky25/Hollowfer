# Batch 48 — PS5 controller button glyphs (closes Q11)

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-48` (pending)
**Directive:** Trevor — "can we get those icons for the button symbols added now? im using a ps5
controller so want to see triangle, circle, x, square, etc."

## What shipped
1. **`ControllerGlyphGenerator`** (Editor, menu `Hollowfen → Generate Controller Glyphs`, idempotent):
   procedurally draws a 7-glyph TMP sprite sheet — DualSense face buttons (dark chip + Sony legend
   colors: blue ✕, red ○, pink □, green △) + neutral `ui_x`/`ui_check` + `coin` — SDF-style AA line
   art, no external assets. Builds `Assets/_Hollowfen/UI/Glyphs/ControllerGlyphs.asset`
   (TMP_SpriteAsset, faceInfo.pointSize=64 so sprites render at running text size) and registers it as
   **TMP's project-wide default sprite asset** → `<sprite name="ps_triangle">` works in ANY TMP text.
2. **`ControllerGlyphs`** (runtime): one brand-aware resolver — `For(Face)` returns PS sprite tags on
   DualSense/DualShock, letter legends on Xbox ("Y/B/A/X") and Switch ("X/A/B/Y"). Replaces the three
   divergent copies of pad-brand detection.
3. Call sites: InteractionPromptHUD ("[E] / <△-icon> Talk to Bram"; pad part hidden when no pad),
   InspectScreen Forage(North)/Leave(East), InventoryScreen Close(East). Also swapped the other Q11
   boxed glyphs: CoinHUD `◉`→coin sprite, StoryDetailScreen `✕`→ui_x, PauseScreen "Saved ✓"→ui_check.

## Gotchas (hard-won, for the docs)
- A TMP_SpriteAsset saved WITHOUT `m_Version="1.1.0"` triggers TMP's legacy upgrade on load, which
  rebuilds the tables from the empty legacy sprite list and **silently wipes** the authored tables.
- `TMP_Settings.defaultSpriteAsset = x` (static setter) does not persist — set `m_defaultSpriteAsset`
  via SerializedObject on `TMP_Settings.instance` too.

## Verification
- [x] Asset: 7 named sprites, sheet + material live, TMP default sprite asset = ControllerGlyphs.
- [x] Visual (play, test label): all 7 render correctly inline — chips/colors/baseline right
      (`zoom_glyphs.png`), including the prompt/saved/coin compositions.
- Brand branch on real DualSense = Trevor's pass (detection logic unchanged from the shipped
  Inspect/Inventory code, only the PS output strings changed).
