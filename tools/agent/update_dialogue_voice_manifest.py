#!/usr/bin/env python3
"""Rebuild the Unity dialogue VO manifest from canonical DialogueData YAML assets.

The editor-side DialogueVoiceoverImporter treats this manifest as a staleness contract: every
line's speaker, text hash, and index-matched WAV path must agree before any references are wired.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

from generate_vo import DIALOGUE_DIR, OUT_ROOT, parse_lines, sanitize_speaker


def main() -> None:
    dialogue_root = Path(DIALOGUE_DIR)
    voice_root = Path(OUT_ROOT)
    assets = sorted(dialogue_root.rglob("Dialogue_*.asset"), key=lambda path: path.name)
    names: set[str] = set()
    entries: list[dict] = []
    missing: list[Path] = []

    for asset in assets:
        asset_name = asset.stem
        if asset_name in names:
            raise SystemExit(f"Duplicate dialogue asset filename: {asset_name}")
        names.add(asset_name)
        for index, (speaker, text) in enumerate(parse_lines(str(asset))):
            relative = Path(asset_name) / f"{index:02d}_{sanitize_speaker(speaker)}.wav"
            if not (voice_root / relative).is_file():
                missing.append(relative)
            entries.append(
                {
                    "dialogue": asset_name,
                    "line": index,
                    "speaker": speaker,
                    "textSha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
                    "clip": relative.as_posix(),
                }
            )

    if missing:
        sample = "\n".join(f"  {path}" for path in missing[:20])
        raise SystemExit(f"Refusing manifest update: {len(missing)} WAV files are missing:\n{sample}")

    destination = voice_root / "dialogue_manifest.json"
    temporary = destination.with_suffix(".json.new")
    temporary.write_text(
        json.dumps({"version": 1, "entries": entries}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    temporary.replace(destination)
    print(f"Wrote {len(entries)} dialogue lines from {len(assets)} assets to {destination}")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit("Cancelled before manifest replacement")
