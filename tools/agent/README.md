# tools/agent — Agent Helper Scripts
Scripts that make agent sessions faster and more reliable. Policy: when you find yourself repeating a multi-step incantation (driving the bridge, resetting state, capturing screenshots), turn it into a script here, document it below, and keep this list current.
Everything here must run with system python3/bash — no venv, no pip installs.
Committed to the repo on purpose: /tmp gets wiped (we lost the original unitymcp.py that way).
Status: updated 2026-07-21 (Batch 125).

---

## Scripts

| Script | Purpose | Usage |
|---|---|---|
| `unitymcp.py` | Drive the Unity editor over MCP streamable-HTTP when session `mcp__UnityMCP__*` tools aren't registered (session started before Unity). Also importable as a library (`import unitymcp; unitymcp.rpc(...)`) — the smoke-test scripts do this. | `python3 tools/agent/unitymcp.py list` · `python3 tools/agent/unitymcp.py call <tool> '<json>'` (verified against the live bridge 2026-07-11) |
| `dashboard.py` | Generate Trevor's production board (static HTML) from TODOS.md + QUESTIONS.md + worksheets + git. Every wrap-up: regenerate, then republish the Artifact (stable URL) so the morning board is current. | `python3 tools/agent/dashboard.py [--output path]` |
| `lint_hollowfen.py` | Gotcha linter (legacy Input, dataPath, emoji in content, missing .meta, public fields on components). No Unity needed. Waivers: `lint_waivers.txt`. | `python3 tools/agent/lint_hollowfen.py` — exit 1 on unwaived errors |
| `run_integrity.py` | Run `DataIntegrity.RunAllAsReport()` in the live editor via the bridge; exit 1 on errors. See `Hollowfen-Unity/Docs/tests.md` for what it proves. | `python3 tools/agent/run_integrity.py` |
| `smoke_play.py` | Play-mode smoke: activate Unity (App Nap!), play, ≥240 frames, no new console errors, state sample, stop. | `python3 tools/agent/smoke_play.py [--min-frames N]` |
| `unity_pipeline.py` | Structured JSON client for the pinned native Unity CLI/Pipeline connection. It preserves exact command argument names (including underscores), supports commands with a `name` parameter, and never reads or emits Unity startup logs. | Import `UnityPipeline`, or run the baseline script below. Set `UNITY_CLI` only when the CLI is not at `~/.unity/bin/unity`. |
| `capture_visual_baseline.py` | Run audit preflight + data integrity, capture eight gate-checked 1280×800 UI states, and sample the five-stop village Editor diagnostic route using isolated temporary saves. Existing evidence is immutable unless `--replace` is explicit. | `python3 tools/agent/capture_visual_baseline.py [--replace] [--samples-per-stop 8] [--timed-frames 60]` |
| `capture_full_ui_audit.py` | Run production preflight + owned save isolation, then capture and measure the complete 43-route UI catalog at 1280×800 under both 100% and maximum 115% accessibility profiles. Uses the existing production UI verifier and never reads startup logs. | `python3 tools/agent/capture_full_ui_audit.py [--replace]` |

**Pre-commit gate**: `.githooks/pre-commit` runs lint always + integrity when the bridge is up. Enable per clone: `git config core.hooksPath .githooks`.

## Native Hollowfen commands

Batch 121 adds seven first-class, Editor-only commands: `hollowfen_health`,
`hollowfen_preflight`, `hollowfen_verifier_catalog`, `hollowfen_run_verifier`,
`hollowfen_begin_save_isolation`, `hollowfen_end_save_isolation`, and
`hollowfen_world_audit`. Use the catalog plus `dry_run=true` before confirmed verifier execution,
and bracket every state-mutating Play Mode verifier with the owned isolation commands. The full
command contracts, safe Play/reconnect/cleanup sequence, and production boundary live in
`Hollowfen-Unity/Docs/systems/agent-tooling.md`.

## Wanted (build when first needed)

- `reset_state.sh` — clean-state dev reset (delete `saves/slot0.json`, clear PlayerPrefs via bridge).
- A pixel-diff comparator with reviewed tolerances; Batch 120 establishes the immutable canonical captures but keeps visual judgment manual.

## Unity launch (no editor open)

```
"/Applications/Unity/Hub/Editor/6000.4.4f1/Unity.app/Contents/MacOS/Unity" -projectPath "<repo>/Hollowfen-Unity" &
osascript -e 'tell application "Unity" to activate'   # triggers asset refresh
```
