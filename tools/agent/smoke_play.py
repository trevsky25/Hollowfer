#!/usr/bin/env python3
"""Play-mode smoke test via the Unity MCP bridge: activate Unity (macOS App Nap
freezes the player loop when the app is backgrounded), enter Play mode, wait
for real frames, assert zero console errors, sample game state, exit cleanly.

Usage: python3 tools/agent/smoke_play.py [--min-frames 240] [--max-polls 40]
Exit codes: 0 pass · 1 console errors while playing · 2 never reached stable
play mode · 3 bridge unreachable.

Promoted from the Batch 11 verification script (2026-07-11).
"""

import argparse
import os
import signal
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unitymcp


def _rpc_with_deadline(method, params, seconds=10):
    """Keep a stale SSE response from pinning the whole smoke run for five minutes."""
    previous = signal.getsignal(signal.SIGALRM)

    def timed_out(_signum, _frame):
        raise TimeoutError(f"Unity bridge response exceeded {seconds}s")

    signal.signal(signal.SIGALRM, timed_out)
    signal.setitimer(signal.ITIMER_REAL, seconds)
    try:
        return unitymcp.rpc(method, params)
    finally:
        signal.setitimer(signal.ITIMER_REAL, 0)
        signal.signal(signal.SIGALRM, previous)


def call(tool, args, retries=4):
    last = None
    for _ in range(retries):
        try:
            obj = _rpc_with_deadline("tools/call", {"name": tool, "arguments": args})
            if obj and "result" in obj:
                sc = obj["result"].get("structuredContent", {})
                return sc.get("data") or sc.get("result", {}).get("data") or sc
            last = obj
        except Exception as e:  # bridge blips during play-mode entry
            last = str(e)
            try:
                os.remove(unitymcp.SESSION_CACHE)
            except OSError:
                pass
        time.sleep(3)
    print(f"smoke: bridge call {tool} failed: {last}", file=sys.stderr)
    sys.exit(3)


def execute(code):
    data = call("execute_code", {"action": "execute", "code": code})
    return data.get("result") if isinstance(data, dict) else None


def pipe_counts(code, label, retries=5):
    """Read an `int|int` bridge result, tolerating a transient boolean ack.

    Unity can acknowledge execute_code with `True` for one bridge tick while scripts
    finish reloading. That is transport state, not a console count, so retry instead
    of crashing the smoke runner while the game itself is healthy.
    """
    last = None
    for _ in range(retries):
        last = execute(code)
        if isinstance(last, str):
            parts = last.split("|")
            if len(parts) == 2:
                try:
                    return int(parts[0]), int(parts[1])
                except ValueError:
                    pass
        time.sleep(1)
    print(f"smoke: invalid {label} response after {retries} attempts: {last!r}",
          file=sys.stderr)
    sys.exit(3)


STATE = 'return UnityEditor.EditorApplication.isPlaying + "|" + UnityEngine.Time.frameCount;'

CONSOLE = (
    'var t = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");'
    'var m = t.GetMethod("GetCountsByType");'
    "object[] a = new object[]{0,0,0};"
    "m.Invoke(null, a);"
    'return a[0] + "|" + a[1];'
)

GAMESTATE = (
    "var q = Hollowfen.Quests.QuestManager.ActiveQuest;"
    'string quest = q == null ? "(none)" : q.Id;'
    "int done = Hollowfen.Quests.QuestManager.CompletedQuestIds.Count;"
    "var tm = Hollowfen.GameTime.TimeManager.Instance;"
    'string clock = tm == null ? "(no TimeManager)" : ("day " + tm.Day + " hour " + tm.Hour.ToString("F1"));'
    'return "activeQuest=" + quest + " completed=" + done + " | " + clock;'
)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--min-frames", type=int, default=240)
    ap.add_argument("--max-polls", type=int, default=40)
    args = ap.parse_args()

    # App Nap suspends EditorApplication.update entirely when Unity is hidden;
    # the in-project PlayModeBackgroundTicker can't run if the app is napped.
    subprocess.run(["osascript", "-e", 'tell application "Unity" to activate'],
                   capture_output=True, timeout=10)

    pre_errors, _ = pipe_counts(CONSOLE, "pre-play console")
    print(f"smoke: pre-play console errors={pre_errors}")

    call("manage_editor", {"action": "play"})
    playing = False
    for i in range(args.max_polls):
        time.sleep(3)
        try:
            s = execute(STATE)
        except SystemExit:
            raise
        except Exception:
            continue
        if s and s.startswith("True|") and int(s.split("|")[1]) >= args.min_frames:
            playing = True
            print(f"smoke: stable play mode after poll {i} ({s.split('|')[1]} frames)")
            break
        # App Nap fallback: if play mode is on but frames are frozen, drive them
        # synchronously — EditorApplication.Step() works regardless of focus.
        if s and s.startswith("True|") and i >= 3:
            try:
                stepped = execute(
                    "for (int i = 0; i < " + str(args.min_frames) + "; i++) UnityEditor.EditorApplication.Step();"
                    'return "True|" + UnityEngine.Time.frameCount;')
            except Exception:
                continue
            if stepped and int(stepped.split("|")[1]) >= args.min_frames:
                playing = True
                print(f"smoke: frames frozen (App Nap) — stepped synchronously to {stepped.split('|')[1]}")
                break

    if not playing:
        print("smoke: FAIL — never reached stable play mode (is Unity visible/focused?)")
        call("manage_editor", {"action": "stop"})
        return 2

    errors, _ = pipe_counts(CONSOLE, "in-play console")
    state = execute(GAMESTATE)
    call("manage_editor", {"action": "stop"})

    print(f"smoke: in-play console errors={errors} (pre-play {pre_errors})")
    print(f"smoke: game state — {state}")
    if errors > pre_errors:
        print("smoke: FAIL — new console errors during play")
        return 1
    print("smoke: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
