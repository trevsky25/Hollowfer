# Batch 120 — Scripted visual and performance baseline

**Date:** 2026-07-21 · **Status:** verified and ready to commit

## Goal

Turn the production queue's visual-regression/performance baseline into a repeatable 1280×800
Editor workflow through Unity CLI/Pipeline, while keeping Hollowfen's existing data-integrity,
production-preflight, and active-UI gates authoritative.

## Plan

- [x] Verify Pipeline and Coplay are simultaneously healthy on the pinned Hollowfen Editor.
- [x] Add a reusable Pipeline JSON client and one-command baseline runner.
- [x] Add narrow Editor-only staging helpers for Game View sizing, in-memory reference progression,
  UI capture, and deterministic village viewpoints.
- [x] Capture eight canonical UI presentations at 1280×800, with each state passing the active
  production UI verifier before capture.
- [x] Record a fixed five-stop village performance path as Editor diagnostic evidence.
- [x] Run compile, lint, integrity, smoke, production preflight, and package-exclusion regressions.
- [x] Update the queue, test manifest, benchmark record, and this worksheet.

## Safety boundary

- Pipeline remains Editor-only; no Runtime Pipeline manager or Player command surface is added.
- The runner calls `DataIntegrity.RunAllAsReport()`, the exact audit-build technical preflight, and
  `ProductionUIVerifier.VerifyActiveForAutomation()` instead of replacing or bypassing them.
- Reference story/field-guide unlocks exist only in Play Mode memory and are reset on Play exit.
- Gameplay sampling arms an isolated temporary save directory before Play Mode and removes it after.
- The runner reads structured Pipeline responses only; it never reads or publishes Unity startup logs.
- ProductionBuildGate's denylist and player assembly/plugin inspection remain unchanged.

## Verification evidence

- **Connection health:** native Unity CLI `1.0.0-beta.2` reported Unity `6000.4.4f1` ready,
  stopped, not compiling, pinned to this project, with a clean `Scene_MainMenu` and zero Editor
  console errors. Coplay `10.1.0` simultaneously reported the same project ready and its console
  returned zero errors; the fallback bridge then completed integrity and smoke checks.
- **Baseline runner:** `python3 tools/agent/capture_visual_baseline.py --samples-per-stop 6
  --timed-frames 60 --replace` returned PASS after the audit preflight and data-integrity gate.
  It captured eight 1280×800 PNGs and sampled all five route stops. Every screenshot passed the
  active production UI verifier before capture and was visually reviewed.
- **Defect caught:** the first run detected clipping on “The First Festival in Three Years.” Story
  card titles now retain the one-line layout while auto-sizing from 34px to 26px; the repeated full
  capture passed.
- **Performance reference:** Editor-step p95 ranged from 3.91ms at Tobin mill to 6.33ms at the
  village well. Median camera presentations ranged from 396,708 to 6,767,482 triangles and 79 to
  147 SetPass calls. These are matched-route Editor diagnostics, not standalone or Deck evidence;
  exact values live in `Docs/benchmarks.md` and `baseline-report.json`.
- **Production boundary:** the exact audit preflight returned `AUDIT PREFLIGHT — PASS
  (StandaloneOSX)`. That preflight still scans Player assemblies and plugin compatibility against
  the unchanged Coplay/Pipeline/Newtonsoft/Roslyn denylist. No Player-runtime automation was added.
- **Regression gates:** native Pipeline recompile completed with zero errors; Pipeline and Coplay
  data integrity each returned `ERRORS=0 WARNINGS=0`; gotcha lint returned `ERRORS=0 WARNINGS=0
  WAIVED=1`; Python byte-compilation passed; all PNG headers measured 1280×800; and the Coplay
  smoke ran 368 frames with zero pre-play or in-play errors.
- **Save safety:** the aggregate SHA-256 of the four real slot primaries and backups was unchanged
  before and after the baseline/smoke work:
  `2ad3ece7a596382a97b989b2a990e9a674b0f024f84842ecc593a7c1c0a48443`.

## Review verdict

**Performance: PASS.** The only runtime-facing change is bounded TMP auto-sizing when Story cards
are constructed. The Pipeline staging and timing code is Editor-only, adds no Player assembly or
hot-path work, and records the first matched-route numbers without treating them as a release-player
framerate claim.

## Files

- `tools/agent/unity_pipeline.py`
- `tools/agent/capture_visual_baseline.py`
- `Assets/_Hollowfen/Scripts/Editor/VisualBaselineHarness.cs`
- `Assets/_Hollowfen/Scripts/Editor/ProductionBuildGate.cs`
- `Assets/_Hollowfen/Scripts/UI/StoryScreen.cs`
- `Docs/screenshots/batch-120/`
- `Docs/benchmarks.md`
- `Docs/tests.md`
- `Docs/systems/menu-pages.md`
- `tools/agent/README.md`
- `../TODOS.md`

## Deferred

- Add a reviewed tolerant pixel-diff comparator when the team is ready to define acceptable
  antialiasing and RenderTexture variance. Batch 120 intentionally keeps image judgment manual.
- Profile a non-development release player on Steam Deck to prove the 60fps village floor. Editor
  step timing cannot make that certification claim.
