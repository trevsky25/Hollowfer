# Batch 101 — Modal and mission-card artifact cleanup

**Date:** 2026-07-17 · **Status:** verified

## Goal

Remove the visible rectangular backing around rounded confirmation dialogs and the first mission/objective card, then audit the shared popup family for the same generated-layer artifact. Per Trevor's standing direction, make and verify Unity project changes only; do not create or run a player build.

## Plan

- [x] Trace confirmation, intro-objective, and notification surface layers.
- [x] Reproduce the affected cards in Play Mode and isolate the offending layer(s).
- [x] Remove the artifact across the affected shared card family without flattening intended rounded surfaces.
- [x] Add regression verification for the cleaned hierarchy.
- [x] Compile and verify the corrected cards in Play Mode without building.
- [x] Update UI docs and finalize this worksheet.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Scope | `ConfirmModal` and `IntroGuide`; leave unrelated notifications unchanged | Live inspection confirmed both reported cards contained the anti-pattern. Other notifications were not implicated, so their visual treatment remains scoped for a separate audit if needed. |
| Surface treatment | Remove detached shadows and unclipped full-card decoration; keep the rounded base and inset accents | The scrim, surface contrast, hairline/rail, and typography already establish depth. Removing the offending layers fixes the silhouette without redesigning the cards. |
| Build | Do not build | Trevor explicitly requested Unity-only changes until he says otherwise. |

## Verification evidence

- Reproduced the attached Pause → Quit confirmation at 1920×1080. Runtime hierarchy showed `Canvas/Shadow` at `687.6×377.6` behind a `640×330` rounded card; disabling it removed the dark rectangular surround. Disabling the unclipped `Grain` and `Sheen` removed the remaining card-sized rectangle at the transparent corners.
- Reproduced the first `IntroGuide` objective card. Runtime hierarchy showed a `956×576` sibling shadow behind the `900×520` rounded card plus a stretched rectangular `SurfaceDepth`; disabling both restored one clean rounded silhouette.
- Rebuilt scripts and repeated both presentations from source:
  - Confirmation hierarchy: `Canvas/Shadow=False`, `Canvas/Card/Grain=False`, `Canvas/Card/Sheen=False`.
  - Intro hierarchy: `IntroGuideCanvas/Shadow=False`, `StoryObjectiveCard/SurfaceDepth=False`.
  - `ProductionUIVerifier`: `PASS · 0 critical · 0 advisory` on both visible presentations.
- Visual screenshots confirmed the rounded parchment confirmation and rounded ink objective card retain their fill, inset frame/rail, typography, buttons, and scrim with no surrounding rectangle.
- Unity compilation completed with zero Console errors.
- Data Integrity completed with `ERRORS=0 WARNINGS=0`.
- The temporary Slot 0 journal used to reach the real welcome/objective flow was deleted (`Ready → Empty`). Unity was returned to `Scene_MainMenu` in Edit Mode, and temporary verification screenshots were removed from `Assets/Screenshots`.
- No player build was created or run.

## Docs updated

- `Docs/systems/ui-framework.md` — records the shadow-free/clipped overlay-card contract and verifier coverage.

## Unfinished / handoff

No implementation work remains. Do not create a player build until Trevor explicitly requests one.

## Feedback to Trevor

This was the same broader composition lesson as the earlier Pause artifact: a rounded root does not clip rectangular children, and a detached procedural shadow can expose its own bounds when placed over a light card. The verifier now protects both reported surfaces from regaining those layers.
