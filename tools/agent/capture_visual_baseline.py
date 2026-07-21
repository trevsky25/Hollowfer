#!/usr/bin/env python3
"""Capture Hollowfen's canonical 1280x800 UI set and a fixed village perf path.

The runner uses Unity CLI/Pipeline for live Editor control and Roslyn calls into the
Editor-only VisualBaselineHarness. Before Play Mode it runs Hollowfen's data-integrity
report and exact audit-build technical preflight. Every UI image must also pass the
active production UI verifier before ScreenCapture is queued.

Usage:
  python3 tools/agent/capture_visual_baseline.py
  python3 tools/agent/capture_visual_baseline.py --replace

Raw Unity startup logs are never read or emitted.
"""

from __future__ import annotations

import argparse
import json
import math
import shutil
import statistics
import struct
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from unity_pipeline import PipelineError, UnityPipeline


REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_ROOT = REPO_ROOT / "Hollowfen-Unity"
MAIN_MENU_SCENE = "Assets/_Hollowfen/Scenes/Scene_MainMenu.unity"
GAMEPLAY_SCENE = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity"
WIDTH = 1280
HEIGHT = 800

UI_CAPTURES = (
    ("main-menu", "01-main-menu-1280x800.png"),
    ("save-slot", "02-save-slots-1280x800.png"),
    ("settings", "03-settings-1280x800.png"),
    ("story", "04-story-index-1280x800.png"),
    ("story-detail", "05-story-detail-1280x800.png"),
    ("field-guide", "06-field-guide-1280x800.png"),
    ("mushroom-detail", "07-mushroom-detail-1280x800.png"),
    ("wren", "08-people-archive-1280x800.png"),
)

VILLAGE_PATH = (
    ("Crooked Pintle", 275.0, 88.0, 5.0),
    ("Village well", 286.0, 160.0, 0.0),
    ("Theo market road", 325.0, 213.0, 285.0),
    ("Joren forge", 198.0, 198.0, 315.0),
    ("Tobin mill", 233.0, 318.0, 180.0),
)


def csharp_string(value: str | Path) -> str:
    return json.dumps(str(value))


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise PipelineError(f"Capture is not a valid PNG: {path}")
    return struct.unpack(">II", header[16:24])


def wait_for_editor(pipeline: UnityPipeline, play_mode: str, timeout: float = 90.0) -> dict:
    deadline = time.monotonic() + timeout
    last = None
    expected_status = "playing" if play_mode == "playing" else "ready"
    while time.monotonic() < deadline:
        try:
            last = pipeline.command("editor_status")
        except PipelineError as error:
            # Entering/exiting Play Mode can briefly regenerate the descriptor or reject a
            # heartbeat while scripts reload. Treat that as transport churn, not a failed run.
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
    raise PipelineError(f"Editor did not settle in '{play_mode}' mode: {last}")


def wait_for_screen(pipeline: UnityPipeline, screen_id: str, timeout: float = 20.0) -> str:
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        last = pipeline.eval("return Hollowfen.EditorTools.VisualBaselineHarness.PresentationState();")
        parts = str(last).split("|")
        if len(parts) >= 3 and parts[0] == screen_id and parts[1] == "False" and parts[2] == "1280x800":
            return str(last)
        pipeline.eval(
            "UnityEditor.EditorApplication.Step(); return UnityEngine.Time.frameCount.ToString();"
        )
        time.sleep(0.12)
    raise PipelineError(f"UI screen '{screen_id}' did not settle: {last}")


def step_frames(pipeline: UnityPipeline, count: int) -> int:
    value = pipeline.eval(
        f"for (int i = 0; i < {count}; i++) UnityEditor.EditorApplication.Step();"
        "return UnityEngine.Time.frameCount;",
        timeout_ms=max(5000, count * 250),
    )
    return int(value)


def capture_screen(
    pipeline: UnityPipeline, screen_id: str, output: Path, replace: bool
) -> dict:
    if output.exists():
        if not replace:
            raise PipelineError(f"Capture already exists (use --replace): {output}")
        output.unlink()

    relative = output.relative_to(PROJECT_ROOT).as_posix()
    report = pipeline.eval(
        "return Hollowfen.EditorTools.VisualBaselineHarness.CaptureCurrentScreen(" +
        csharp_string(relative) + ");"
    )
    deadline = time.monotonic() + 20.0
    while time.monotonic() < deadline and (not output.exists() or output.stat().st_size == 0):
        step_frames(pipeline, 1)
        time.sleep(0.12)
    if not output.exists() or output.stat().st_size == 0:
        raise PipelineError(f"Unity did not finish capture: {relative}")

    size = png_size(output)
    if size != (WIDTH, HEIGHT):
        raise PipelineError(f"Capture {relative} is {size[0]}x{size[1]}, not {WIDTH}x{HEIGHT}.")
    return {"screen": screen_id, "path": relative, "bytes": output.stat().st_size, "gate": report}


