# Batch 99 — Distinct gameplay score entry

**Date:** 2026-07-17 · **Status:** complete

## Goal

Make the transition into the world unmistakably musical: retain `Misty Forest` as the title theme, but always start gameplay on one of the eight companion pieces, including when `Scene_Hollowfen` is launched directly for testing.

## Plan

- [x] Confirm the packaged player and gameplay scene contain all nine compositions.
- [x] Confirm the full-bag runtime rotation uses nine unique, non-looping clips.
- [x] Exclude the title theme from gameplay's initial random selection.
- [x] Recompile, verify direct and menu-to-world entry, rebuild, and inspect the player package.

## Evidence so far

- The July 17 player package contains the companion-track asset names in `sharedassets0.assets`.
- Gameplay Play Mode observed `Misty Forest` plus all eight named companion compositions in one full-bag rotation.
- `MusicPlaylistVerifier` passed: nine streamed compositions, full-bag shuffle, no immediate repeats, two-source routing, and randomized quiet intervals.
- Direct `Scene_Hollowfen` Play Mode entry opened on `Homeward by Lantern`, not `Misty Forest`.
- Normal scene transition opened the menu on `Misty Forest` and gameplay on `Fields After Rain`.
- Release build succeeded with zero build errors at `/Users/TrevorKist/Desktop/Hollowfen - The Failing Village 0.1.1.app`.
- Package audit found the companion names in `sharedassets0.assets`; the final `Assembly-CSharp.dll` and gameplay `level1` hashes differ from the older player, confirming the runtime and scene changes reached the build.

## Docs updated

- `Docs/systems/audio.md` — records the title-theme exclusion on gameplay entry.

## Unfinished / handoff

No implementation work remains. The superseded interim `0.1.1` player created during this batch was deleted and replaced at the same path by the final verified build; that intermediate artifact is not recoverable, but its replacement is complete.
