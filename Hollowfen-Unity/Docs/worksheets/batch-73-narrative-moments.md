# Batch 73 — Objective narrative moments

**Date:** 2026-07-15 · **Status:** DONE (compile + data verified)
**Directive:** Generalize the Hidden Journal's painted/voiced discovery language across objectives and NPC interactions; begin with an illustrated Marra's Kitchen beat during her first quest conversation.

## Delivered

- Added `StoryMomentData` as the canonical authoring asset for card art/title, localized caption passages, beat-mapped paintings, VO, pacing, trigger type, and optional runtime-context focus.
- Added `StoryMomentDirector` to sequence prop focus → illustrated narration/title plate → restore, with a queue and skip seam.
- Added `NarrativePresentationSession` ref-counted leases. Dialogue, narration, and prop focus can nest without the first finisher restoring player control too early.
- Migrated Hidden Journal's three paintings, seven narrator clips, beat map, and focus lens from scene-authored arrays to `StoryMoment_Act1_HiddenJournal`.
- Removed the old per-component journal narration/focus arrays after migration, so quest props now have one authored cinematic route plus an optional runtime context transform.
- Added `DialogueData._transitionMoment`; Marra's first-basket node now dissolves into the clean `Marra's Kitchen` story-card painting with no overlaid UI copy, paced by Wren's 9.0-second narration before payment begins.
- Kept presentation separate from progression. Marra's art is previewed mid-scene, but the story card unlocks only after payment succeeds.
- Story-card notifications now defer until dialogue/cinematics/HUD ownership is released, fixing the journal toast playing invisibly behind its long reveal.
- Extended DataIntegrity: all 26 quests require exactly one matching StoryCard, a real unlock route, valid StoryMoment media/mappings/VO, one owning quest, and no replay from repeat dialogue.
- Extended Kokoro generation with targeted extra/speaker modes. Wren's final follow-up direction uses the British `bf_emma` source with slower dialogue/inner-narration delivery; Bram's clips were not overwritten.
- Locked Unity's output mix to 48 kHz after the editor initialized at 24 kHz against a 48 kHz music bed and produced 2× playback.

## Verification

- Unity compilation: zero Console errors after forced asset refresh.
- Data Integrity: `ERRORS=0 WARNINGS=0`; 26 quests, 30 story cards, and 2 authored story moments checked.
- Hidden Journal resolves 7 caption beats / 7 VO slots / 3 paintings.
- Marra resolves 1 caption beat / 1 VO slot / canonical `StoryCard_06_MarraKitchen` art.
- Regenerated Marra VO: 24 kHz mono Int16 WAV, 8.975 seconds; Unity imports it into the 48 kHz project mix without changing duration.
- Nested presentation locks release in order: `0 → 2 → 1 → 0`.
- Audio import check: mix 48 kHz; music 48 kHz / 193.480s; Marra VO 24 kHz / 8.975s. All 24 regenerated Wren/narrator WAVs are finite, non-empty, and below clipping.

## Files

- `Assets/_Hollowfen/Scripts/Data/StoryMomentData.cs`
- `Assets/_Hollowfen/Scripts/UI/StoryMomentDirector.cs`
- `Assets/_Hollowfen/Scripts/Cinematics/NarrativePresentationSession.cs`
- `Assets/_Hollowfen/Scripts/UI/NarrationOverlay.cs`
- `Assets/_Hollowfen/Scripts/UI/StoryCardToast.cs`
- `Assets/_Hollowfen/Scripts/Dialogue/DialogueData.cs`
- `Assets/_Hollowfen/Scripts/Dialogue/DialogueScreen.cs`
- `Assets/_Hollowfen/Scripts/Quests/QuestData.cs`
- `Assets/_Hollowfen/Scripts/Quests/QuestInteractable.cs`
- `Assets/_Hollowfen/Scripts/Editor/DataIntegrity.cs`
- `Assets/_Hollowfen/Data/StoryMoments/`
- `Assets/_Hollowfen/Audio/VO/MarraKitchen/`
- `tools/agent/generate_vo.py`