def capture_ui_set(
    pipeline: UnityPipeline, output_dir: Path, replace: bool, isolated_save: Path
) -> list[dict]:
    pipeline.command("open_scene", path=MAIN_MENU_SCENE)
    pipeline.eval("return Hollowfen.EditorTools.VisualBaselineHarness.ConfigureGameView();")
    pipeline.eval(
        "System.IO.Directory.CreateDirectory(" + csharp_string(isolated_save) + ");" +
        "Hollowfen.Save.SaveManager.EditorArmSaveDirectoryOverrideForNextPlay(" +
        csharp_string(isolated_save) + ");return \"armed\";"
    )
    pipeline.command("set_autotick", enable=True, interval_ms=16)
    pipeline.command("editor_focus")
    pipeline.command("editor_play")
    wait_for_editor(pipeline, "playing")
    step_frames(pipeline, 45)
    print("visual-baseline: " + str(pipeline.eval(
        "return Hollowfen.EditorTools.VisualBaselineHarness.PrepareReferenceProgression();"
    )))

    captures: list[dict] = []
    for screen_id, filename in UI_CAPTURES:
        if screen_id == "story-detail":
            pipeline.eval(
                'return Hollowfen.EditorTools.VisualBaselineHarness.OpenSelectedDetail('
                '"story", "story-detail");'
            )
        elif screen_id == "mushroom-detail":
            pipeline.eval(
                'return Hollowfen.EditorTools.VisualBaselineHarness.OpenSelectedDetail('
                '"field-guide", "mushroom-detail");'
            )
        else:
            pipeline.eval(
                "return Hollowfen.EditorTools.VisualBaselineHarness.PrepareScreen(" +
                csharp_string(screen_id) + ");"
            )
        state = wait_for_screen(pipeline, screen_id)
        step_frames(pipeline, 12)
        result = capture_screen(pipeline, screen_id, output_dir / filename, replace)
        captures.append(result)
        print(f"visual-baseline: captured {screen_id} ({state})")
    return captures


