#!/usr/bin/env python3
"""
Phase 1 — Bake a clean version of demo3_village.glb.

The exported demo village has three problems that prevent it from being
loaded directly into the Three.js scene:

  1. Every named root node has absolute Unity world coordinates baked
     into its translation (X +139..+345, Y +90..+117, Z -161..-263),
     which means the village renders ~250 units away from world origin.
  2. 54 unnamed root nodes (LOD variants and orphan instance refs
     from Unity's prefab system) sit stacked at (0,0,0) — they would
     pile on top of the player spawn.
  3. A Lake Polygon is scaled 98× and offset; Hollowfen has a *dry*
     riverbed in its story, so the lake is not wanted.

This script applies the manifest's recorded `offset` to every named root
node, drops the 54 orphans, drops the Lake Polygon and Unity scene-
metadata placeholders (Camera_Orientation, Point Light), and re-emits
both the GLB and a recentered collider manifest. The binary mesh chunk
is preserved bit-for-bit; only the JSON header is rewritten.

Output:
  public/world/demo3/demo3_village_clean.glb
  public/world/demo3/manifest_clean.json

Run from project root:
  python3 tools/clean_demo3_village.py
"""

import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC_GLB = ROOT / "public/world/demo3/demo3_village.glb"
SRC_MANIFEST = ROOT / "public/world/demo3/manifest.json"
OUT_GLB = ROOT / "public/world/demo3/demo3_village_clean.glb"
OUT_MANIFEST = ROOT / "public/world/demo3/manifest_clean.json"

GLB_MAGIC = 0x46546C67
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942

# Nodes to drop entirely (Unity scene metadata + the lake — Hollowfen has a dry riverbed)
DROP_NODE_NAMES = {
    "Lake Polygon",
    "Camera_Orientation",
    "Point Light",
    "Placeholder for referenced Light in Prefab instance (1)",
}


def read_glb(path):
    """Return (json_dict, bin_bytes). Raises on malformed input."""
    data = path.read_bytes()
    magic, version, total_len = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC:
        raise ValueError(f"Not a GLB: bad magic 0x{magic:08x}")
    if version != 2:
        raise ValueError(f"Expected GLB version 2, got {version}")
    if total_len != len(data):
        print(f"  warn: header length {total_len} != file size {len(data)}", file=sys.stderr)

    pos = 12
    chunks = {}
    while pos < len(data):
        ch_len, ch_type = struct.unpack_from("<II", data, pos)
        pos += 8
        chunks[ch_type] = data[pos:pos + ch_len]
        pos += ch_len

    if JSON_CHUNK not in chunks:
        raise ValueError("Missing JSON chunk")
    j = json.loads(chunks[JSON_CHUNK].decode("utf-8"))
    b = chunks.get(BIN_CHUNK, b"")
    return j, b


def write_glb(path, j, b):
    """Write a GLB with the given JSON dict + binary chunk (pass-through)."""
    json_bytes = json.dumps(j, separators=(",", ":")).encode("utf-8")
    # JSON chunks must be padded to 4-byte boundary with 0x20 (space)
    json_pad = (4 - (len(json_bytes) % 4)) % 4
    json_bytes += b" " * json_pad

    bin_pad = (4 - (len(b) % 4)) % 4
    bin_bytes = b + (b"\x00" * bin_pad)

    has_bin = len(bin_bytes) > 0
    total = 12 + 8 + len(json_bytes) + (8 + len(bin_bytes) if has_bin else 0)

    with open(path, "wb") as f:
        f.write(struct.pack("<III", GLB_MAGIC, 2, total))
        f.write(struct.pack("<II", len(json_bytes), JSON_CHUNK))
        f.write(json_bytes)
        if has_bin:
            f.write(struct.pack("<II", len(bin_bytes), BIN_CHUNK))
            f.write(bin_bytes)


