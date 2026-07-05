using UnityEngine;

public static class GeneratedPrimitiveFactory
{
    public static GameObject Create(PrimitiveType type, string name, Material material = null)
    {
        GameObject gameObject = new GameObject(name);
        MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = CreateMesh(type);
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        return gameObject;
    }

    private static Mesh CreateMesh(PrimitiveType type)
    {
        Mesh source = Resources.GetBuiltinResource<Mesh>(BuiltinMeshName(type));
        if (source != null)
        {
            Mesh mesh = Object.Instantiate(source);
            mesh.name = $"Generated {type} Mesh";
            return mesh;
        }

        return CreateFallbackCubeMesh(type);
    }

    private static string BuiltinMeshName(PrimitiveType type)
    {
        return type switch
        {
            PrimitiveType.Sphere => "Sphere.fbx",
            PrimitiveType.Capsule => "Capsule.fbx",
            PrimitiveType.Cylinder => "Cylinder.fbx",
            PrimitiveType.Plane => "Plane.fbx",
            PrimitiveType.Quad => "Quad.fbx",
            _ => "Cube.fbx"
        };
    }

    private static Mesh CreateFallbackCubeMesh(PrimitiveType type)
    {
        Mesh mesh = new Mesh { name = $"Generated {type} Fallback Mesh" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 4, 5, 0, 5, 1,
            1, 5, 6, 1, 6, 2,
            2, 6, 7, 2, 7, 3,
            3, 7, 4, 3, 4, 0,
            4, 7, 6, 4, 6, 5,
            3, 0, 1, 3, 1, 2
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
