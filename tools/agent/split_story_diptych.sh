#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: split_story_diptych.sh SOURCE_IMAGE OUTPUT_DIRECTORY" >&2
  exit 2
fi

story_source=$1
story_output_dir=$2
if [[ ! -f "$story_source" ]]; then
  echo "missing generated diptych: $story_source" >&2
  exit 3
fi

mkdir -p "$story_output_dir"
story_width=$(sips -g pixelWidth "$story_source" | awk '/pixelWidth:/ { print $2 }')
story_height=$(sips -g pixelHeight "$story_source" | awk '/pixelHeight:/ { print $2 }')
story_panel_height=$((story_height / 2 - 4))
story_crop_width=$((story_panel_height * 16 / 9))
if ((story_crop_width > story_width)); then
  story_crop_width=$story_width
fi
story_offset_x=$(((story_width - story_crop_width) / 2))
story_lower_y=$((story_height - story_panel_height))

sips -c "$story_panel_height" "$story_crop_width" \
  --cropOffset 0 "$story_offset_x" "$story_source" \
  -o "$story_output_dir/02-middle.png" >/dev/null
sips -z 940 1672 "$story_output_dir/02-middle.png" >/dev/null

sips -c "$story_panel_height" "$story_crop_width" \
  --cropOffset "$story_lower_y" "$story_offset_x" "$story_source" \
  -o "$story_output_dir/03-final.png" >/dev/null
sips -z 940 1672 "$story_output_dir/03-final.png" >/dev/null

echo "story diptych split: ${story_width}x${story_height} -> $story_output_dir"
