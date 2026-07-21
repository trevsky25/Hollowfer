# Batch 98 — Companion score playlist

**Date:** 2026-07-17 · **Status:** complete

## Goal
Keep the approved `Misty Forest` musical identity while expanding Hollowfen into a long-form, low-repetition soundtrack: multiple related compositions, shuffled without immediate repeats, with quiet breathing room between cues instead of one three-minute file looping continuously.

## Plan
- [x] Audit the imported music library and current adaptive score implementation.
- [x] Render and inspect an original eight-track companion library.
- [x] Add shuffle-bag playback, cross-scene repeat protection, and randomized inter-track silence.
- [x] Wire menu/gameplay playlists and verify live routing, timing, and region behavior.
- [x] Run integrity checks, update the audio system doc, and finalize evidence.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Existing theme | Preserve `Misty Forest.wav` unchanged as the reference/title cue. | Trevor likes the current track; expansion should build around it rather than replace it. |
| New library | Eight original 48 kHz stereo modal ambient pieces, roughly 2.5–3.25 minutes each. | Nine total compositions yield a much longer recurrence window without inflating the build with uncurated tracks. |
| Playback | Shuffle bag, no immediate repeat across bag or scene boundaries, each track plays once. | Random selection alone can repeat; a bag guarantees variety while remaining unpredictable. |
| Pacing | Random silence between compositions, longer in gameplay than on the title screen. | Sparse arrivals create the requested Minecraft-like memory and leave room for the regional ambience. |
| Regional system | Preserve current volume and low-pass reactions independently of playlist selection. | Musical variety should not remove Old Wood/night restraint or future regional override support. |
| Delivery format | 224 kbps MP3 masters; Unity Streaming, background load, preload disabled. | Unity 6.4 recognizes MP3 as `AudioClip` on this project while `.m4a` imported as a generic asset; streaming avoids loading nine long decoded clips into memory. |

## Verification evidence

- Rendered eight 48 kHz stereo compositions at 224 kbps, 154–196 seconds each (total companion music 23:00; full nine-track library 26:13 including `Misty Forest`).
- FFmpeg EBU inspection: -16.8 to -16.6 LUFS integrated, 6.5–7.7 LU loudness range, -4.3 to -2.4 dBFS true peak. Pilot spectrogram showed distinct sections, musical partials, and intentional rests rather than a continuous noise bed.
- Unity importer inspection: all eight companion clips are `AudioClip`, 48 kHz stereo, `Streaming`, background-loaded, and not preloaded.
- Main Menu Play Mode: `MUSIC PLAYLIST — PASS` with all nine clips, both non-looping banks, full-bag uniqueness, refill-boundary protection, mixer routing, and 20–50 second quiet intervals.
- Gameplay Play Mode: the same playlist verifier passed with 45–120 second quiet intervals.
- Gameplay regression: `WORLD FEEDBACK — PASS`; all four regions, six trigger volumes, day/night ambience, score filters/levels, crossfades, and arrival title remained intact.
- Real scene transition: Main Menu began with `Misty Forest`; loading gameplay in the same Play session began with `Rain on the Mill Roof`. Explicitly advancing the gameplay bag visited all eight companions before returning to `Misty Forest` in its final slot.
- Script compile completed with zero project-code errors; console only reported existing Magic Pig package warnings. Generator `py_compile`, manifest JSON parse, and `git diff --check` passed.

## Docs updated

- `Docs/systems/audio.md` — records the nine-track library, playback contract, recurrence window, import settings, and verification surface.
- `Assets/_Hollowfen/Audio/Music/README.md` — source/authorship/regeneration notes.

## Unfinished / handoff

No implementation work remains. Final subjective listening/mix approval is Trevor's creative review; every track can be regenerated deterministically from the checked-in script and manifest.

## Feedback to Trevor

The soundtrack now contains the original song Trevor likes plus eight related companion pieces. Pure random selection was replaced with a shuffle bag, so every piece is heard before any can repeat, and quiet gaps make music arrive occasionally instead of behaving like a constant loop.
