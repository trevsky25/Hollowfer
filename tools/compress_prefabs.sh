#!/usr/bin/env bash
# Compress every .glb in public/world/prefabs/ via gltf-transform.
# Output stays as binary .glb (no .opt suffix — that triggers gltf-transform
# to write separated glTF + .bin + .webp files, which is not what we want).
# Originals are preserved in public/world/prefabs_raw/.

set -euo pipefail

PREFAB_DIR="/Users/TrevorKist/Desktop/Hollowfen - The Failing Village/public/world/prefabs"
RAW_DIR="/Users/TrevorKist/Desktop/Hollowfen - The Failing Village/public/world/prefabs_raw"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

mkdir -p "$RAW_DIR"
cd "$PREFAB_DIR"

shopt -s nullglob
files=( *.glb )
total=${#files[@]}
done_count=0

for f in "${files[@]}"; do
  done_count=$((done_count + 1))
  if [[ -f "$RAW_DIR/$f" ]]; then
    echo "[$done_count/$total] skip already-raw-backed: $f"
    continue
  fi

  raw_size=$(stat -f%z "$f")
  echo "[$done_count/$total] $f ($(numfmt --to=iec --suffix=B "$raw_size"))"

  out="$TMP_DIR/$f"
  npx --yes --package=@gltf-transform/cli@latest -- gltf-transform optimize "$f" "$out" \
    --texture-size 1024 --texture-compress webp 2>&1 | tail -3

  if [[ ! -s "$out" ]]; then
    echo "  !! optimization produced empty file, leaving original in place"
    continue
  fi

  cp "$f" "$RAW_DIR/$f"
  mv "$out" "$f"
  new_size=$(stat -f%z "$f")
  echo "  -> $(numfmt --to=iec --suffix=B "$new_size")"
done

echo "---"
echo "TOTAL after compression:"
du -sh "$PREFAB_DIR"
