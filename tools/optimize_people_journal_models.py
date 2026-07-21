"""Build static, menu-safe journal models for the People of Hollowfen.

The source manifest is owned by the character roster pipeline. Each entry must
provide at least these optimization fields::

    {
        "id": "edda",
        "displayName": "Edda",
        "modelSource": "public/models/.../Edda_TPose.fbx",
        "albedoSource": "public/models/.../Image_0.jpg",
        "normalSource": "public/models/.../Image_2.jpg",
        "targetTriangles": 60000,
        "textureSize": 2048,
        "previewScale": 1.0
    }

``previewScale`` is a uniform multiplier applied to the source character's
meter-scale height. The result is centered on the horizontal plane and its
feet are grounded at Unity Y=0.

Run from the repository root (all arguments are optional)::

    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python tools/optimize_people_journal_models.py -- \
      --manifest tools/people_of_hollowfen_manifest.json \
      --project-root Hollowfen-Unity

Use ``--only edda --only hollin`` for a partial rebuild. Outputs are replaced
atomically and may be regenerated safely. Gameplay/source assets are never
modified.
"""

import argparse
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

import bpy
from mathutils import Matrix, Vector


DEFAULT_MANIFEST_PATH = Path("tools/people_of_hollowfen_manifest.json")
DEFAULT_PROJECT_PATH = Path("Hollowfen-Unity")
GENERATED_PATH = Path("Assets/_Hollowfen/Models/Characters/People/Generated")

