# Batch 75 — Wren British voice direction

**Date:** 2026-07-15 · **Status:** SUPERSEDED by batch-76 (kept as casting history)
**Directive:** Give Wren an older-English, British sound while keeping her natural and appropriate for a grounded woman in her late twenties.

## Direction

- Recast Wren and her inner narrator together with Kokoro `bf_emma` so dialogue and narration remain one character.
- Kept the performance restrained and period-drama adjacent, not exaggerated Shakespeare or aristocratic parody.
- Dialogue remains at `0.90`; reflective narration remains at `0.82`.
- Regenerated only Wren/narrator files. Bram's established `bm_lewis` performance was not touched.

## Coverage

- Homecoming dialogue: 2 Wren lines.
- Crooked Pintle key conversation: 6 Wren lines.
- Bram repeat conversation: 1 Wren line.
- Homecoming intro: 6 narrator beats.
- Hidden Journal: 7 narrator beats.
- Intro Guide: 1 narrator beat.
- Marra's Kitchen: 1 narrator beat.
- Total: 24 regenerated clips.

## Verification

- All 24 WAVs: 24 kHz mono, finite, non-empty, peak range `0.352..0.792` (no clipping).
- Unity imports preserve timing: Marra `8.075s`, first homecoming narration `5.400s`, first Wren dialogue `2.025s`.
- Project mix remains 48 kHz.
- Data Integrity: `ERRORS=0 WARNINGS=0`.

## Files

- `tools/agent/generate_vo.py`
- `Assets/_Hollowfen/Audio/VO/`
- `Docs/systems/audio.md`
