#!/usr/bin/env python3
"""Recast Wren's dialogue and inner narration with Chatterbox TTS.

Unlike ``generate_vo.py`` (the lightweight Kokoro scratch-VO generator), this
script is deliberately reference-conditioned and staged.  It renders every
Wren/Narrator clip to a temporary directory, validates the complete set, and
only touches Unity assets when ``--apply`` is supplied.

The reference must be a voice recording the project is allowed to use.  A
clean, dry 10-20 second performance is ideal.  For the initial model upgrade we
can condition on the existing British scratch VO; replace that with an
authorized human performance before treating the voice as final cast audio.

Local setup (Apple Silicon is supported through MPS):
    brew install ffmpeg
    uv venv /tmp/hollowfen-chatterbox-env --python 3.11
    uv pip install --python /tmp/hollowfen-chatterbox-env/bin/python \
        chatterbox-tts soundfile 'setuptools<81'

Usage:
    /tmp/hollowfen-chatterbox-env/bin/python \
        tools/agent/generate_wren_chatterbox.py \
        --reference /path/to/authorized_wren_reference.wav

Add ``--apply`` after reviewing the staged results to replace the index-matched
Unity WAVs.  Existing .meta files are never modified.
"""

from __future__ import annotations

import argparse
import gc
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zlib

import numpy as np
import soundfile as sf

from generate_vo import (
    DIALOGUE_DIR,
    EXTRAS,
    INTRO_CAPTIONS,
    JOURNAL_CAPTIONS,
    OUT_ROOT,
    dialogue_asset_names,
    parse_lines,
    sanitize_speaker,
)


UNITY_VO_ROOT = Path(OUT_ROOT)
PROJECT_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_STAGE = PROJECT_ROOT / "output" / "voice-staging" / "wren-chatterbox-stage"
EXPECTED_CLIP_COUNT = 122
EXPECTED_SAMPLE_RATE = 24_000
GENERATOR_VERSION = 2
STAGE_MANIFEST_NAME = "_stage_manifest.json"
DEFAULT_BATCH_SIZE = 6
# Trevor's final pacing pass: a restrained tempo lift that preserves the
# Chatterbox performance's pitch and British character.
PLAYBACK_TEMPO = 1.05

# Conservative story-performance profiles.  Lower CFG weight avoids the
# hurried cadence Chatterbox can pick up from a prompt; modest exaggeration
# retains breath and emphasis without turning Wren theatrical.
PERFORMANCE = {
    "dialogue": {
        "exaggeration": 0.54,
        "cfg_weight": 0.32,
        "temperature": 0.74,
    },
    "narration": {
        "exaggeration": 0.46,
        "cfg_weight": 0.34,
        "temperature": 0.72,
    },
}


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def render_fingerprint(
    relative_path: Path,
    text: str,
    profile_name: str,
    reference_digest: str,
) -> str:
    payload = {
        "version": GENERATOR_VERSION,
        "relativePath": relative_path.as_posix(),
        "text": text,
        "profile": profile_name,
        "performance": PERFORMANCE[profile_name],
        "playbackTempo": PLAYBACK_TEMPO,
        "referenceSha256": reference_digest,
        "sampleRate": EXPECTED_SAMPLE_RATE,
    }
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def load_stage_manifest(stage_root: Path) -> dict:
    path = stage_root / STAGE_MANIFEST_NAME
    if not path.is_file():
        return {"version": GENERATOR_VERSION, "clips": {}}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"Unreadable Wren stage manifest: {path}: {exc}") from exc
    if data.get("version") != GENERATOR_VERSION or not isinstance(data.get("clips"), dict):
        return {"version": GENERATOR_VERSION, "clips": {}}
    return data


def save_stage_manifest(stage_root: Path, manifest: dict) -> None:
    path = stage_root / STAGE_MANIFEST_NAME
    temporary = path.with_suffix(".json.new")
    temporary.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    os.replace(temporary, path)


def iter_wren_clips():
    """Yield (relative output path, text, performance profile) in stable order."""
    for index, text in enumerate(INTRO_CAPTIONS):
        yield Path("HomecomingIntro") / f"{index:02d}_Narrator.wav", text, "narration"

    for index, text in enumerate(JOURNAL_CAPTIONS):
        yield Path("HiddenJournal") / f"{index:02d}_Narrator.wav", text, "narration"

    for group, utterances in EXTRAS.items():
        for index, (speaker, text) in enumerate(utterances):
            if speaker == "Narrator" and text:
                yield Path(group) / f"{index:02d}_Narrator.wav", text, "narration"

    for asset_name in dialogue_asset_names():
        asset_path = Path(DIALOGUE_DIR) / f"{asset_name}.asset"
        for index, (speaker, text) in enumerate(parse_lines(str(asset_path))):
            if speaker == "Wren" and text:
                yield Path(asset_name) / f"{index:02d}_{sanitize_speaker(speaker)}.wav", text, "dialogue"


