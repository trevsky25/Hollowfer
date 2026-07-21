#!/usr/bin/env python3
"""Capture and measure Hollowfen's complete production UI at 1280x800.

The runner uses the native Unity CLI/Pipeline connection, the Editor-only
FullUIAuditHarness, the existing ProductionUIVerifier, the owned Hollowfen save-isolation
commands, and the exact production preflight. It audits both the standard 100% interface
and maximum 115%/reduced-motion/caption-backing profile. Raw Unity startup logs are never
read or emitted.

Usage:
  python3 tools/agent/capture_full_ui_audit.py
  python3 tools/agent/capture_full_ui_audit.py --replace
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
import time
from pathlib import Path

from unity_pipeline import PipelineError, UnityPipeline


REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_ROOT = REPO_ROOT / "Hollowfen-Unity"
MAIN_MENU_SCENE = "Assets/_Hollowfen/Scenes/Scene_MainMenu.unity"
GAMEPLAY_SCENE = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity"
OUTPUT_ROOT = PROJECT_ROOT / "Docs" / "screenshots" / "batch-125-ui-audit"
WIDTH = 1280
HEIGHT = 800

MENU_ROUTES = (
    "main-menu",
    "save-slots",
    "loading",
    "settings-audio",
    "settings-graphics",
    "settings-controls",
    "settings-accessibility",
    "settings-credits",
    "confirm-modal",
    "story-index",
    "story-detail",
    "field-guide",
    "mushroom-detail",
    "people-archive",
    "purse",
    "restoration-ledger",
    "ending-credits",
)

GAMEPLAY_ROUTES = (
    "gameplay-hud",
    "request-tracker",
    "pause",
    "inventory",
    "inspect-known",
    "identify-study",
    "identify-quiz",
    "map",
    "dialogue-line",
    "dialogue-choices",
    "village-request",
    "cultivation",
    "apothecary-preparation",
    "apothecary-case-unstarted",
    "apothecary-case-investigation",
    "apothecary-case-decisions",
    "apothecary-case-followup",
    "apothecary-case-resolved",
    "intro-guide",
    "story-card-toast",
    "key-item-toast",
    "region-arrival-toast",
    "restoration-toast",
    "narration",
    "narration-cinematic",
    "cutting-hud",
)

PROFILES = (
    (0, "100-standard"),
    (2, "115-accessible"),
)


def csharp_string(value: str | Path) -> str:
    return json.dumps(str(value))


def hash_file(path: Path) -> str | None:
    if not path.is_file():
        return None
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise PipelineError(f"Capture is not a valid PNG: {path}")
    return struct.unpack(">II", header[16:24])


def wait_for_editor(
    pipeline: UnityPipeline, play_mode: str, timeout: float = 90.0
) -> dict:
    deadline = time.monotonic() + timeout
    expected_status = "playing" if play_mode == "playing" else "ready"
    last: object = None
    while time.monotonic() < deadline:
        try:
            last = pipeline.command("editor_status")
        except PipelineError as error:
            last = str(error)
            time.sleep(0.25)
            continue
        if (
            isinstance(last, dict)
            and last.get("status") == expected_status
            and not last.get("compiling")
            and not last.get("domainReloadInProgress")
            and last.get("playMode") == play_mode
        ):
            return last
        time.sleep(0.25)
    raise PipelineError(f"Editor did not settle in {play_mode!r}: {last}")


def step_frames(pipeline: UnityPipeline, count: int) -> int:
    """Wait for Pipeline autotick to advance real Unity frames.

    Calling EditorApplication.Step repeatedly inside one Roslyn evaluation prevents Unity from
    retiring JobTemp allocations between frames and can flood the console with false lifetime
    diagnostics. The audit already owns Pipeline autotick, so polling the real frame counter is
    both faithful to normal Play Mode and console-clean.
    """
    if count <= 0:
        return int(pipeline.eval("return UnityEngine.Time.frameCount;"))
    start = int(pipeline.eval("return UnityEngine.Time.frameCount;"))
    target = start + count
    deadline = time.monotonic() + max(10.0, count * 0.12)
    current = start
    while time.monotonic() < deadline:
        current = int(pipeline.eval("return UnityEngine.Time.frameCount;"))
        if current >= target:
            return current
        time.sleep(0.025)
    raise PipelineError(
        f"Pipeline autotick stalled at frame {current}; expected at least {target}."
    )


def wait_for_scene(pipeline: UnityPipeline, expected_path: str, timeout: float = 90.0) -> None:
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        last = pipeline.eval(
            "return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;"
        )
        if str(last) == expected_path:
            return
        step_frames(pipeline, 2)
        time.sleep(0.1)
    raise PipelineError(f"Scene did not settle at {expected_path}: {last}")


def clear_owned_outputs(replace: bool) -> None:
    if not OUTPUT_ROOT.exists():
        return
    existing = [path for path in OUTPUT_ROOT.rglob("*") if path.is_file()]
    if existing and not replace:
        raise PipelineError(
            f"Batch 125 evidence already exists ({len(existing)} files); use --replace."
        )
    for path in existing:
        path.unlink()
    for directory in sorted(
        [path for path in OUTPUT_ROOT.rglob("*") if path.is_dir()], reverse=True
    ):
        directory.rmdir()


def load_scene_in_play(pipeline: UnityPipeline, scene_name: str, expected_path: str) -> None:
    pipeline.eval(
        "UnityEngine.SceneManagement.SceneManager.LoadScene(" +
        csharp_string(scene_name) + "); return \"loading\";",
        timeout_ms=15000,
    )
    wait_for_scene(pipeline, expected_path)
    step_frames(pipeline, 180 if expected_path == GAMEPLAY_SCENE else 90)


def capture_route(
    pipeline: UnityPipeline,
    profile: str,
    route: str,
    replace: bool,
) -> dict:
    output = OUTPUT_ROOT / profile / f"{route}-1280x800.png"
    if output.exists():
        if not replace:
            raise PipelineError(f"Capture already exists: {output}")
        output.unlink()

    pipeline.eval(
        "return Hollowfen.EditorTools.FullUIAuditHarness.PrepareRoute(" +
        csharp_string(route) + ");",
        timeout_ms=15000,
    )
    # Stable screens get two seconds of real Pipeline-autoticked layout/fade time. The two
    # coroutine-owned transient notices need an earlier sample so the audit measures the notice,
    # not the clean HUD after their short hold has elapsed.
    settle_frames = 30 if route in {"region-arrival-toast", "restoration-toast"} else 120
    step_frames(pipeline, settle_frames)
    pipeline.eval("return Hollowfen.EditorTools.FullUIAuditHarness.FinalizeRoute();")
    step_frames(pipeline, 2)
    state = str(
        pipeline.eval("return Hollowfen.EditorTools.FullUIAuditHarness.PresentationState();")
    )
    if (
        not state.startswith(route + "|")
        or "|False|" not in state
        or f"|{WIDTH}x{HEIGHT}|" not in state
    ):
        raise PipelineError(f"Route {route!r} did not settle at 1280x800: {state}")

    relative = output.relative_to(PROJECT_ROOT).as_posix()
    payload = pipeline.eval(
        "return Hollowfen.EditorTools.FullUIAuditHarness.InspectAndCapture(" +
        csharp_string(route) + "," + csharp_string(profile) + "," +
        csharp_string(relative) + ");",
        timeout_ms=15000,
    )
    report = json.loads(str(payload))
    if int(report.get("visibleTextCount") or 0) == 0:
        raise PipelineError(f"Route {route!r} settled without visible text: {state}")
    deadline = time.monotonic() + 20.0
    while time.monotonic() < deadline and (
        not output.exists() or output.stat().st_size == 0
    ):
        step_frames(pipeline, 1)
        time.sleep(0.1)
    if not output.exists() or output.stat().st_size == 0:
        raise PipelineError(f"Unity did not finish capture: {relative}")
    if png_size(output) != (WIDTH, HEIGHT):
        raise PipelineError(f"Capture is not {WIDTH}x{HEIGHT}: {relative}")

    report.update(
        {
            "state": state,
            "capture": relative,
            "bytes": output.stat().st_size,
            "sha256": hash_file(output),
        }
    )
    status = "PASS" if str(report.get("productionVerifier", "")).startswith("PASS") else "FINDING"
    print(
        f"full-ui-audit: {profile}/{route} — {status}; "
        f"{report.get('visibleTextCount', 0)} text; "
        f"{len(report.get('textBelow14Pixels') or [])} below 14px; "
        f"{len(report.get('clippedText') or [])} clipped"
    )
    return report


def write_report(report: dict) -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    path = OUTPUT_ROOT / "full-ui-audit-report.json"
    path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--replace", action="store_true")
    args = parser.parse_args()

    pipeline = UnityPipeline(PROJECT_ROOT, timeout=60)
    initial_scene = MAIN_MENU_SCENE
    preference_snapshot: str | None = None
    isolation_started = False
    autotick_enabled = False
    captures: list[dict] = []
    real_save = PROJECT_ROOT / "saves" / "slot0.json"
    real_save_before = hash_file(real_save)

    try:
        clear_owned_outputs(args.replace)
        status = wait_for_editor(pipeline, "stopped")
        if Path(status.get("projectPath", "")).resolve() != PROJECT_ROOT.resolve():
            raise PipelineError("Pipeline routed to the wrong Unity project.")
        scenes = pipeline.command("list_open_scenes") or {}
        active = next(
            (scene for scene in scenes.get("scenes", []) if scene.get("isActive")), None
        )
        if active and active.get("isDirty"):
            raise PipelineError("The active scene is dirty; audit capture refuses to continue.")
        if active and active.get("path"):
            initial_scene = active["path"]

        preflight = pipeline.command("hollowfen_preflight")
        pipeline.command("hollowfen_begin_save_isolation", dry_run=True)
        isolation = pipeline.command("hollowfen_begin_save_isolation", confirm=True)
        isolation_started = True
        pipeline.command("open_scene", path=MAIN_MENU_SCENE)
        pipeline.eval("return Hollowfen.EditorTools.FullUIAuditHarness.ConfigureGameView();")
        pipeline.command("clear_console")
        pipeline.command("set_autotick", enable=True, interval_ms=16)
        autotick_enabled = True
        pipeline.command("editor_focus")
        pipeline.command("editor_play")
        wait_for_editor(pipeline, "playing")
        step_frames(pipeline, 90)
        pipeline.eval(
            "Hollowfen.Save.SaveManager.SetActiveSlot(0);"
            "Hollowfen.Save.SaveManager.WritePlaceholderToSlot(0);"
            "return Hollowfen.Save.SaveManager.SaveDirectory;"
        )
        preference_snapshot = str(
            pipeline.eval(
                "return Hollowfen.EditorTools.FullUIAuditHarness.CapturePreferenceSnapshot();"
            )
        )

        for scale_index, profile in PROFILES:
            if str(
                pipeline.eval(
                    "return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;"
                )
            ) != MAIN_MENU_SCENE:
                load_scene_in_play(pipeline, "Scene_MainMenu", MAIN_MENU_SCENE)
            pipeline.eval(
                "return Hollowfen.EditorTools.FullUIAuditHarness.ApplyProfile(" +
                str(scale_index) + ");"
            )
            step_frames(pipeline, 8)
            pipeline.eval(
                "return Hollowfen.EditorTools.FullUIAuditHarness.PrepareReferenceProgression();"
            )
            for route in MENU_ROUTES:
                captures.append(capture_route(pipeline, profile, route, args.replace))

            load_scene_in_play(pipeline, "Scene_Hollowfen", GAMEPLAY_SCENE)
            pipeline.eval(
                "return Hollowfen.EditorTools.FullUIAuditHarness.PrepareReferenceProgression();"
            )
            for route in GAMEPLAY_ROUTES:
                captures.append(capture_route(pipeline, profile, route, args.replace))

        console = pipeline.command("get_console_logs", types="error,warning", count=250)
        report = {
            "batch": "batch-125",
            "resolution": f"{WIDTH}x{HEIGHT}",
            "unityCli": pipeline.version(),
            "unityEditor": status.get("unityVersion"),
            "preflight": preflight,
            "isolation": isolation,
            "profiles": [profile for _, profile in PROFILES],
            "routeCountPerProfile": len(MENU_ROUTES) + len(GAMEPLAY_ROUTES),
            "captures": captures,
            "console": console,
            "summary": {
                "captureCount": len(captures),
                "productionFailures": sum(
                    not str(item.get("productionVerifier", "")).startswith("PASS")
                    for item in captures
                ),
                "routesWithTextBelow14Pixels": sum(
                    bool(item.get("textBelow14Pixels")) for item in captures
                ),
                "routesWithParagraphsBelow14Pixels": sum(
                    bool(item.get("paragraphsBelow14Pixels")) for item in captures
                ),
                "routesWithClippedText": sum(
                    bool(item.get("clippedText")) for item in captures
                ),
                "routesWithOffscreenText": sum(
                    bool(item.get("offscreenText")) for item in captures
                ),
            },
        }
        write_report(report)
        print(
            "full-ui-audit: COMPLETE — "
            f"{len(captures)} captures; "
            f"{report['summary']['productionFailures']} verifier findings; "
            f"{report['summary']['routesWithTextBelow14Pixels']} routes below 14px"
        )
        return 0
    except (PipelineError, OSError, ValueError, json.JSONDecodeError) as error:
        print(f"full-ui-audit: FAIL — {error}", file=sys.stderr)
        if captures:
            write_report(
                {
                    "batch": "batch-125",
                    "incomplete": True,
                    "error": str(error),
                    "captures": captures,
                }
            )
        return 1
    finally:
        if preference_snapshot is not None:
            try:
                pipeline.eval(
                    "return Hollowfen.EditorTools.FullUIAuditHarness.RestorePreferenceSnapshot(" +
                    csharp_string(preference_snapshot) + ");"
                )
                step_frames(pipeline, 3)
            except Exception as error:  # cleanup must continue through a disconnected Play reload
                print(f"full-ui-audit: preference cleanup warning — {error}", file=sys.stderr)
        for _ in range(5):
            try:
                pipeline.command("editor_stop")
                wait_for_editor(pipeline, "stopped", timeout=45)
                break
            except Exception:
                time.sleep(0.5)
        if autotick_enabled:
            try:
                pipeline.command("set_autotick", enable=False, interval_ms=16)
            except Exception as error:
                print(f"full-ui-audit: autotick cleanup warning — {error}", file=sys.stderr)
        if isolation_started:
            try:
                pipeline.command("hollowfen_end_save_isolation", dry_run=True)
                pipeline.command("hollowfen_end_save_isolation", confirm=True)
            except Exception as error:
                print(f"full-ui-audit: isolation cleanup warning — {error}", file=sys.stderr)
        try:
            pipeline.command("open_scene", path=initial_scene)
        except Exception as error:
            print(f"full-ui-audit: scene restore warning — {error}", file=sys.stderr)
        real_save_after = hash_file(real_save)
        if real_save_before != real_save_after:
            print(
                "full-ui-audit: SAFETY FAILURE — real slot0 hash changed",
                file=sys.stderr,
            )


if __name__ == "__main__":
    sys.exit(main())
