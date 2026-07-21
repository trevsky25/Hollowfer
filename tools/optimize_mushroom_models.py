"""Build runtime-budget mushroom FBXs from the original Meshy source drop.

Run from the repository root:
    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python tools/optimize_mushroom_models.py -- \
      --source-root "public/mushrooms/3D Models" \
      --project-root Hollowfen-Unity

The JSON manifest is shared with Unity's MushroomModelImporter. Originals stay
untouched; generated FBXs and canonical texture copies land under the Unity
project's Generated mushroom-model folder.
"""

import argparse
import json
import os
from pathlib import Path
import shutil
import sys

import bpy


MANIFEST_PATH = Path("Assets/_Hollowfen/Models/Mushrooms/MushroomModelManifest.json")
GENERATED_PATH = Path("Assets/_Hollowfen/Models/Mushrooms/Generated")


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", required=True)
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--only", nargs="*", default=[])
    return parser.parse_args(argv)


def mesh_objects():
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def triangle_count(objects):
    total = 0
    for obj in objects:
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def decimate_to(objects, target_triangles):
    current = triangle_count(objects)
    if current <= target_triangles:
        return current

    ratio = max(0.001, min(1.0, target_triangles / current))
    for obj in objects:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new(name="Hollowfen_Runtime_Budget", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return triangle_count(objects)


def export_fbx(objects, destination):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.stem + ".tmp.fbx")
    bpy.ops.export_scene.fbx(
        filepath=str(temporary),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        axis_forward="-Z",
        axis_up="Y",
    )
    os.replace(temporary, destination)


def copy_texture(source, destination):
    if not source.exists():
        raise FileNotFoundError(source)
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(destination.suffix + ".tmp")
    shutil.copy2(source, temporary)
    os.replace(temporary, destination)


def copy_model_textures(source_fbx, output_folder, key):
    stem = source_fbx.with_suffix("")
    mappings = {
        "": "Albedo",
        "_normal": "Normal",
        "_metallic": "Metallic",
        "_roughness": "Roughness",
        "_emission": "Emission",
    }
    for suffix, label in mappings.items():
        source = Path(str(stem) + suffix + ".png")
        copy_texture(source, output_folder / f"Mushroom_{key}_{label}.png")


def build_model(model, source_root, generated_root):
    key = model["key"]
    source_fbx = source_root / model["sourceFolder"] / model["sourceFile"]
    if not source_fbx.exists():
        raise FileNotFoundError(source_fbx)

    print(f"[MushroomOptimize] {key}: importing {source_fbx.name}", flush=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source_fbx), use_anim=False)
    objects = mesh_objects()
    if not objects:
        raise RuntimeError(f"{key}: FBX contains no mesh objects")

    output_folder = generated_root / key
    source_triangles = triangle_count(objects)
    journal_triangles = decimate_to(objects, int(model["journalTriangles"]))
    export_fbx(objects, output_folder / f"Mushroom_{key}_Journal.fbx")
    world_triangles = decimate_to(objects, int(model["worldTriangles"]))
    export_fbx(objects, output_folder / f"Mushroom_{key}_World.fbx")
    copy_model_textures(source_fbx, output_folder, key)

    print(
        f"[MushroomOptimize] {key}: {source_triangles:,} -> "
        f"journal {journal_triangles:,} -> world {world_triangles:,} triangles",
        flush=True,
    )


def main():
    args = parse_args()
    project_root = Path(args.project_root).expanduser().resolve()
    source_root = Path(args.source_root).expanduser().resolve()
    manifest_path = project_root / MANIFEST_PATH
    generated_root = project_root / GENERATED_PATH

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    selected = set(args.only)
    models = [m for m in manifest["models"] if not selected or m["key"] in selected]
    missing = selected.difference(m["key"] for m in models)
    if missing:
        raise ValueError("Unknown model key(s): " + ", ".join(sorted(missing)))

    for index, model in enumerate(models, start=1):
        print(f"[MushroomOptimize] Building {index}/{len(models)}", flush=True)
        build_model(model, source_root, generated_root)
    print(f"[MushroomOptimize] Complete: {len(models)} models", flush=True)


if __name__ == "__main__":
    main()