def validate_reference(path: Path) -> None:
    if not path.is_file():
        raise SystemExit(f"Reference clip not found: {path}")
    info = sf.info(path)
    if info.channels != 1:
        raise SystemExit(f"Reference clip must be mono (found {info.channels} channels): {path}")
    if not 5.0 <= info.duration <= 30.0:
        raise SystemExit(
            f"Reference clip should be 5-30 seconds (found {info.duration:.2f}s): {path}"
        )


def validate_render(path: Path, destination: Path, expected_sample_rate: int, text: str) -> str:
    audio, sample_rate = sf.read(path, dtype="float32", always_2d=False)
    if sample_rate != expected_sample_rate:
        raise RuntimeError(f"{path}: expected {expected_sample_rate}Hz, found {sample_rate}Hz")
    if audio.ndim != 1:
        raise RuntimeError(f"{path}: expected mono audio, found shape {audio.shape}")
    if len(audio) == 0 or not np.isfinite(audio).all():
        raise RuntimeError(f"{path}: empty or non-finite audio")

    duration = len(audio) / sample_rate
    peak = float(np.max(np.abs(audio)))
    rms = float(np.sqrt(np.mean(np.square(audio))))
    if not 0.01 <= peak <= 0.99 or rms < 0.002:
        raise RuntimeError(f"{path}: suspicious level (peak={peak:.3f}, rms={rms:.4f})")

    words = max(1, len(re.findall(r"[A-Za-z0-9']+", text)))
    maximum_from_text = max(3.2, words / 1.1 + 2.2)
    if duration > maximum_from_text:
        raise RuntimeError(
            f"{path}: duration {duration:.2f}s exceeds the {maximum_from_text:.2f}s "
            f"safety window for {words} words"
        )

    # Catch repetitions/hallucinated continuations without forcing the new
    # performance to match Kokoro's exact pace.
    if destination.is_file():
        previous_duration = sf.info(destination).duration
        minimum = max(0.30, previous_duration * 0.40)
        maximum = max(2.50, previous_duration * 2.40)
        if not minimum <= duration <= maximum:
            raise RuntimeError(
                f"{path}: duration {duration:.2f}s is outside the safety window "
                f"{minimum:.2f}-{maximum:.2f}s (previous {previous_duration:.2f}s)"
            )

    return f"{duration:.2f}s peak={peak:.3f} rms={rms:.4f}"


