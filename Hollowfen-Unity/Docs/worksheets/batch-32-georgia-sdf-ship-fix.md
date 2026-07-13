# Batch 32 — Georgia SDF ship fix (+ first Mac build attempt)

**Date:** 2026-07-12 · **Status:** DONE (verified, fable-reviewed) · tag `batch-32` (pending)
**TODOS item:** #15a — "Georgia SDF font config fix" (ship blocker). The Mac `.app` boot test is split to
15b (build App-Nap-stalled; see Build evidence).

## Diagnosis (empirical, via bridge)
- **Georgia SDF** (`Assets/UI/Fonts/Georgia SDF.asset`): `atlasPopulationMode = Dynamic`,
  `m_ClearDynamicDataOnBuild = 1`, only **61** glyphs baked (whatever rendered during past play
  sessions), 1024×1024 atlas, no own fallbacks.
- Georgia is **not** the TMP default (`TMP_Settings.defaultFontAsset = LiberationSans SDF`, Static,
  250 chars). Georgia is assigned per-component in `Scene_MainMenu`, `Scene_Hollowfen`, `MainMenu`.
- Global fallback **LiberationSans SDF - Fallback**: Dynamic, 3 glyphs baked.

**Why it's a ship blocker:** `ClearDynamicDataOnBuild=1` strips Georgia's baked glyph table at
build time; the player must regenerate every glyph at runtime via FreeType from the source .ttf —
slow, hitchy, fragile, and the whole point of a pre-baked SDF is lost. It is also the source of the
recurring git churn (`Georgia SDF.asset` re-serializes on every play as Dynamic bakes new glyphs).

## Fix (engineering, correct answer for a Latin-script game)
1. While still Dynamic, `TryAddCharacters` a comprehensive Latin set into Georgia:
   ASCII printable (U+0020–U+007E) + Latin-1 Supplement (U+00A0–U+00FF) + smart punctuation
   (U+2013 – U+2014 – U+2018 U+2019 U+201C U+201D U+2026). Guaranteed superset of all authored
   English content.
2. Switch Georgia `atlasPopulationMode → Static` and `clearDynamicDataOnBuild → false`
   (belt-and-suspenders) so the atlas ships intact and never self-modifies → **churn eliminated**.
3. Save the asset + its atlas texture sub-asset.
4. Verify: game text renders in-editor; play mode leaves the asset git-clean; coverage diff vs the
   union of all authored strings shows 0 missing; attempt a Mac player build + boot.

**CJK note:** Georgia is Latin-only; Simplified Chinese (future localization batch) will add its own
CJK font asset. Making Georgia Static now does not constrain that — Static fonts still accept
fallbacks. No Trevor decision entangled.

## Review gate
Text-rendering config change touching the whole game's primary font → FABLE-REVIEW GATE before
commit (per night-shift Model Tiering: "anything the integrity/lint gates can't validate").

## What shipped (final)
- **Georgia SDF**: Dynamic→**Static**, **201 glyphs / 2 atlas pages** (ASCII U+0020–7E + full Latin-1
  Supplement U+00A0–FF + General Punctuation en/em dash, curly quotes, ellipsis, bullet, guillemets),
  `TryAddCharacters` missing=0. `m_ClearDynamicDataOnBuild: 0`. `m_SourceFontFile: {fileID: 0}` (Georgia.ttf
  no longer ships — Microsoft-licensed; GUID kept for re-bakes). Fallback (`LiberationSans SDF - Fallback`)
  added to Georgia's own `fallbackFontAssetTable`.
