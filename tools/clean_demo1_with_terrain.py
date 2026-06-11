#!/usr/bin/env python3
"""
Clean the Demo 1 + Terrain export.

This GLB was produced after running the in-Unity TerrainToMesh editor script,
so it contains a baked TerrainMesh node alongside the village. We:

  1. KEEP these root nodes: World, TerrainMesh, Stones (Forest Pack)
  2. DROP everything else (8 documentation comment headers, Camera Dolly,
     Scene Manager, Canvas, EventSystem, Ambient Audio Objects, River
     Waypoints Main/Town, Exterior Light Probes, Volume).
  3. Pass through unchanged otherwise — auto-centering and offsets are
     applied at runtime by Village._loadBackdrop().

Output:
  public/world/demo3/demo1_village_with_terrain_clean.glb
"""

import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "public/world/demo3/demo1_village_with_terrain.glb"
OUT = ROOT / "public/world/demo3/demo1_village_with_terrain_clean.glb"

GLB_MAGIC = 0x46546C67
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942

KEEP_ROOT_NAMES = {"World", "TerrainMesh", "Stones (Forest Pack)"}


def read_glb(path):
    data = path.read_bytes()
    magic, version, total = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC:
        raise ValueError(f"Bad magic 0x{magic:08x}")
    pos = 12
    chunks = {}
    while pos < len(data):
        cl, ct = struct.unpack_from("<II", data, pos)
        pos += 8
        chunks[ct] = data[pos:pos + cl]
        pos += cl
    return json.loads(chunks[JSON_CHUNK].decode("utf-8")), chunks.get(BIN_CHUNK, b"")


def write_glb(path, j, b):
    jb = json.dumps(j, separators=(",", ":")).encode("utf-8")
    jb += b" " * ((4 - len(jb) % 4) % 4)
    bb = b + b"\x00" * ((4 - len(b) % 4) % 4)
    has_bin = len(bb) > 0
    total = 12 + 8 + len(jb) + (8 + len(bb) if has_bin else 0)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", GLB_MAGIC, 2, total))
        f.write(struct.pack("<II", len(jb), JSON_CHUNK))
        f.write(jb)
        if has_bin:
            f.write(struct.pack("<II", len(bb), BIN_CHUNK))
            f.write(bb)


def main():
    print(f"Reading: {SRC.relative_to(ROOT)}  ({SRC.stat().st_size/1e9:.2f} GB)")
    j, b = read_glb(SRC)
    print(f"  nodes: {len(j['nodes']):,}  meshes: {len(j['meshes']):,}  materials: {len(j['materials'])}")

    scene = j["scenes"][j.get("scene", 0)]
    original_roots = list(scene["nodes"])
    kept, dropped = [], []
    for ni in original_roots:
        n = j["nodes"][ni]
        name = n.get("name", "")
        if name in KEEP_ROOT_NAMES:
            kept.append((ni, name))
        else:
            dropped.append((ni, name or "<empty>"))

    scene["nodes"] = [ni for ni, _ in kept]

    print(f"\nKept ({len(kept)}):")
    for ni, name in kept:
        t = j["nodes"][ni].get("translation", [0,0,0])
        has_mesh = "mesh" in j["nodes"][ni]
        nkids = len(j["nodes"][ni].get("children", []))
        print(f"  {name:30s}  T=({t[0]:7.2f},{t[1]:7.2f},{t[2]:7.2f})  mesh={has_mesh}  kids={nkids}")

    print(f"\nDropped ({len(dropped)}):")
    for ni, name in dropped:
        print(f"  {name[:50]}")

    print(f"\nWriting: {OUT.relative_to(ROOT)}")
    write_glb(OUT, j, b)
    print(f"  size: {OUT.stat().st_size/1e9:.2f} GB")
    print("Done.")


if __name__ == "__main__":
    main()
