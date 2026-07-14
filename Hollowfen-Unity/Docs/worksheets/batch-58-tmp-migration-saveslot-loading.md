# Batch 58 — TMP migration: SaveSlot + Loading (menu-ui-audit fix #3)

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-58` (pending)
**Audit items:** T1 (SaveSlotScreen legacy uGUI Text → sans), T2 (LoadingScreen `_label` legacy Text),
X1 (font-pop through the New-Game flow). These were the last `UnityEngine.UI.Text` in the shell.

## Approach — in-place Text→TMP swap (not a full rebuild)
Chose a targeted component swap over a code-built rebuild: it preserves the already-verified layout
(rects, rounded rows, spacing) and only changes what was wrong — the font. Steps:
1. Script field types `Text[]`/`Text` → `TMP_Text[]`/`TMP_Text` (SaveSlotScreen `_slotLabels`/`_slotMetas`,
   LoadingScreen `_label`); `.text`/`.gameObject` calls are identical on `TMP_Text`.
2. In-scene, per legacy Text: capture (text/size/color/anchor/style), `DestroyImmediate` it, add
   `TextMeshProUGUI` on the same GameObject with mapped properties + font (Title → IM Fell, everything
   else → EB Garamond), reassign the serialized refs.

## What changed
- **SaveSlotScreen**: 10 Text → TMP — "Choose a Slot" (IM Fell), 4 "Journal N" labels + 4 metas + footer
  (EB Garamond). `_slotLabels`/`_slotMetas` reassigned to the new TMP by name/order.
- **LoadingScreen**: 2 Text → TMP — `LoadingLabel` (the "Traveling to Hollowfen…" rolling caption) +
  its `LOADING` eyebrow, both EB Garamond; `_label` repointed to `LoadingLabel`.
  (Gotcha hit + fixed: my first swap loop left `_label` pointing at the last-swapped Text (the eyebrow);
  re-assigned to `LoadingLabel` by GameObject name.)

## Verification (Play mode)
- `liveLegacyUIText = 0` — **zero `UnityEngine.UI.Text` active in any loaded scene**. The whole shell is TMP.
- SaveSlot screenshot `b58_saveslot.png`: "Choose a Slot" reads in IM Fell; Journal labels + metas +
  footer in EB Garamond; middle-dots `·` and apostrophes render; rounded rows + layout intact. No sans.
- Scene diff is the 12 Text→TMP swaps (10 SaveSlot + 2 Loading) + serialized-ref reassignments; the
  3 "rotation" lines flagged by the churn check are identical identity values (git context artifact,
  not real change). Compile clean, no errors.

## Test script for Trevor
1. Play from `Scene_MainMenu` → New Game → the slot screen: "Choose a Slot" is the tall Fell title;
   Journal names + descriptions are the EB Garamond body serif. No bold sans anywhere.
2. Continue an existing save → the loading card's "Traveling to Hollowfen…" caption is serif (EBG), no
   longer sans — the New-Game → SaveSlot → Loading flow is one consistent typeface now (X1 resolved).

## Next (final audit batch): batch-59 = transition/layout polish + 1280×800 Steam-Deck + a live in-game
pass (HUD/dialogue/inspect/narration) — audit X2/L1/L2/L3.