def clean_glb(j, offset_x, offset_y, offset_z):
    """Strip orphans + Unity metadata + Lake; recenter named nodes by the offset.

    Coordinate convention (verified empirically against the manifest's
    documented bounds): subtract X, ADD Y and Z. The manifest's offset
    field stores Y and Z with inverted sign relative to X — likely an
    artefact of the exporter's Unity-to-glTF handedness conversion.

    Returns (kept_count, dropped_count) for reporting.
    """
    nodes = j["nodes"]
    scene_idx = j.get("scene", 0)
    scene = j["scenes"][scene_idx]
    original_roots = scene["nodes"]

    kept_roots = []
    dropped_unnamed = 0
    dropped_lake_or_meta = 0
    recentered = 0

    for ni in original_roots:
        n = nodes[ni]
        name = n.get("name")

        if name is None:
            # Unnamed root = export-orphan LOD or hinge instance. Drop.
            dropped_unnamed += 1
            continue

        if name in DROP_NODE_NAMES:
            dropped_lake_or_meta += 1
            continue

        # Apply offset to recenter to world origin.
        # See docstring for sign convention: subtract X, add Y, add Z.
        if "translation" in n:
            t = n["translation"]
            n["translation"] = [t[0] - offset_x, t[1] + offset_y, t[2] + offset_z]
            recentered += 1

        kept_roots.append(ni)

    scene["nodes"] = kept_roots
    return {
        "original_root_count": len(original_roots),
        "kept": len(kept_roots),
        "dropped_unnamed_orphans": dropped_unnamed,
        "dropped_lake_or_metadata": dropped_lake_or_meta,
        "recentered": recentered,
    }


def clean_manifest(m):
    """Update manifest to point at the cleaned GLB.

    Verified by inspection: the original colliders are ALREADY in the
    village-local space (their X range is -107.86..+107.86, matching the
    documented bounds exactly). The Unity-world coordinates were only
    baked into the GLB node transforms, not into the collider list. So
    the colliders pass through unchanged — only the model filename and
    offset metadata need updating.
    """
    ox, oy, oz = m["offset"]["x"], m["offset"]["y"], m["offset"]["z"]
    new = dict(m)
    new["model"] = "demo3_village_clean.glb"
    new["source_offset_applied"] = {"x": ox, "y": oy, "z": oz}
    new["offset"] = {"x": 0.0, "y": 0.0, "z": 0.0}
    new["colliders"] = [dict(c) for c in m["colliders"]]
    return new


def main():
    print(f"Reading: {SRC_GLB.relative_to(ROOT)}")
    j, b = read_glb(SRC_GLB)
    print(f"  JSON: {len(j['nodes'])} nodes, {len(j['meshes'])} meshes, "
          f"{len(j['materials'])} materials")
    print(f"  BIN: {len(b):,} bytes")

    print(f"Reading: {SRC_MANIFEST.relative_to(ROOT)}")
    m = json.loads(SRC_MANIFEST.read_text())
    ox, oy, oz = m["offset"]["x"], m["offset"]["y"], m["offset"]["z"]
    print(f"  offset to apply: ({ox:.4f}, {oy:.4f}, {oz:.4f})")
    print(f"  colliders to recenter: {len(m['colliders'])}")

    print(f"Cleaning GLB...")
    stats = clean_glb(j, ox, oy, oz)
    for k, v in stats.items():
        print(f"  {k}: {v}")

    print(f"Cleaning manifest...")
    cm = clean_manifest(m)

    print(f"Writing: {OUT_GLB.relative_to(ROOT)}")
    write_glb(OUT_GLB, j, b)
    print(f"  size: {OUT_GLB.stat().st_size:,} bytes")

    print(f"Writing: {OUT_MANIFEST.relative_to(ROOT)}")
    OUT_MANIFEST.write_text(json.dumps(cm, indent=2))
    print(f"  size: {OUT_MANIFEST.stat().st_size:,} bytes")

    print("Done.")


if __name__ == "__main__":
    main()
