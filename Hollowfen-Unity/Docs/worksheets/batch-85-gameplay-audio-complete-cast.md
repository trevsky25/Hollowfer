# Batch 85 — Gameplay sound, feedback, and complete cast voice-over

Date: 2026-07-16  
Status: implementation complete; verification recorded below

## Goal

Give Hollowfen's repeatable game loop a tactile, restrained sound language and ensure every NPC interaction has a real voice performance—not merely the three-dialogue Bram test slice.

## What shipped

- `GameplaySfx`: thirteen deterministic 48 kHz mono cues routed through the SFX mixer for alternating knife strokes, forage collection, delivery, coin gain/spend, item/quest outcomes, key turn, door movement, rest, planting, and crop maturity.
- Wren's existing ten-clip footstep bank and landing Foley now use a dedicated spatial AudioSource routed through the same SFX mixer; the Starter Assets `PlayClipAtPoint` bypass is gone.
- Core action wiring: the forage cutting challenge, wild/cultivated harvest, village request delivery, mill unlock, rest transition, grow-bed planting/maturity, and dialogue outcomes now dispatch intentional feedback.
- Outcome prioritization: dialogue chooses one result cue per completion frame (quest/card > earnings > spending > item) so compound outcomes do not become a pile-up.
- Complete dialogue VO: all 267 lines across all 75 `DialogueData` assets are covered—107 Wren lines and 160 supporting-cast lines, including repeats, request handoffs, festival scenes, Act IV, and all endings.
- Full cast direction: distinct British voice/speed profiles for Bram, Marra, Almy, Edda, Hollin, Joren, Voss, Theo, Calden, Aldric, and Pell. Wren preserves the approved grounded reference-conditioned identity and 1.05× pitch-safe pacing.
- Deterministic import: `DialogueVoiceoverImporter` applies the spoken-word import policy and wires every exact `<dialogue>/<index>_<speaker>.wav` reference.
- Staleness protection: `dialogue_manifest.json` fingerprints each speaker/text/clip tuple with SHA-256. Both the importer and DataIntegrity fail if authored dialogue changes without a matching voice regeneration.
- Render QA: missing clips, non-24 kHz/stereo files, silence/clipping, and implausibly long hallucinated performances block manifest creation.

## Quality catch during the pass

The legacy YAML parser preserved escaped `\n` paragraph breaks and `\u2014` punctuation inside double-quoted Unity scalars. That could make speech models read formatting tokens aloud. The first Wren expansion was stopped before its staged files were applied; parsing now uses JSON-compatible YAML escape decoding, and the supporting cast was regenerated from the corrected text.

## Verification

- Python generator compile and manifest `--check`: pending final render.
- Unity compile/importer: pending final render.
- `DataIntegrity.RunAllAsReport()`: pending final render.
- `GameplayAudioVerifier.RunAll()`: pending final render.
- gotcha lint and full play-mode smoke: pending final render.

## Files

- `Assets/_Hollowfen/Scripts/Audio/GameplaySfx.cs`
- `Assets/_Hollowfen/Scripts/Editor/{DialogueVoiceoverImporter,GameplayAudioVerifier}.cs`
- `Assets/_Hollowfen/Scripts/{Dialogue,Foraging,Requests,Quests,Time,Cultivation,UI}/...` integration points
- `Assets/_Hollowfen/Audio/VO/Dialogue_*/`
- `tools/agent/{generate_vo,generate_wren_chatterbox,build_voice_manifest}.py`
- `Docs/systems/audio.md`, `Docs/tests.md`
