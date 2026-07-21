#!/usr/bin/env python3
"""Build or verify the dialogue text-to-voice manifest consumed by Unity.

The clip filename proves line ordering; the SHA-256 text fingerprint proves that an
edited line cannot silently keep an obsolete performance. Run after a complete cast
render, or pass ``--check`` in CI/local validation without modifying the manifest.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re

import numpy as np
import soundfile as sf

from generate_vo import (
    DIALOGUE_DIR,
    OUT_ROOT,
    dialogue_asset_names,
    parse_lines,
    sanitize_speaker,
)


MANIFEST = Path(OUT_ROOT) / "dialogue_manifest.json"


def text_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def build_entries() -> list[dict[str, object]]:
    entries: list[dict[str, object]] = []
    missing: list[Path] = []
    invalid: list[str] = []
    for dialogue in dialogue_asset_names():
        asset = Path(DIALOGUE_DIR) / f"{dialogue}.asset"
        for index, (speaker, text) in enumerate(parse_lines(str(asset))):
            relative = Path(dialogue) / f"{index:02d}_{sanitize_speaker(speaker)}.wav"
            if re.search(r"\\(?:n|u[0-9A-Fa-f]{4})", text):
                invalid.append(f"{dialogue}:{index}: undecoded YAML escape in spoken text")
            absolute = Path(OUT_ROOT) / relative
            if not absolute.is_file():
                missing.append(relative)
            else:
                try:
                    audio, sample_rate = sf.read(absolute, dtype="float32", always_2d=False)
                    if sample_rate != 24000 or audio.ndim != 1:
                        invalid.append(f"{relative}: expected 24kHz mono")
                    elif len(audio) == 0 or not np.isfinite(audio).all():
                        invalid.append(f"{relative}: empty or non-finite audio")
                    else:
                        peak = float(np.max(np.abs(audio)))
                        rms = float(np.sqrt(np.mean(np.square(audio))))
                        duration = len(audio) / sample_rate
                        words = max(1, len(re.findall(r"[A-Za-z0-9']+", text)))
                        maximum = max(3.2, words / 1.1 + 2.2)  # generous ~66 wpm hallucination guard
                        if not 0.01 <= peak <= 0.99 or rms < 0.002:
                            invalid.append(
                                f"{relative}: suspicious level peak={peak:.3f} rms={rms:.4f}"
                            )
                        elif duration > maximum:
                            invalid.append(
                                f"{relative}: {duration:.2f}s exceeds {maximum:.2f}s for {words} words"
                            )
                except Exception as error:
                    invalid.append(f"{relative}: unreadable WAV ({error})")
            entries.append(
                {
                    "dialogue": dialogue,
                    "line": index,
                    "speaker": speaker,
                    "textSha256": text_hash(text),
                    "clip": relative.as_posix(),
                }
            )
    if missing:
        sample = "\n".join(f"  {path}" for path in missing[:12])
        suffix = "\n  ..." if len(missing) > 12 else ""
        raise SystemExit(f"Refusing manifest: {len(missing)} dialogue WAVs are missing:\n{sample}{suffix}")
    if invalid:
        sample = "\n".join(f"  {problem}" for problem in invalid[:12])
        suffix = "\n  ..." if len(invalid) > 12 else ""
        raise SystemExit(f"Refusing manifest: {len(invalid)} dialogue WAVs failed QA:\n{sample}{suffix}")
    return entries


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify the committed manifest only.")
    args = parser.parse_args()

    payload = {"version": 1, "entries": build_entries()}
    encoded = json.dumps(payload, indent=2, ensure_ascii=False) + "\n"
    if args.check:
        if not MANIFEST.is_file():
            raise SystemExit(f"Voice manifest is missing: {MANIFEST}")
        if MANIFEST.read_text(encoding="utf-8") != encoded:
            raise SystemExit("Voice manifest is stale; regenerate it after the dialogue VO pass.")
        print(f"Voice manifest PASS: {len(payload['entries'])} current dialogue lines")
        return

    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(encoded, encoding="utf-8")
    print(f"Wrote {MANIFEST}: {len(payload['entries'])} dialogue lines")


if __name__ == "__main__":
    main()
