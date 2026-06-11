#!/usr/bin/env python3
"""
Phase 1 — Bake a clean version of demo1_village.glb.

Demo 1 was exported from Unity (Magic Pig Games "Medieval Environment - Demo 1")
via UnityGLTF/glTFast. It has a much cleaner root structure than Demo 3 — the
village geometry lives under a single `World` container with four named groups:
Small Village, Main Town, Rivers, Terrain.

Problems we fix:
  1. Village is offset from world origin. Leaf-mesh positions span
     X[-356..0], Y[0..61], Z[0..321]. We want the village centered at
     (X=0, Z=0) with Y feet still on the ground (Y=0).
  2. Several root nodes are pure utility / Unity-only stuff that adds bytes
     and visual clutter:
       - 8 instruction-comment header objects ("CAMERA DOLLY MOVEMENT",
         "A/W/S/D/Q/E to move camera ...", etc.)
       - Camera Dolly (animated tour camera — irrelevant in our engine)
       - Scene Manager, Canvas (UI overlay), Volume (post-process)
       - Exterior Light Probes (lighting bake helpers, invisible)
       - Ambient Audio Objects, River Waypoints (audio sources / path nodes
         for Forest Birds + Flow river audio packs we don't own)

What we KEEP at the root:
  - World         (village + terrain + rivers placeholder + main town)
  - Stones (Forest Pack)  (real stone props placed in the scene)

Translation offset applied:
    (+177.91, 0, -160.72)
  Subtract X-midpoint, leave Y alone (Y already starts at ground), subtract
  Z-midpoint. Note this is a different sign convention than demo3 — Demo 1
  came out with a saner coordinate frame so we just shift directly.

The binary mesh chunk is preserved bit-for-bit; only the JSON header is
rewritten. This means the output file is the same ~1.84 GB as the input.
A separate compression pass (gltf-transform / gltfpack) is required to get
the file down to web-shippable size.

Output:
  public/world/demo3/demo1_village_clean.glb

Run from project root:
  python3 tools/clean_demo1_village.py
"""

import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "public/world/demo3/demo1_village.glb"
OUT = ROOT / "public/world/demo3/demo1_village_clean.glb"

GLB_MAGIC = 0x46546C67
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942

# Recentering offset, derived from village leaf-position midpoints.
OFFSET = (177.91, 0.0, -160.72)

# Root nodes to KEEP (everything else is dropped).
KEEP_ROOT_NAMES = {
    "World",
    "Stones (Forest Pack)",
}


def read_glb(path):
    data = path.read_bytes()
    magic, version, total_len = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC:
        raise ValueError(f"Not a GLB: bad magic 0x{magic:08x}")
    pos = 12
    chunks = {}
    while pos < len(data):
        ch_len, ch_type = struct.unpack_from("<II", data, pos)
        pos += 8
        chunks[ch_type] = data[pos:pos + ch_len]
        pos += ch_len
    j = json.loads(chunks[JSON_CHUNK].decode("utf-8"))
    b = chunks.get(BIN_CHUNK, b"")
    return j, b


def write_glb(path, j, b):
    json_bytes = json.dumps(j, separators=(",", ":")).encode("utf-8")
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


def clean(j):
    nodes = j["nodes"]
    scene = j["scenes"][j.get("scene", 0)]
    original_roots = list(scene["nodes"])

    kept = []
    dropped = []
    for ni in original_roots:
        n = nodes[ni]
        name = n.get("name", "")
        if name in KEEP_ROOT_NAMES:
            # Apply offset to this kept root's translation.
            t = n.get("translation", [0.0, 0.0, 0.0])
            n["translation"] = [
                t[0] + OFFSET[0],
                t[1] + OFFSET[1],
                t[2] + OFFSET[2],
            ]
            kept.append((ni, name))
        else:
            dropped.append((ni, name))

    scene["nodes"] = [ni for ni, _ in kept]
    return kept, dropped


def main():
    print(f"Reading: {SRC.relative_to(ROOT)}")
    j, b = read_glb(SRC)
    print(f"  JSON: {len(j['nodes']):,} nodes, {len(j['meshes']):,} meshes, "
          f"{len(j['materials'])} materials, {len(j.get('images', []))} images")
    print(f"  BIN:  {len(b):,} bytes ({len(b)/1e9:.2f} GB)")

    print(f"\nApplying offset: {OFFSET}")
    kept, dropped = clean(j)

    print(f"\nKept roots ({len(kept)}):")
    for ni, name in kept:
        t = j["nodes"][ni].get("translation", [0, 0, 0])
        print(f"  {name:30s}  T=({t[0]:7.2f},{t[1]:7.2f},{t[2]:7.2f})")

    print(f"\nDropped roots ({len(dropped)}):")
    for ni, name in dropped:
        display = name if name.strip() else "<empty/separator>"
        print(f"  {display[:60]}")

    print(f"\nWriting: {OUT.relative_to(ROOT)}")
    write_glb(OUT, j, b)
    print(f"  size: {OUT.stat().st_size:,} bytes ({OUT.stat().st_size/1e9:.2f} GB)")
    print("Done.")


if __name__ == "__main__":
    main()
