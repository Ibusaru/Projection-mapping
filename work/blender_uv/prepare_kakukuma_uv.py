import argparse
import json
from pathlib import Path

import bpy


CANVAS_WIDTH = 1024
CANVAS_HEIGHT = 512
CANVAS_BOX = {
    "x_min": 50.0,
    "x_max": 974.0,
    "y_min": 34.0,
    "y_max": 478.0,
}
UV_MARKER = "_CanvasUV"
MESH_NAME = "Clownfish_CanvasUV"
UV_LAYER_NAME = "DrawingCanvasUV"


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


def bounds_for_axis(vertices, axis_index):
    values = [vertex.co[axis_index] for vertex in vertices]
    return min(values), max(values)


def normalize(value, low, high):
    if abs(high - low) < 1e-8:
        return 0.5
    return max(0.0, min(1.0, (value - low) / (high - low)))


def canvas_to_uv(canvas_x, canvas_y):
    return canvas_x / CANVAS_WIDTH, 1.0 - canvas_y / CANVAS_HEIGHT


def vertex_to_canvas(vertex, y_min, y_max, z_min, z_max):
    u = normalize(vertex.co.y, y_min, y_max)
    v = normalize(vertex.co.z, z_min, z_max)
    canvas_x = CANVAS_BOX["x_min"] + (CANVAS_BOX["x_max"] - CANVAS_BOX["x_min"]) * u
    canvas_y = CANVAS_BOX["y_max"] - (CANVAS_BOX["y_max"] - CANVAS_BOX["y_min"]) * v
    return canvas_x, canvas_y


def assign_canvas_uv(obj):
    mesh = obj.data
    y_min, y_max = bounds_for_axis(mesh.vertices, 1)
    z_min, z_max = bounds_for_axis(mesh.vertices, 2)

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])

    uv_layer = mesh.uv_layers.new(name=UV_LAYER_NAME)
    mesh.uv_layers.active = uv_layer
    mesh.name = MESH_NAME

    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index]
            canvas_x, canvas_y = vertex_to_canvas(vertex, y_min, y_max, z_min, z_max)
            uv_layer.data[loop_index].uv = canvas_to_uv(canvas_x, canvas_y)

    return y_min, y_max, z_min, z_max


def unique_sorted(values, epsilon=0.0001):
    values = sorted(values)
    result = []
    for value in values:
        if not result or abs(result[-1] - value) > epsilon:
            result.append(value)
    return result


def triangle_y_intersections(points, x):
    intersections = []
    for index in range(3):
        x0, y0 = points[index]
        x1, y1 = points[(index + 1) % 3]
        if abs(x1 - x0) < 1e-8:
            if abs(x - x0) < 1e-6:
                intersections.extend([y0, y1])
            continue

        low = min(x0, x1)
        high = max(x0, x1)
        if x < low - 1e-6 or x > high + 1e-6:
            continue

        t = (x - x0) / (x1 - x0)
        intersections.append(y0 + (y1 - y0) * t)

    return unique_sorted(intersections)


def projected_profile_points(obj, y_min, y_max, z_min, z_max, sample_count=52):
    mesh = obj.data
    projected = [vertex_to_canvas(vertex, y_min, y_max, z_min, z_max) for vertex in mesh.vertices]
    top = []
    bottom = []

    for sample_index in range(sample_count + 1):
        x = CANVAS_BOX["x_min"] + (CANVAS_BOX["x_max"] - CANVAS_BOX["x_min"]) * sample_index / sample_count
        y_hits = []
        for polygon in mesh.polygons:
            if len(polygon.vertices) != 3:
                continue
            triangle = [projected[vertex_index] for vertex_index in polygon.vertices]
            hits = triangle_y_intersections(triangle, x)
            if len(hits) >= 2:
                y_hits.append(min(hits))
                y_hits.append(max(hits))

        if not y_hits:
            continue

        top.append([round(x, 1), round(min(y_hits), 1)])
        bottom.append([round(x, 1), round(max(y_hits), 1)])

    return top, bottom


def select_export_objects():
    for obj in bpy.context.scene.objects:
        should_export = obj.type in {"MESH", "ARMATURE"}
        obj.select_set(should_export)
        if should_export:
            bpy.context.view_layer.objects.active = obj


def configure_materials(obj):
    for slot in obj.material_slots:
        material = slot.material
        if material is None:
            continue
        material.name = "Kakukuma_DrawingBase"
        material.diffuse_color = (1.0, 1.0, 1.0, 1.0)


def main():
    args = parse_args()
    meshes = mesh_objects()
    if len(meshes) != 1:
        raise RuntimeError(f"Expected exactly one mesh object, found {len(meshes)}")

    mesh = meshes[0]
    y_min, y_max, z_min, z_max = assign_canvas_uv(mesh)
    configure_materials(mesh)
    top, bottom = projected_profile_points(mesh, y_min, y_max, z_min, z_max)

    output_blend = Path(args.output_blend)
    output_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))

    output_fbx = Path(args.output_fbx)
    output_fbx.parent.mkdir(parents=True, exist_ok=True)
    select_export_objects()
    bpy.ops.export_scene.fbx(
        filepath=str(output_fbx),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
    )

    print("KAKUKUMA_UV_RESULT_START")
    print(
        json.dumps(
            {
                "mesh": mesh.data.name,
                "object": mesh.name,
                "uv_layer": UV_LAYER_NAME,
                "uv_marker": UV_MARKER,
                "canvas_box": CANVAS_BOX,
                "top": top,
                "bottom": bottom,
                "output_blend": str(output_blend),
                "output_fbx": str(output_fbx),
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    print("KAKUKUMA_UV_RESULT_END")


if __name__ == "__main__":
    main()
