import argparse
import json
import sys

import bpy


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", required=True)
    args = sys.argv
    if "--" in args:
        args = args[args.index("--") + 1 :]
    else:
        args = []
    return parser.parse_args(args)


def round6(value):
    return round(float(value), 6)


def vector_to_list(value):
    return [round6(component) for component in value]


def main():
    args = parse_args()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.ops.import_scene.fbx(filepath=args.fbx)

    objects = []
    for obj in bpy.context.scene.objects:
        item = {
            "object": obj.name,
            "type": obj.type,
            "parent": obj.parent.name if obj.parent else "",
            "children": [child.name for child in obj.children],
            "location": vector_to_list(obj.location),
            "rotation_euler": vector_to_list(obj.rotation_euler),
            "scale": vector_to_list(obj.scale),
            "dimensions": vector_to_list(obj.dimensions),
        }
        if obj.type == "MESH":
            mesh = obj.data
            layer = mesh.uv_layers.active
            uvs = [loop.uv.copy() for loop in layer.data] if layer else []
            vertex_u_values = [[] for _ in mesh.vertices]
            if layer:
                for loop_index, loop in enumerate(mesh.loops):
                    vertex_u_values[loop.vertex_index].append(layer.data[loop_index].uv.x)

            vertex_u_average = [
                sum(values) / len(values) if values else 0.5
                for values in vertex_u_values
            ]
            low_u_y = [
                mesh.vertices[index].co.y
                for index, average in enumerate(vertex_u_average)
                if average <= 0.12
            ]
            high_u_y = [
                mesh.vertices[index].co.y
                for index, average in enumerate(vertex_u_average)
                if average >= 0.88
            ]
            low_u_z = [
                mesh.vertices[index].co.z
                for index, average in enumerate(vertex_u_average)
                if average <= 0.12
            ]
            high_u_z = [
                mesh.vertices[index].co.z
                for index, average in enumerate(vertex_u_average)
                if average >= 0.88
            ]
            item.update(
                {
                    "mesh": mesh.name,
                    "vertices": len(mesh.vertices),
                    "polygons": len(mesh.polygons),
                    "uv_layers": [item.name for item in mesh.uv_layers],
                    "uv_min": [round6(min(uv[index] for uv in uvs)) for index in range(2)] if uvs else [],
                    "uv_max": [round6(max(uv[index] for uv in uvs)) for index in range(2)] if uvs else [],
                    "low_u_y_average": round6(sum(low_u_y) / len(low_u_y)) if low_u_y else None,
                    "high_u_y_average": round6(sum(high_u_y) / len(high_u_y)) if high_u_y else None,
                    "low_u_z_average": round6(sum(low_u_z) / len(low_u_z)) if low_u_z else None,
                    "high_u_z_average": round6(sum(high_u_z) / len(high_u_z)) if high_u_z else None,
                }
            )
        objects.append(
            item
        )

    print("FBX_UV_INSPECT_START")
    print(json.dumps(objects, ensure_ascii=False, indent=2))
    print("FBX_UV_INSPECT_END")


if __name__ == "__main__":
    main()