- **LiberationSans SDF - Fallback**: Dynamic→**Static**, **204 glyphs** (ASCII + Latin-1 + → ← ○ that
  Georgia's serif source lacks + punctuation), source nulled, `ClearDynamicDataOnBuild: 0`. The 4 glyphs its
  source also lacks (✓ ✕ △ ◉) are genuinely homeless in every project font → QUESTIONS Q11.
- **All 3 project TMP_FontAssets are now Static** (audit: Georgia, LiberationSans SDF, the Fallback) — no
  Dynamic font remains to strip on build.

## Verification evidence
- **Root-cause + fix empirical**, all via the bridge: pre-fix Georgia was Dynamic w/ 61 stray glyphs +
  `ClearDynamicDataOnBuild=1`; TMP default is LiberationSans SDF (Georgia assigned per-component in scenes).
- **Churn-immunity PROVEN twice** (the recurring gotcha, now cured): sha256 of both `.asset` files
  byte-identical across a full play → render-heavy-glyph-string → stop → force-refresh cycle. Re-verified
  after the Latin-1 rebake + source-null.
- **Glyph resolution (play mode):** a string with café/naïve/déjà/Ölür + curly quotes + `→ ← ○ •` +
  punctuation rendered **41 via Georgia, 3 via fallback, 0 unresolved**, with `sourceFontFile == null`
  (proves Static renders from the baked atlas, not the source).
- **Full inventory basis:** enumerated every non-ASCII codepoint across all `Assets/Scripts` +
  `Assets/_Hollowfen` (19 unique); all now covered except ✓✕△◉ (no font source has them, pre-existing).
- **Gates:** lint 0/0 (1 waived), integrity 0/0 (26 quests / 70 dialogues / 11 NPCs / 14 locations /
  21 mushrooms). Re-run clean post-rebake.
- **Build:** dev StandaloneOSX build compiled ALL scripts with **zero errors** (only pre-existing
  third-party NatureManufacture warnings) and reached the BuildPlayer phase ("1 URP assets included in
  build") — proving no build-time compile errors — before the **backgrounded editor App-Nap-stalled**
  (log mtime froze, no bee_backend running, `NSAppSleepDisabled` not set this session). `.app` never
  written → boot test deferred to **item 15b**. Build-attempt shrapnel (UnityConnect m_Enabled 0→1, a
  `Resources/PerformanceTestRunInfo.json` that would ship, URP/ProjectSettings churn) reverted/deleted —
  NOT committed.

## Fable review (model: fable) — verdict PASS WITH CHANGES; all required changes applied
- **Latin-1 was silently dropped** from my own plan (baked ASCII+15 punct, not the promised Latin-1) →
  **re-baked** Georgia to full Latin-1 (201 glyphs, 2 pages via multi-atlas). ✅
- **`m_SourceFontFile` left live** → Georgia.ttf (MS-licensed) would ship + drags FreeType dep back →
  **nulled on both** (kept GUID). ✅
- **Build shrapnel in the tree** (UnityConnect re-enabled, PerformanceTestRunInfo in Resources/, URP churn)
  → **reverted/deleted**, tree is batch-32-only. ✅
- **Don't claim #15 done / fill worksheet / soften "FIXED"** → **#15 split into 15a (done) + 15b (boot +
  scene cleanup)**; steam-constraints reworded; this worksheet completed. ✅
- Reviewer independently confirmed: no `TMP_InputField` / runtime free-text path, no per-component fallback
  override, exactly 3 fonts, ✓✕△◉ genuinely absent. Added culture-format (U+202F narrow-NBSP) caveat to
  conventions for the localization batch.

## Docs updated
- `conventions.md` — new **Fonts (TMP) — ship config** section: Static rule, ship-font inventory, source-null
  + licensing, the reflection re-bake recipe (incl. re-link source from GUID), U+202F culture caveat.
- `steam-constraints.md` — ship-blocker line reworded (config fixed 15a; boot pending 15b), risk summary
  updated.
- `night-shift.md` — the "git checkout the Georgia SDF churn" gotcha marked FIXED (fonts now Static).
- `QUESTIONS.md` — Q11 (homeless ✓✕△◉ symbols → controller-glyph/icon pass).
- `TODOS.md` — #15 split 15a/15b; status snapshot + Q11.
