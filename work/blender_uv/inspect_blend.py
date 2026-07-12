import bpy
import json


def vector_to_list(value):
    return [round(float(component), 6) for component in value]


objects = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue

    mesh = obj.data
    objects.append(
        {
            "name": obj.name,
            "mesh": mesh.name,
            "vertices": len(mesh.vertices),
            "polygons": len(mesh.polygons),
            "uv_layers": [layer.name for layer in mesh.uv_layers],
            "dimensions": vector_to_list(obj.dimensions),
            "location": vector_to_list(obj.location),
            "rotation_euler": vector_to_list(obj.rotation_euler),
            "scale": vector_to_list(obj.scale),
            "materials": [slot.material.name if slot.material else "" for slot in obj.material_slots],
        }
    )

print("BLEND_INSPECT_JSON_START")
print(json.dumps(objects, ensure_ascii=False, indent=2))
print("BLEND_INSPECT_JSON_END")