def percentile(values: list[float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = max(0, min(len(ordered) - 1, math.ceil(len(ordered) * fraction) - 1))
    return ordered[index]


def aggregate_stop(name: str, samples: list[dict], step_times: list[float]) -> dict:
    renders = [sample.get("render") or {} for sample in samples]
    memories = [sample.get("memory") or {} for sample in samples]
    return {
        "name": name,
        "samples": len(samples),
        "timedEditorSteps": len(step_times),
        "editorStepMedianMs": statistics.median(step_times) if step_times else None,
        "editorStepP95Ms": percentile(step_times, 0.95),
        "editorStepMaxMs": max(step_times, default=None),
        "trianglesMedian": int(statistics.median(
            [int(render.get("triangles") or 0) for render in renders]
        )),
        "setPassMedian": int(statistics.median(
            [int(render.get("setPassCalls") or 0) for render in renders]
        )),
        "allocatedPeakBytes": max(
            [int(memory.get("totalAllocatedBytes") or 0) for memory in memories], default=0
        ),
    }


def capture_performance_path(
    pipeline: UnityPipeline, samples_per_stop: int, timed_frames: int, isolated_save: Path
) -> dict:
    pipeline.command("editor_stop")
    wait_for_editor(pipeline, "stopped")
    pipeline.eval(
        "Hollowfen.Save.SaveManager.EditorClearSaveDirectoryOverride();return \"cleared\";"
    )
    pipeline.command("open_scene", path=GAMEPLAY_SCENE)
    pipeline.eval(
        "System.IO.Directory.CreateDirectory(" + csharp_string(isolated_save) + ");" +
        "Hollowfen.Save.SaveManager.EditorArmSaveDirectoryOverrideForNextPlay(" +
        csharp_string(isolated_save) + ");return \"armed\";"
    )
    pipeline.command("editor_focus")
    pipeline.command("editor_play")
    wait_for_editor(pipeline, "playing")
    step_frames(pipeline, 180)

    stops = []
    for name, x, z, yaw in VILLAGE_PATH:
        staged = pipeline.eval(
            "return Hollowfen.EditorTools.VisualBaselineHarness.PlacePlayerForBaseline(" +
            f"{x:.3f}f, {z:.3f}f, {yaw:.3f}f);"
        )
        step_frames(pipeline, 45)
        timing_payload = pipeline.eval(
            "return Hollowfen.EditorTools.VisualBaselineHarness.MeasureEditorStepTimings(" +
            f"{timed_frames});",
            timeout_ms=max(10000, timed_frames * 500),
        )
        timing_result = json.loads(str(timing_payload))
        step_times = [float(value) for value in timing_result.get("milliseconds", [])]
        samples = []
        for _ in range(samples_per_stop):
            step_frames(pipeline, 3)
            sample = pipeline.command("get_performance_stats")
            if not isinstance(sample, dict):
                raise PipelineError(f"Unexpected performance response at {name}: {sample}")
            samples.append(sample)
        result = aggregate_stop(name, samples, step_times)
        result.update({"x": x, "z": z, "yaw": yaw, "staged": staged})
        stops.append(result)
        print(
            f"visual-baseline: sampled {name} — Editor-step p95 "
            f"{result['editorStepP95Ms'] if result['editorStepP95Ms'] is not None else 'n/a'} ms, "
            f"{result['trianglesMedian']:,} triangles"
        )

    return {
        "kind": "Unity Editor diagnostic; not standalone release evidence",
        "timingMethod": "EditorApplication.Step wall time; includes Editor overhead and is CPU-side only",
        "resolution": f"{WIDTH}x{HEIGHT}",
        "samplesPerStop": samples_per_stop,
        "timedFramesPerStop": timed_frames,
        "path": stops,
    }


def write_json(path: Path, value: dict, replace: bool) -> None:
    if path.exists() and not replace:
        raise PipelineError(f"Report already exists (use --replace): {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--batch", default="batch-120")
    parser.add_argument("--samples-per-stop", type=int, default=8)
    parser.add_argument("--timed-frames", type=int, default=60)
    parser.add_argument("--replace", action="store_true")
    args = parser.parse_args()
    if args.samples_per_stop < 3:
        parser.error("--samples-per-stop must be at least 3")
    if args.timed_frames < 3 or args.timed_frames > 300:
        parser.error("--timed-frames must be between 3 and 300")

    output_dir = PROJECT_ROOT / "Docs" / "screenshots" / args.batch
    pipeline = UnityPipeline(PROJECT_ROOT, timeout=45)
    isolated_root = Path(tempfile.mkdtemp(prefix="hollowfen-visual-baseline-"))
    initial_scene = MAIN_MENU_SCENE
    autotick_enabled = False

    try:
        status = wait_for_editor(pipeline, "stopped")
        if Path(status.get("projectPath", "")).resolve() != PROJECT_ROOT.resolve():
            raise PipelineError(f"Pipeline routed to the wrong project: {status.get('projectPath')}")
        print(f"visual-baseline: Unity CLI {pipeline.version()} · Editor {status.get('unityVersion')}")

        scenes = pipeline.command("list_open_scenes") or {}
        active = next((scene for scene in scenes.get("scenes", []) if scene.get("isActive")), None)
        if active and active.get("isDirty"):
            raise PipelineError("The active scene is dirty; save or revert it before baseline capture.")
        if active and active.get("path"):
            initial_scene = active["path"]

        audit = pipeline.eval(
            "return Hollowfen.EditorTools.ProductionBuildGate."
            "ValidateAuditPreflightForAutomation();"
        )
        integrity = str(pipeline.eval(
            "return Hollowfen.EditorTools.DataIntegrity.RunAllAsReport();"
        ))
        if "ERRORS=0" not in integrity:
            raise PipelineError("Data integrity failed; baseline capture aborted.")
        print(f"visual-baseline: {audit}")
        print("visual-baseline: " + integrity.splitlines()[0])

        pipeline.command("set_autotick", enable=True, interval_ms=16)
        autotick_enabled = True
        captures = capture_ui_set(
            pipeline, output_dir, args.replace, isolated_root / "ui-saves"
        )
        performance = capture_performance_path(
            pipeline, args.samples_per_stop, args.timed_frames, isolated_root / "gameplay-saves"
        )
        report = {
            "batch": args.batch,
            "project": PROJECT_ROOT.relative_to(REPO_ROOT).as_posix(),
            "unityCli": pipeline.version(),
            "unityEditor": status.get("unityVersion"),
            "auditPreflight": audit,
            "dataIntegrity": integrity.splitlines()[0],
            "captures": captures,
            "performance": performance,
        }
        write_json(output_dir / "baseline-report.json", report, args.replace)
        print(f"visual-baseline: PASS — {len(captures)} UI captures and "
              f"{len(performance['path'])} performance stops")
        return 0
    except (PipelineError, OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"visual-baseline: FAIL — {error}", file=sys.stderr)
        return 1
    finally:
        for _ in range(5):
            try:
                pipeline.command("editor_stop")
                wait_for_editor(pipeline, "stopped", timeout=30)
                break
            except Exception:
                time.sleep(0.5)
        try:
            pipeline.eval(
                "Hollowfen.Save.SaveManager.EditorClearSaveDirectoryOverride();return \"cleared\";"
            )
        except Exception:
            pass
        if autotick_enabled:
            try:
                pipeline.command("set_autotick", enable=False)
            except Exception:
                pass
        try:
            pipeline.command("open_scene", path=initial_scene)
        except Exception:
            pass
        shutil.rmtree(isolated_root, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
