import bpy
import json


def list_names(items):
    return [item.name for item in items]


objects = []
for obj in bpy.context.scene.objects:
    objects.append(
        {
            "name": obj.name,
            "type": obj.type,
            "parent": obj.parent.name if obj.parent else "",
            "children": list_names(obj.children),
            "modifiers": [
                {
                    "name": modifier.name,
                    "type": modifier.type,
                    "object": getattr(modifier, "object", None).name
                    if getattr(modifier, "object", None)
                    else "",
                }
                for modifier in obj.modifiers
            ],
        }
    )

print("BLEND_HIERARCHY_JSON_START")
print(json.dumps(objects, ensure_ascii=False, indent=2))
print("BLEND_HIERARCHY_JSON_END")
