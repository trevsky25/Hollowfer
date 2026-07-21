"""Build the menu-safe Wren study model from the untouched Meshy source.

Run from the repository root:
    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python tools/optimize_wren_journal_model.py -- \
      --source "public/models/wren/Wren-Final/Wren-Meshy.fbx" \
      --texture-root "Hollowfen-Unity/Assets/Characters/Wren" \
      --project-root Hollowfen-Unity

The gameplay character remains untouched. This produces one humanoid, skinned
journal derivative with a 90k-triangle ceiling and right-sized texture copies.
"""

import argparse
import os
from pathlib import Path
import sys

import bpy


GENERATED_PATH = Path("Assets/_Hollowfen/Models/Characters/Wren/Generated")
TARGET_TRIANGLES = 90_000


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--texture-root", required=True)
    parser.add_argument("--project-root", required=True)
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
        modifier = obj.modifiers.new(name="Hollowfen_Journal_Budget", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        # Decimate the bind-pose mesh before Blender evaluates the Armature
        # modifier. This preserves the rig and avoids baking a transient pose.
        obj.modifiers.move(obj.modifiers.find(modifier.name), 0)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return triangle_count(objects)


def export_character(destination):
    export_objects = [
        obj for obj in bpy.context.scene.objects if obj.type in {"ARMATURE", "MESH"}
    ]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in export_objects:
        obj.select_set(True)
    armature = next((obj for obj in export_objects if obj.type == "ARMATURE"), export_objects[0])
    bpy.context.view_layer.objects.active = armature

    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.stem + ".tmp.fbx")
    bpy.ops.export_scene.fbx(
        filepath=str(temporary),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        axis_forward="-Z",
        axis_up="Y",
    )
    os.replace(temporary, destination)


def resize_texture(source, destination, maximum_size):
    if not source.exists():
        raise FileNotFoundError(source)
    destination.parent.mkdir(parents=True, exist_ok=True)
    image = bpy.data.images.load(str(source), check_existing=False)
    scale = min(1.0, maximum_size / max(image.size[0], image.size[1]))
    width = max(1, round(image.size[0] * scale))
    height = max(1, round(image.size[1] * scale))
    if width != image.size[0] or height != image.size[1]:
        image.scale(width, height)
    temporary = destination.with_suffix(".tmp.png")
    image.file_format = "PNG"
    image.filepath_raw = str(temporary)
    image.save()
    os.replace(temporary, destination)
    bpy.data.images.remove(image)


def main():
    args = parse_args()
    source = Path(args.source).expanduser().resolve()
    texture_root = Path(args.texture_root).expanduser().resolve()
    project_root = Path(args.project_root).expanduser().resolve()
    generated = project_root / GENERATED_PATH
    if not source.exists():
        raise FileNotFoundError(source)

    print(f"[WrenOptimize] importing {source.name}", flush=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    objects = mesh_objects()
    if not objects:
        raise RuntimeError("Wren FBX contains no mesh objects")
    if not any(obj.type == "ARMATURE" for obj in bpy.context.scene.objects):
        raise RuntimeError("Wren FBX contains no armature")

    source_triangles = triangle_count(objects)
    journal_triangles = decimate_to(objects, TARGET_TRIANGLES)
    export_character(generated / "Wren_Journal.fbx")

    resize_texture(texture_root / "Image_0.jpg", generated / "Wren_Journal_Albedo.png", 2048)
    resize_texture(texture_root / "Image_2.jpg", generated / "Wren_Journal_Normal.png", 2048)
    resize_texture(texture_root / "Image_3.jpg", generated / "Wren_Journal_Emission.png", 1024)
    print(
        f"[WrenOptimize] {source_triangles:,} -> {journal_triangles:,} triangles; "
        "2K albedo/normal + 1K emission",
        flush=True,
    )


if __name__ == "__main__":
    main()
