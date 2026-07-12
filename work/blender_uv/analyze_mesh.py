import bpy
import bmesh
import json
from mathutils import Vector


def round6(value):
    return round(float(value), 6)


def mesh_objects():
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def world_vertices(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        return [obj.matrix_world @ vertex.co for vertex in mesh.vertices], mesh
    finally:
        evaluated.to_mesh_clear()


def summarize_axis(vertices, axis_index, sample_count=12):
    min_value = min(vertex[axis_index] for vertex in vertices)
    max_value = max(vertex[axis_index] for vertex in vertices)
    width_axes = [index for index in range(3) if index != axis_index]
    rows = []
    for sample_index in range(sample_count):
        t0 = sample_index / sample_count
        t1 = (sample_index + 1) / sample_count
        low = min_value + (max_value - min_value) * t0
        high = min_value + (max_value - min_value) * t1
        band = [vertex for vertex in vertices if low <= vertex[axis_index] <= high]
        if not band:
            rows.append({"t": round6((t0 + t1) * 0.5), "count": 0})
            continue

        rows.append(
            {
                "t": round6((t0 + t1) * 0.5),
                "count": len(band),
                "center": [round6(sum(vertex[index] for vertex in band) / len(band)) for index in width_axes],
                "span": [
                    round6(max(vertex[index] for vertex in band) - min(vertex[index] for vertex in band))
                    for index in width_axes
                ],
            }
        )
    return rows


def uv_summary(obj):
    mesh = obj.data
    if not mesh.uv_layers:
        return []

    summaries = []
    for layer in mesh.uv_layers:
        uvs = [loop.uv.copy() for loop in layer.data]
        summaries.append(
            {
                "name": layer.name,
                "count": len(uvs),
                "min": [round6(min(uv[index] for uv in uvs)) for index in range(2)],
                "max": [round6(max(uv[index] for uv in uvs)) for index in range(2)],
                "sample": [[round6(uv.x), round6(uv.y)] for uv in uvs[:24]],
            }
        )
    return summaries


results = []
for obj in mesh_objects():
    vertices, _ = world_vertices(obj)
    bounds_min = [round6(min(vertex[index] for vertex in vertices)) for index in range(3)]
    bounds_max = [round6(max(vertex[index] for vertex in vertices)) for index in range(3)]
    size = [round6(bounds_max[index] - bounds_min[index]) for index in range(3)]
    results.append(
        {
            "name": obj.name,
            "bounds_min": bounds_min,
            "bounds_max": bounds_max,
            "size": size,
            "bands_by_x": summarize_axis(vertices, 0),
            "bands_by_y": summarize_axis(vertices, 1),
            "bands_by_z": summarize_axis(vertices, 2),
            "uv": uv_summary(obj),
        }
    )

print("MESH_ANALYSIS_JSON_START")
print(json.dumps(results, ensure_ascii=False, indent=2))
print("MESH_ANALYSIS_JSON_END")