REQUIRED_FIELDS = {
    "id",
    "displayName",
    "modelSource",
    "albedoSource",
    "normalSource",
    "targetTriangles",
    "textureSize",
    "previewScale",
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(
        description="Generate static People of Hollowfen journal models."
    )
    parser.add_argument(
        "--repo-root",
        help="Repository root. Defaults to the parent of this script's tools folder.",
    )
    parser.add_argument(
        "--manifest",
        default=str(DEFAULT_MANIFEST_PATH),
        help="Manifest path, relative to the repository root unless absolute.",
    )
    parser.add_argument(
        "--project-root",
        default=str(DEFAULT_PROJECT_PATH),
        help="Unity project path, relative to the repository root unless absolute.",
    )
    parser.add_argument(
        "--only",
        action="append",
        default=[],
        metavar="ID",
        help="Build only this id. May be supplied more than once or comma-separated.",
    )
    return parser.parse_args(argv)


def absolute_from_repo(value, repo_root):
    path = Path(value).expanduser()
    return path.resolve() if path.is_absolute() else (repo_root / path).resolve()


def repo_source(value, field_name, character_id, repo_root):
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{character_id}.{field_name} must be a non-empty string")

    relative = Path(value)
    if relative.is_absolute():
        raise ValueError(
            f"{character_id}.{field_name} must be repository-relative: {value!r}"
        )

    source = (repo_root / relative).resolve()
    try:
        source.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(
            f"{character_id}.{field_name} escapes the repository: {value!r}"
        ) from error

    if not source.is_file():
        raise FileNotFoundError(
            f"{character_id}.{field_name} does not exist: {source}"
        )
    return source


def positive_number(value, field_name, character_id):
    if (
        isinstance(value, bool)
        or not isinstance(value, (int, float))
        or not math.isfinite(value)
        or value <= 0
    ):
        raise ValueError(f"{character_id}.{field_name} must be a positive number")
    return float(value)


def positive_integer(value, field_name, character_id):
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError(f"{character_id}.{field_name} must be a positive integer")
    return value


def validate_character_id(value, index):
    if not isinstance(value, str) or not value:
        raise ValueError(f"Manifest entry {index} has an empty or invalid id")
    if value in {".", ".."} or Path(value).name != value or "/" in value or "\\" in value:
        raise ValueError(f"Manifest entry {index} has an unsafe id: {value!r}")
    if any(character not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-" for character in value):
        raise ValueError(
            f"Manifest entry {index} id may contain only letters, numbers, _ and -: {value!r}"
        )
    return value


def load_manifest(manifest_path, repo_root):
    if not manifest_path.is_file():
        raise FileNotFoundError(f"People manifest not found: {manifest_path}")

    with manifest_path.open("r", encoding="utf-8") as manifest_file:
        manifest = json.load(manifest_file)

    if not isinstance(manifest, list) or not manifest:
        raise ValueError("People manifest must be a non-empty top-level JSON array")

    people = []
    seen_ids = set()
    for index, raw_entry in enumerate(manifest):
        if not isinstance(raw_entry, dict):
            raise ValueError(f"Manifest entry {index} must be a JSON object")

        missing = sorted(REQUIRED_FIELDS - raw_entry.keys())
        if missing:
            raise ValueError(
                f"Manifest entry {index} is missing required fields: {', '.join(missing)}"
            )

        character_id = validate_character_id(raw_entry["id"], index)
        folded_id = character_id.casefold()
        if folded_id in seen_ids:
            raise ValueError(f"Duplicate manifest id: {character_id!r}")
        seen_ids.add(folded_id)

        if not isinstance(raw_entry["displayName"], str) or not raw_entry["displayName"].strip():
            raise ValueError(f"{character_id}.displayName must be a non-empty string")

        entry = dict(raw_entry)
        entry["modelPath"] = repo_source(
            raw_entry["modelSource"], "modelSource", character_id, repo_root
        )
        if entry["modelPath"].suffix.casefold() != ".fbx":
            raise ValueError(
                f"{character_id}.modelSource must point to an FBX file: "
                f"{raw_entry['modelSource']!r}"
            )
        entry["albedoPath"] = repo_source(
            raw_entry["albedoSource"], "albedoSource", character_id, repo_root
        )
        entry["normalPath"] = repo_source(
            raw_entry["normalSource"], "normalSource", character_id, repo_root
        )
        entry["targetTriangles"] = positive_integer(
            raw_entry["targetTriangles"], "targetTriangles", character_id
        )
        entry["textureSize"] = positive_integer(
            raw_entry["textureSize"], "textureSize", character_id
        )
        entry["previewScale"] = positive_number(
            raw_entry["previewScale"], "previewScale", character_id
        )
        people.append(entry)

    return people


def selected_ids(raw_values):
    selected = set()
    for raw_value in raw_values:
        for character_id in raw_value.split(","):
            character_id = character_id.strip()
            if character_id:
                selected.add(character_id.casefold())
    return selected


def display_path(path, base):
    try:
        return path.relative_to(base)
    except ValueError:
        return path


def mesh_triangle_count(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def static_mesh_from_scene(character_id):
    """Bake evaluated meshes and their world transforms into one static mesh."""

    source_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not source_objects:
        raise RuntimeError(f"{character_id}: source FBX contains no mesh objects")

    depsgraph = bpy.context.evaluated_depsgraph_get()
    depsgraph.update()
    baked_objects = []
    for index, source_object in enumerate(source_objects):
        evaluated_object = source_object.evaluated_get(depsgraph)
        baked_mesh = bpy.data.meshes.new_from_object(
            evaluated_object,
            preserve_all_data_layers=True,
            depsgraph=depsgraph,
        )
        if baked_mesh is None:
            raise RuntimeError(
                f"{character_id}: could not bake mesh {source_object.name!r}"
            )

        baked_object = bpy.data.objects.new(
            f"Character_{character_id}_Journal_Part_{index:02d}", baked_mesh
        )
        bpy.context.scene.collection.objects.link(baked_object)

        world_matrix = source_object.matrix_world.copy()
        baked_mesh.transform(world_matrix)
        if world_matrix.to_3x3().determinant() < 0:
            baked_mesh.flip_normals()
        baked_mesh.update()
        baked_object.matrix_world = Matrix.Identity(4)
        baked_objects.append(baked_object)

    # The evaluated copies no longer depend on armatures, constraints, shape
    # keys, animation, or the imported hierarchy. Remove every imported object.
    baked_set = set(baked_objects)
    for scene_object in list(bpy.context.scene.objects):
        if scene_object not in baked_set:
            bpy.data.objects.remove(scene_object, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    for baked_object in baked_objects:
        baked_object.select_set(True)
    bpy.context.view_layer.objects.active = baked_objects[0]
    if len(baked_objects) > 1:
        bpy.ops.object.join()

    journal_object = bpy.context.view_layer.objects.active
    journal_object.name = f"Character_{character_id}_Journal"
    journal_object.data.name = f"Character_{character_id}_Journal_Mesh"
    journal_object.matrix_world = Matrix.Identity(4)
    journal_object.animation_data_clear()
    journal_object.data.animation_data_clear()
    return journal_object


def decimate_to(journal_object, target_triangles):
    current = mesh_triangle_count(journal_object.data)
    if current <= target_triangles:
        return current

    # A second pass is normally unnecessary, but handles meshes whose collapse
    # constraints leave the first proportional pass slightly above the ceiling.
    for pass_index in range(3):
        current = mesh_triangle_count(journal_object.data)
        if current <= target_triangles:
            break

        ratio = max(0.001, min(1.0, target_triangles / current))
        modifier = journal_object.modifiers.new(
            name=f"Hollowfen_Journal_Budget_{pass_index + 1}", type="DECIMATE"
        )
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True

        bpy.ops.object.select_all(action="DESELECT")
        journal_object.select_set(True)
        bpy.context.view_layer.objects.active = journal_object
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    current = mesh_triangle_count(journal_object.data)
    if current > target_triangles:
        raise RuntimeError(
            f"Decimation stopped at {current:,} triangles, above the "
            f"{target_triangles:,}-triangle target"
        )
    return current


def mesh_bounds(mesh):
    if not mesh.vertices:
        raise RuntimeError("Baked journal mesh has no vertices")

    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for vertex in mesh.vertices:
        coordinate = vertex.co
        minimum.x = min(minimum.x, coordinate.x)
        minimum.y = min(minimum.y, coordinate.y)
        minimum.z = min(minimum.z, coordinate.z)
        maximum.x = max(maximum.x, coordinate.x)
        maximum.y = max(maximum.y, coordinate.y)
        maximum.z = max(maximum.z, coordinate.z)
    return minimum, maximum


def scale_center_and_ground(journal_object, preview_scale):
    """Apply relative height scale and put Blender Z / Unity Y ground at zero."""

    mesh = journal_object.data
    source_minimum, source_maximum = mesh_bounds(mesh)
    source_height = source_maximum.z - source_minimum.z
    if source_height <= 0:
        raise RuntimeError("Baked journal mesh has zero height")

    mesh.transform(Matrix.Scale(preview_scale, 4))
    scaled_minimum, scaled_maximum = mesh_bounds(mesh)
    horizontal_center_x = (scaled_minimum.x + scaled_maximum.x) * 0.5
    horizontal_center_y = (scaled_minimum.y + scaled_maximum.y) * 0.5
    mesh.transform(
        Matrix.Translation(
            Vector(
                (
                    -horizontal_center_x,
                    -horizontal_center_y,
                    -scaled_minimum.z,
                )
            )
        )
    )
    mesh.update()

    final_minimum, final_maximum = mesh_bounds(mesh)
    final_height = final_maximum.z - final_minimum.z
    if abs(final_minimum.z) > 1e-5:
        raise RuntimeError(
            f"Grounding failed; Blender Z / Unity Y minimum is {final_minimum.z}"
        )
    return source_height, final_height


def validate_mesh(journal_object, character_id):
    mesh = journal_object.data
    if not mesh.uv_layers:
        raise RuntimeError(f"{character_id}: optimized mesh has no UV map")
    if not mesh.loops or not mesh.polygons:
        raise RuntimeError(f"{character_id}: optimized mesh has no renderable faces")
    if journal_object.parent is not None or journal_object.modifiers:
        raise RuntimeError(f"{character_id}: optimized mesh is not hierarchy-free")
    if any(obj.type == "ARMATURE" for obj in bpy.context.scene.objects):
        raise RuntimeError(f"{character_id}: armature remained in static export scene")


def temporary_path(destination):
    return destination.with_name(f"{destination.stem}.tmp{destination.suffix}")


def export_static_fbx(journal_object, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = temporary_path(destination)
    if temporary.exists():
        temporary.unlink()

    bpy.ops.object.select_all(action="DESELECT")
    journal_object.select_set(True)
    bpy.context.view_layer.objects.active = journal_object
    bpy.ops.export_scene.fbx(
        filepath=str(temporary),
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="OFF",
        use_custom_props=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    if not temporary.is_file() or temporary.stat().st_size == 0:
        raise RuntimeError(f"Blender did not create FBX output: {temporary}")
    return temporary


_PILLOW_RESIZER = r"""
import json
import sys
from PIL import Image

source, destination, maximum = sys.argv[1], sys.argv[2], int(sys.argv[3])
with Image.open(source) as image:
    image.load()
    source_size = image.size
    if max(image.size) > maximum:
        image.thumbnail((maximum, maximum), Image.Resampling.LANCZOS)
    output_size = image.size
    image.save(destination, format="PNG", compress_level=6)
print(json.dumps({"source": source_size, "output": output_size}))
"""


def find_pillow_python():
    candidates = []
    discovered = shutil.which("python3")
    if discovered:
        candidates.append(discovered)
    candidates.extend(["/opt/homebrew/bin/python3", "/usr/local/bin/python3"])
    for candidate in dict.fromkeys(candidates):
        if not Path(candidate).is_file():
            continue
        probe = subprocess.run(
            [candidate, "-c", "import PIL"],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            check=False,
        )
        if probe.returncode == 0:
            return candidate
    raise RuntimeError(
        "Texture optimization requires a system python3 with Pillow installed"
    )


def resize_texture(source, temporary, maximum_size, is_normal_map=False):
    if temporary.exists():
        temporary.unlink()
    temporary.parent.mkdir(parents=True, exist_ok=True)

    # Blender 5.1 can invalidate otherwise valid PNG buffers after importing an
    # FBX with unresolved embedded texture paths. Pillow keeps this mechanical
    # transcode independent of the scene and preserves normal-map channel data.
    python = find_pillow_python()
    process = subprocess.run(
        [python, "-c", _PILLOW_RESIZER, str(source), str(temporary), str(maximum_size)],
        capture_output=True,
        text=True,
        check=False,
    )
    if process.returncode != 0:
        raise RuntimeError(
            f"Pillow could not transcode {source}: {process.stderr.strip()}"
        )
    if not temporary.is_file() or temporary.stat().st_size == 0:
        raise RuntimeError(f"Pillow did not create PNG output: {temporary}")
    try:
        dimensions = json.loads(process.stdout.strip().splitlines()[-1])
        return tuple(dimensions["source"]), tuple(dimensions["output"])
    except (IndexError, KeyError, TypeError, json.JSONDecodeError) as error:
        raise RuntimeError(
            f"Could not read Pillow dimensions for {source}: {process.stdout!r}"
        ) from error


def commit_outputs(temporary_outputs):
    for temporary, destination in temporary_outputs:
        os.replace(temporary, destination)


def clean_temporaries(temporary_outputs):
    for temporary, _destination in temporary_outputs:
        if temporary.exists():
            temporary.unlink()


def build_character(entry, project_root):
    character_id = entry["id"]
    output_root = project_root / GENERATED_PATH / character_id
    output_root.mkdir(parents=True, exist_ok=True)

    fbx_destination = output_root / f"Character_{character_id}_Journal.fbx"
    albedo_destination = output_root / f"Character_{character_id}_Journal_Albedo.png"
    normal_destination = output_root / f"Character_{character_id}_Journal_Normal.png"
    temporary_outputs = [
        (temporary_path(fbx_destination), fbx_destination),
        (temporary_path(albedo_destination), albedo_destination),
        (temporary_path(normal_destination), normal_destination),
    ]
    clean_temporaries(temporary_outputs)

    started = time.perf_counter()
    print(
        f"[PeopleJournal] {character_id}: importing {entry['modelPath'].name}",
        flush=True,
    )

    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.unit_settings.system = "METRIC"
        bpy.context.scene.unit_settings.scale_length = 1.0
        bpy.ops.import_scene.fbx(filepath=str(entry["modelPath"]), use_anim=False)

        journal_object = static_mesh_from_scene(character_id)
        source_triangles = mesh_triangle_count(journal_object.data)
        if source_triangles <= 0:
            raise RuntimeError(f"{character_id}: source mesh has no triangles")

        output_triangles = decimate_to(journal_object, entry["targetTriangles"])
        source_height, output_height = scale_center_and_ground(
            journal_object, entry["previewScale"]
        )
        validate_mesh(journal_object, character_id)

        export_static_fbx(journal_object, fbx_destination)
        albedo_source_size, albedo_output_size = resize_texture(
            entry["albedoPath"],
            temporary_path(albedo_destination),
            entry["textureSize"],
        )
        normal_source_size, normal_output_size = resize_texture(
            entry["normalPath"],
            temporary_path(normal_destination),
            entry["textureSize"],
            is_normal_map=True,
        )

        commit_outputs(temporary_outputs)
    except Exception:
        clean_temporaries(temporary_outputs)
        raise

    elapsed = time.perf_counter() - started
    print(
        f"[PeopleJournal] {character_id}: {source_triangles:,} -> "
        f"{output_triangles:,} triangles (target {entry['targetTriangles']:,}); "
        f"height {source_height:.3f}m x {entry['previewScale']:.3f} = "
        f"{output_height:.3f}m; FBX {fbx_destination.stat().st_size / (1024 * 1024):.2f} MiB",
        flush=True,
    )
    print(
        f"[PeopleJournal] {character_id}: albedo "
        f"{albedo_source_size[0]}x{albedo_source_size[1]} -> "
        f"{albedo_output_size[0]}x{albedo_output_size[1]}; normal "
        f"{normal_source_size[0]}x{normal_source_size[1]} -> "
        f"{normal_output_size[0]}x{normal_output_size[1]}; {elapsed:.1f}s",
        flush=True,
    )

    return {
        "sourceTriangles": source_triangles,
        "outputTriangles": output_triangles,
        "elapsed": elapsed,
    }


def main():
    args = parse_args()
    inferred_repo_root = Path(__file__).resolve().parents[1]
    repo_root = (
        Path(args.repo_root).expanduser().resolve()
        if args.repo_root
        else inferred_repo_root
    )
    if not repo_root.is_dir():
        raise FileNotFoundError(f"Repository root is invalid: {repo_root}")
    manifest_path = absolute_from_repo(args.manifest, repo_root)
    project_root = absolute_from_repo(args.project_root, repo_root)

    if not project_root.is_dir() or not (project_root / "Assets").is_dir():
        raise FileNotFoundError(f"Unity project root is invalid: {project_root}")

    people = load_manifest(manifest_path, repo_root)
    requested = selected_ids(args.only)
    available = {entry["id"].casefold(): entry["id"] for entry in people}
    unknown = sorted(requested - available.keys())
    if unknown:
        raise ValueError(f"Unknown --only id(s): {', '.join(unknown)}")
    if requested:
        people = [entry for entry in people if entry["id"].casefold() in requested]

    print(
        f"[PeopleJournal] building {len(people)} character(s) from "
        f"{display_path(manifest_path, repo_root)}",
        flush=True,
    )
    started = time.perf_counter()
    results = [build_character(entry, project_root) for entry in people]
    elapsed = time.perf_counter() - started
    print(
        f"[PeopleJournal] complete: {len(results)} character(s), "
        f"{sum(result['sourceTriangles'] for result in results):,} -> "
        f"{sum(result['outputTriangles'] for result in results):,} triangles, "
        f"{elapsed:.1f}s",
        flush=True,
    )


if __name__ == "__main__":
    main()
