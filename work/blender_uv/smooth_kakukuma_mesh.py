import argparse
import json
from pathlib import Path

import bpy


MESH_OBJECT_NAME = "Clownfish_CanvasUV"
MESH_NAME = "Clownfish_CanvasUV"
UV_LAYER_NAME = "DrawingCanvasUV"
MATERIAL_NAME = "Kakukuma_DrawingBase"
SUBDIVISION_LEVELS = 2
SWIM_ACTION_NAME = "Kakukuma_SmoothSwimPreview"
SWIM_SHAPE_LEFT = "SmoothSwim_Left"
SWIM_SHAPE_RIGHT = "SmoothSwim_Right"


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-blend", required=True)
    parser.add_argument("--output-fbx", required=True)
    args = bpy.app.driver_namespace.get("_argv")
    if args is None:
        import sys

        args = sys.argv
    if "--" in args:
        args = args[args.index("--") + 1 :]
    else:
        args = []
    return parser.parse_args(args)


def mesh_objects():
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def select_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def ensure_single_mesh():
    meshes = mesh_objects()
    if len(meshes) != 1:
        raise RuntimeError(f"Expected exactly one mesh object, found {len(meshes)}")

    obj = meshes[0]
    obj.name = MESH_OBJECT_NAME
    obj.data.name = MESH_NAME
    select_active(obj)
    return obj


def clear_existing_shape_keys(obj):
    if obj.data.shape_keys is None:
        return

    select_active(obj)
    while obj.data.shape_keys and obj.data.shape_keys.key_blocks:
        obj.active_shape_key_index = len(obj.data.shape_keys.key_blocks) - 1
        bpy.ops.object.shape_key_remove()


def apply_simple_subdivision(obj):
    if len(obj.data.vertices) >= 300:
        return False

    select_active(obj)
    modifier = obj.modifiers.new("Smooth deformation density", "SUBSURF")
    modifier.subdivision_type = "SIMPLE"
    modifier.levels = SUBDIVISION_LEVELS
    modifier.render_levels = SUBDIVISION_LEVELS
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return True