def apply_playback_tempo(path: Path, sample_rate: int) -> None:
    """Shorten a rendered performance without changing Wren's vocal pitch."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise RuntimeError("ffmpeg is required for Wren's pitch-preserving tempo pass")

    retimed = path.with_name(path.stem + ".retimed.wav")
    try:
        subprocess.run(
            [
                ffmpeg,
                "-hide_banner",
                "-loglevel", "error",
                "-y",
                "-i", str(path),
                "-filter:a", f"atempo={PLAYBACK_TEMPO:.4f}",
                "-ar", str(sample_rate),
                "-ac", "1",
                "-c:a", "pcm_f32le",
                str(retimed),
            ],
            check=True,
        )
        retimed.replace(path)
    finally:
        if retimed.exists():
            retimed.unlink()


def apply_peak_headroom(path: Path, ceiling: float = 0.97) -> None:
    """Prevent a strong Chatterbox take from clipping after tempo processing."""
    audio, sample_rate = sf.read(path, dtype="float32", always_2d=False)
    if audio.ndim != 1 or len(audio) == 0:
        raise RuntimeError(f"Cannot normalize malformed render: {path}")
    peak = float(np.max(np.abs(audio)))
    if peak <= ceiling:
        return
    sf.write(path, audio * (ceiling / peak), sample_rate, subtype="FLOAT")


def staged_clip_is_current(
    stage_root: Path,
    manifest: dict,
    relative_path: Path,
    text: str,
    profile_name: str,
    reference_digest: str,
) -> bool:
    staged_path = stage_root / relative_path
    expected_fingerprint = render_fingerprint(
        relative_path,
        text,
        profile_name,
        reference_digest,
    )
    if manifest["clips"].get(relative_path.as_posix()) != expected_fingerprint:
        return False
    if not staged_path.is_file():
        return False
    try:
        validate_render(
            staged_path,
            UNITY_VO_ROOT / relative_path,
            EXPECTED_SAMPLE_RATE,
            text,
        )
    except RuntimeError as exc:
        print(f"discarding invalid staged clip {relative_path}: {exc}", flush=True)
        return False
    return True


def resolve_device(requested: str, torch) -> str:
    if requested != "auto":
        return requested
    if torch.backends.mps.is_available():
        return "mps"
    if torch.cuda.is_available():
        return "cuda"
    return "cpu"


def run_worker(args, clips: list[tuple[Path, str, str]], reference: Path) -> None:
    """Render one bounded batch, saving restart-safe progress after each clip."""
    import torch
    import torchaudio as ta
    from chatterbox.tts import ChatterboxTTS

    requested_paths = set(args.worker_path)
    selected = [clip for clip in clips if clip[0].as_posix() in requested_paths]
    found_paths = {clip[0].as_posix() for clip in selected}
    missing_paths = sorted(requested_paths - found_paths)
    if missing_paths:
        raise RuntimeError(f"Worker received unknown Wren clip paths: {missing_paths}")
    if not selected:
        raise RuntimeError("Worker received no Wren clips")

    device = resolve_device(args.device, torch)
    stage_root = args.stage_dir.expanduser().resolve()
    stage_root.mkdir(parents=True, exist_ok=True)
    reference_digest = file_sha256(reference)
    manifest = load_stage_manifest(stage_root)

    print(f"Loading Chatterbox on {device} for {len(selected)} clips...", flush=True)
    model = ChatterboxTTS.from_pretrained(device=device)
    if model.sr != EXPECTED_SAMPLE_RATE:
        raise RuntimeError(
            f"Chatterbox sample rate changed: expected {EXPECTED_SAMPLE_RATE}, found {model.sr}"
        )
    model.prepare_conditionals(
        str(reference),
        exaggeration=PERFORMANCE["narration"]["exaggeration"],
    )

    for relative_path, text, profile_name in selected:
        params = PERFORMANCE[profile_name]
        seed = 173 + zlib.crc32(relative_path.as_posix().encode("utf-8"))
        torch.manual_seed(seed)
        wav = model.generate(
            text,
            repetition_penalty=1.25,
            exaggeration=params["exaggeration"],
            cfg_weight=params["cfg_weight"],
            temperature=params["temperature"],
        )
        staged_path = stage_root / relative_path
        staged_path.parent.mkdir(parents=True, exist_ok=True)
        ta.save(str(staged_path), wav, model.sr)
        del wav
        apply_playback_tempo(staged_path, model.sr)
        apply_peak_headroom(staged_path)
        detail = validate_render(
            staged_path,
            UNITY_VO_ROOT / relative_path,
            model.sr,
            text,
        )
        manifest["clips"][relative_path.as_posix()] = render_fingerprint(
            relative_path,
            text,
            profile_name,
            reference_digest,
        )
        save_stage_manifest(stage_root, manifest)
        print(f"rendered {relative_path}  {detail}  «{text[:64]}»", flush=True)

        # Chatterbox can leave large temporary MPS allocations behind.  Clear
        # them after every line; the parent also retires this worker after a
        # small batch so a long cast render cannot pressure the whole machine.
        gc.collect()
        if device == "mps":
            torch.mps.synchronize()
            torch.mps.empty_cache()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--reference",
        type=Path,
        required=True,
        help="Authorized 5-30 second mono WAV used to condition Wren's identity and accent.",
    )
    parser.add_argument(
        "--stage-dir",
        type=Path,
        default=DEFAULT_STAGE,
        help=f"Temporary render directory (default: {DEFAULT_STAGE}).",
    )
    parser.add_argument(
        "--device",
        choices=("auto", "mps", "cpu", "cuda"),
        default="auto",
        help="Torch device. 'auto' prefers Apple MPS, then CUDA, then CPU.",
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Replace Unity WAVs only after the entire staged set validates.",
    )
    parser.add_argument(
        "--missing-only",
        action="store_true",
        help="Render only absent destinations, preserving already-approved Wren performances.",
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=DEFAULT_BATCH_SIZE,
        help=(
            "Clips per disposable renderer process. Smaller batches use less peak memory "
            f"(default: {DEFAULT_BATCH_SIZE})."
        ),
    )
    parser.add_argument(
        "--fresh",
        action="store_true",
        help="Discard restart-safe staged progress before rendering.",
    )
    parser.add_argument("--worker", action="store_true", help=argparse.SUPPRESS)
    parser.add_argument("--worker-path", action="append", default=[], help=argparse.SUPPRESS)
    args = parser.parse_args()

    reference = args.reference.expanduser().resolve()
    validate_reference(reference)

    clips = list(iter_wren_clips())
    if len(clips) != EXPECTED_CLIP_COUNT:
        raise SystemExit(
            f"Refusing recast: expected {EXPECTED_CLIP_COUNT} Wren/Narrator clips, found {len(clips)}. "
            "Update this generator alongside the dialogue content."
        )

    stage_root = args.stage_dir.expanduser().resolve()
    if args.worker:
        run_worker(args, clips, reference)
        return

    if not 1 <= args.batch_size <= 12:
        raise SystemExit("--batch-size must be between 1 and 12")
    if args.fresh and stage_root.exists():
        shutil.rmtree(stage_root)
    stage_root.mkdir(parents=True, exist_ok=True)

    target_clips: list[tuple[Path, str, str]] = []
    for relative_path, text, profile_name in clips:
        destination = UNITY_VO_ROOT / relative_path
        if args.missing_only and destination.is_file():
            print(f"retained {relative_path}", flush=True)
            continue
        target_clips.append((relative_path, text, profile_name))

    reference_digest = file_sha256(reference)
    manifest = load_stage_manifest(stage_root)
    pending: list[tuple[Path, str, str]] = []
    for relative_path, text, profile_name in target_clips:
        if staged_clip_is_current(
            stage_root,
            manifest,
            relative_path,
            text,
            profile_name,
            reference_digest,
        ):
            print(f"resumed {relative_path}", flush=True)
        else:
            pending.append((relative_path, text, profile_name))

    total_batches = (len(pending) + args.batch_size - 1) // args.batch_size
    script_path = Path(__file__).resolve()
    for offset in range(0, len(pending), args.batch_size):
        batch = pending[offset:offset + args.batch_size]
        batch_number = offset // args.batch_size + 1
        print(
            f"Starting memory-bounded Wren batch {batch_number}/{total_batches} "
            f"({len(batch)} clips)...",
            flush=True,
        )
        command = [
            sys.executable,
            str(script_path),
            "--reference", str(reference),
            "--stage-dir", str(stage_root),
            "--device", args.device,
            "--worker",
        ]
        for relative_path, _, _ in batch:
            command.extend(("--worker-path", relative_path.as_posix()))
        environment = os.environ.copy()
        environment["TOKENIZERS_PARALLELISM"] = "false"
        subprocess.run(command, check=True, env=environment)

    print("Validating complete staged Wren set...", flush=True)
    manifest = load_stage_manifest(stage_root)
    rendered: list[tuple[Path, Path, str]] = []
    for relative_path, text, profile_name in target_clips:
        if not staged_clip_is_current(
            stage_root,
            manifest,
            relative_path,
            text,
            profile_name,
            reference_digest,
        ):
            raise RuntimeError(f"Staged Wren clip is missing or stale: {relative_path}")
        staged_path = stage_root / relative_path
        destination = UNITY_VO_ROOT / relative_path
        detail = validate_render(staged_path, destination, EXPECTED_SAMPLE_RATE, text)
        rendered.append((staged_path, destination, text))
        print(f"  {relative_path}: {detail}", flush=True)

    if not args.apply:
        print(f"Validated {len(rendered)} clips. Staged only: {stage_root}")
        print("Re-run with --apply to replace the Unity WAVs.")
        return

    for staged_path, destination, _ in rendered:
        destination.parent.mkdir(parents=True, exist_ok=True)
        temporary = destination.with_suffix(".wav.new")
        shutil.copyfile(staged_path, temporary)
        os.replace(temporary, destination)
    missing_after_apply = [relative for relative, _, _ in clips if not (UNITY_VO_ROOT / relative).is_file()]
    if missing_after_apply:
        raise RuntimeError(f"Apply finished with {len(missing_after_apply)} missing destinations")
    print(
        f"Applied {len(rendered)} validated Wren/Narrator clips; "
        f"complete set is {len(clips)} at {UNITY_VO_ROOT}"
    )


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit("Cancelled; Unity assets were not changed unless validation had completed.")
