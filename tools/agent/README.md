# tools/agent — Agent Helper Scripts
Scripts that make agent sessions faster and more reliable. Policy: when you find yourself repeating a multi-step incantation (driving the bridge, resetting state, capturing screenshots), turn it into a script here, document it below, and keep this list current.
Everything here must run with system python3/bash — no venv, no pip installs.
Committed to the repo on purpose: /tmp gets wiped (we lost the original unitymcp.py that way).
Status: seeded 2026-07-11 (Batch 12).

---

## Scripts

| Script | Purpose | Usage |
|---|---|---|
| `unitymcp.py` | Drive the Unity editor over MCP streamable-HTTP when session `mcp__UnityMCP__*` tools aren't registered (session started before Unity). | `python3 tools/agent/unitymcp.py list` · `python3 tools/agent/unitymcp.py call <tool> '<json>'` ⚠️ Recreated from protocol knowledge after the /tmp wipe — verify against the live bridge on next Unity session. |

## Wanted (build when first needed)

- `reset_state.sh` — clean-state dev reset (delete `saves/slot0.json`, clear PlayerPrefs via bridge).
- `run_tests.sh` — trigger Unity Test Framework EditMode/PlayMode runs via the bridge, report results (Phase 2).
- `lint_hollowfen.py` — conventions.md prohibited-pattern scanner with `--fix` (Phase 2, pre-commit hook).
- `screenshot_screens.py` — canonical-screen capture pass at 1280×800 for visual regression (Phase 3).

## Unity launch (no editor open)

```
"/Applications/Unity/Hub/Editor/6000.4.4f1/Unity.app/Contents/MacOS/Unity" -projectPath "<repo>/Hollowfen-Unity" &
osascript -e 'tell application "Unity" to activate'   # triggers asset refresh
```