def smooth_normals(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = True

    select_active(obj)
    modifier = obj.modifiers.new("Soft swim normals", "WEIGHTED_NORMAL")
    modifier.keep_sharp = True
    modifier.weight = 50
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.update()


def configure_materials(obj):
    if not obj.material_slots:
        material = bpy.data.materials.new(MATERIAL_NAME)
        obj.data.materials.append(material)

    for slot in obj.material_slots:
        material = slot.material
        if material is None:
            material = bpy.data.materials.new(MATERIAL_NAME)
            slot.material = material

        material.name = MATERIAL_NAME
        material.diffuse_color = (1.0, 1.0, 1.0, 1.0)
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is not None:
            principled.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
            principled.inputs["Roughness"].default_value = 0.74
            principled.inputs["Metallic"].default_value = 0.0


def ensure_uv_layer(obj):
    if UV_LAYER_NAME in obj.data.uv_layers:
        obj.data.uv_layers.active = obj.data.uv_layers[UV_LAYER_NAME]
        return

    if obj.data.uv_layers:
        obj.data.uv_layers[0].name = UV_LAYER_NAME
        obj.data.uv_layers.active = obj.data.uv_layers[UV_LAYER_NAME]
        return

    raise RuntimeError("Mesh has no UV layers to preserve.")


def normalized_axis(value, low, high):
    if abs(high - low) < 1e-6:
        return 0.5
    return max(0.0, min(1.0, (value - low) / (high - low)))


def smoothstep(edge0, edge1, value):
    if abs(edge1 - edge0) < 1e-6:
        return 0.0
    t = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def create_swim_shape_keys(obj):
    clear_existing_shape_keys(obj)

    mesh = obj.data
    basis = obj.shape_key_add(name="Basis")
    left = obj.shape_key_add(name=SWIM_SHAPE_LEFT)
    right = obj.shape_key_add(name=SWIM_SHAPE_RIGHT)

    z_values = [vertex.co.z for vertex in mesh.vertices]
    x_values = [vertex.co.x for vertex in mesh.vertices]
    z_min = min(z_values)
    z_max = max(z_values)
    side_size = max(x_values) - min(x_values)
    amplitude = max(side_size * 0.014, 0.035)

    for index, vertex in enumerate(mesh.vertices):
        t = normalized_axis(vertex.co.z, z_min, z_max)
        centered = abs(t - 0.5) * 2.0
        tail_weight = smoothstep(0.32, 1.0, centered)
        wave = __import__("math").sin((t - 0.22) * __import__("math").tau * 1.08)
        offset = wave * amplitude * tail_weight
        vertical_lift = __import__("math").sin(t * __import__("math").pi) * amplitude * 0.18 * tail_weight

        left.data[index].co = basis.data[index].co.copy()
        left.data[index].co.x += offset
        left.data[index].co.y += vertical_lift

        right.data[index].co = basis.data[index].co.copy()
        right.data[index].co.x -= offset
        right.data[index].co.y -= vertical_lift


def keyframe_shape_key(key, frame, value):
    key.value = value
    key.keyframe_insert("value", frame=frame)


def create_smooth_swim_preview(obj):
    create_swim_shape_keys(obj)
    shape_keys = obj.data.shape_keys
    shape_keys.name = SWIM_ACTION_NAME
    left = shape_keys.key_blocks[SWIM_SHAPE_LEFT]
    right = shape_keys.key_blocks[SWIM_SHAPE_RIGHT]

    for frame, left_value, right_value in (
        (1, 0.0, 0.0),
        (16, 0.62, 0.0),
        (31, 0.0, 0.0),
        (46, 0.0, 0.62),
        (61, 0.0, 0.0),
    ):
        keyframe_shape_key(left, frame, left_value)
        keyframe_shape_key(right, frame, right_value)

    if shape_keys.animation_data and shape_keys.animation_data.action:
        shape_keys.animation_data.action.name = SWIM_ACTION_NAME
        for fcurve in shape_keys.animation_data.action.fcurves:
            fcurve.modifiers.new(type="CYCLES")
            for point in fcurve.keyframe_points:
                point.interpolation = "SINE"

    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 60


def export_fbx(obj, output_fbx):
    select_active(obj)
    bpy.ops.export_scene.fbx(
        filepath=str(output_fbx),
        use_selection=True,
        object_types={"MESH"},
        bake_anim=False,
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
    )


def main():
    args = parse_args()
    obj = ensure_single_mesh()
    before_vertices = len(obj.data.vertices)
    before_polygons = len(obj.data.polygons)

    ensure_uv_layer(obj)
    subdivided = apply_simple_subdivision(obj)
    ensure_uv_layer(obj)
    smooth_normals(obj)
    configure_materials(obj)
    create_smooth_swim_preview(obj)

    output_blend = Path(args.output_blend)
    output_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))

    output_fbx = Path(args.output_fbx)
    output_fbx.parent.mkdir(parents=True, exist_ok=True)
    export_fbx(obj, output_fbx)

    print("KAKUKUMA_SMOOTH_RESULT_START")
    print(
        json.dumps(
            {
                "object": obj.name,
                "mesh": obj.data.name,
                "subdivided": subdivided,
                "vertices_before": before_vertices,
                "vertices_after": len(obj.data.vertices),
                "polygons_before": before_polygons,
                "polygons_after": len(obj.data.polygons),
                "uv_layers": [layer.name for layer in obj.data.uv_layers],
                "shape_keys": [key.name for key in obj.data.shape_keys.key_blocks],
                "output_blend": str(output_blend),
                "output_fbx": str(output_fbx),
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    print("KAKUKUMA_SMOOTH_RESULT_END")


if __name__ == "__main__":
    main()
