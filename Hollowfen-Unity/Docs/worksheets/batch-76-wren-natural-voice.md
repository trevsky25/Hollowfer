# Batch 76 — Wren natural-voice model upgrade

**Date:** 2026-07-15 · **Status:** DONE (audio staged, validated, applied, and import-checked)
**Directive:** Keep Wren's grounded British character while removing the uniform, synthetic cadence still audible in the Kokoro recast.

## Direction

- Replaced the active Wren/inner-narrator renders with reference-conditioned Chatterbox TTS; this is a model-level performance change, not pitch or playback-rate processing.
- Used the existing in-project British Wren scratch performance as the initial accent/identity seed. It is loaded once before rendering so the 24-clip cast cannot drift during a run.
- Dialogue uses modestly more expression than reflective narration. Both profiles lower classifier-free guidance to favor slower, less mechanical phrasing without becoming theatrical.
- Bram remains on the existing Kokoro `bm_lewis` performance and was not regenerated.
- This is still AI scratch VO. A clean 10–20 second authorized human Wren reference is the next quality tier and the honest shipping recommendation if synthetic artifacts remain objectionable.

## Clean implementation

- Added `tools/agent/generate_wren_chatterbox.py`.
- The script reuses the existing dialogue parser and canonical intro/journal/extra copy instead of introducing a second content map.
- It asserts exactly 24 Wren/Narrator clips, renders every file to a temporary stage, validates sample rate/channel/finite samples/levels/duration safety windows, and only copies to Unity after the entire set passes with `--apply`.
- The legacy Kokoro tool now skips Wren/Narrator unless `--allow-wren-scratch` is passed explicitly, so future Bram regeneration cannot silently roll the active recast back.
- Unity `.meta` files are preserved, so every serialized AudioClip reference keeps its GUID.
- The model runs locally on Apple Silicon through Torch MPS and outputs 24 kHz mono WAVs for Unity's locked 48 kHz mix.

## Coverage

- Homecoming dialogue: 2 Wren lines.
- Crooked Pintle key conversation: 6 Wren lines.
- Bram repeat conversation: 1 Wren line.
- Homecoming intro: 6 narrator beats.
- Hidden Journal: 7 narrator beats.
- Intro Guide: 1 narrator beat.
- Marra's Kitchen: 1 narrator beat.
- Total: 24 recast clips.

## Verification

- Generator coverage assertion: 24/24.
- Every staged WAV passed 24 kHz mono, finite/non-empty, peak/RMS, and previous-duration safety checks before apply; the live files are byte-identical to the validated stage.
- Independent live-asset sweep: 24 clips, `105.36s` total, duration range `0.60..12.28s`, peak range `0.195..0.942`, RMS range `0.0417..0.0813`.
- Applied only the 24 Wren/Narrator WAVs; Bram and `.meta` files were not modified by the recast.
- Unity refresh/import: `count=24 bad=0`, every clip imports mono at 24 kHz, duration range `0.600..12.280s`; project mix remains 48 kHz.
- Data Integrity: `ERRORS=0 WARNINGS=0`; post-refresh Unity console: 0 errors/warnings.

## Files

- `tools/agent/generate_wren_chatterbox.py`
- `Assets/_Hollowfen/Audio/VO/`
- `Docs/systems/audio.md`
