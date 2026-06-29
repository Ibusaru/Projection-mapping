using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DrawingFishVisual : MonoBehaviour
{
    private const float CanvasWidth = 1024f;
    private const float CanvasHeight = 512f;
    private const int CurveSegments = 48;
    private const int EllipseSegments = 128;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Material material;

    public Renderer Renderer => meshRenderer;

    public void Apply(Texture2D texture, Bounds worldBounds)
    {
        if (texture == null)
        {
            return;
        }

        EnsureComponents();
        EnsureMaterial(texture);
        meshFilter.sharedMesh = CreateFishMesh(WorldBoundsToLocal(transform.parent != null ? transform.parent : transform, worldBounds));
        meshRenderer.sharedMaterial = material;
        meshRenderer.enabled = true;
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }

    private void EnsureMaterial(Texture2D texture)
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            material = new Material(shader)
            {
                name = "Released Fish Drawing 1to1",
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetOverrideTag("RenderType", "Transparent");
        }

        material.mainTexture = texture;
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static Mesh CreateFishMesh(Bounds localBounds)
    {
        MeshBuilder builder = new MeshBuilder(localBounds);
        builder.AddEllipse(new Vector2(470f, 256f), 315f, 150f, EllipseSegments);

        List<Vector2> tail = new List<Vector2> { new Vector2(760f, 256f), new Vector2(975f, 98f) };
        AddQuadratic(tail, new Vector2(975f, 98f), new Vector2(908f, 256f), new Vector2(975f, 414f), CurveSegments);
        builder.AddFan(tail);

        List<Vector2> topFin = new List<Vector2> { new Vector2(380f, 130f) };
        AddQuadratic(topFin, new Vector2(380f, 130f), new Vector2(480f, 24f), new Vector2(580f, 138f), CurveSegments);
        topFin.Add(new Vector2(500f, 165f));
        builder.AddFan(topFin);

        List<Vector2> bottomFin = new List<Vector2> { new Vector2(390f, 384f) };
        AddQuadratic(bottomFin, new Vector2(390f, 384f), new Vector2(500f, 486f), new Vector2(610f, 374f), CurveSegments);
        bottomFin.Add(new Vector2(520f, 350f));
        builder.AddFan(bottomFin);

        return builder.ToMesh();
    }

    private static void AddQuadratic(List<Vector2> points, Vector2 a, Vector2 b, Vector2 c, int segments)
    {
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float inv = 1f - t;
            points.Add(inv * inv * a + 2f * inv * t * b + t * t * c);
        }
    }

    private static Bounds WorldBoundsToLocal(Transform owner, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Bounds localBounds = new Bounds(owner.InverseTransformPoint(min), Vector3.zero);

        EncapsulateLocal(ref localBounds, owner, new Vector3(min.x, min.y, max.z));
        EncapsulateLocal(ref localBounds, owner, new Vector3(min.x, max.y, min.z));
        EncapsulateLocal(ref localBounds, owner, new Vector3(min.x, max.y, max.z));
        EncapsulateLocal(ref localBounds, owner, new Vector3(max.x, min.y, min.z));
        EncapsulateLocal(ref localBounds, owner, new Vector3(max.x, min.y, max.z));
        EncapsulateLocal(ref localBounds, owner, new Vector3(max.x, max.y, min.z));
        EncapsulateLocal(ref localBounds, owner, max);

        return localBounds;
    }

    private static void EncapsulateLocal(ref Bounds bounds, Transform owner, Vector3 worldPoint)
    {
        bounds.Encapsulate(owner.InverseTransformPoint(worldPoint));
    }

    private sealed class MeshBuilder
    {
        private readonly Bounds bounds;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public MeshBuilder(Bounds localBounds)
        {
            bounds = localBounds.size.sqrMagnitude > 0.001f
                ? localBounds
                : new Bounds(Vector3.zero, new Vector3(1.8f, 0.9f, 0.1f));
        }

        public void AddEllipse(Vector2 center, float radiusX, float radiusY, int segments)
        {
            int centerIndex = AddVertex(center);
            int first = -1;
            int previous = -1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector2 point = new Vector2(
                    center.x + Mathf.Cos(angle) * radiusX,
                    center.y + Mathf.Sin(angle) * radiusY
                );
                int index = AddVertex(point);
                if (i == 0)
                {
                    first = index;
                }
                else
                {
                    triangles.Add(centerIndex);
                    triangles.Add(previous);
                    triangles.Add(index);
                }

                previous = index;
            }

            if (first >= 0 && previous != first)
            {
                triangles.Add(centerIndex);
                triangles.Add(previous);
                triangles.Add(first);
            }
        }

        public void AddFan(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3)
            {
                return;
            }

            Vector2 center = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
            {
                center += points[i];
            }

            center /= points.Count;
            int centerIndex = AddVertex(center);
            int first = AddVertex(points[0]);
            int previous = first;
            for (int i = 1; i < points.Count; i++)
            {
                int index = AddVertex(points[i]);
                triangles.Add(centerIndex);
                triangles.Add(previous);
                triangles.Add(index);
                previous = index;
            }

            triangles.Add(centerIndex);
            triangles.Add(previous);
            triangles.Add(first);
        }

        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "Released Drawing Fish Mesh"
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private int AddVertex(Vector2 canvasPoint)
        {
            vertices.Add(CanvasToLocal(canvasPoint));
            uvs.Add(new Vector2(canvasPoint.x / CanvasWidth, 1f - canvasPoint.y / CanvasHeight));
            return vertices.Count - 1;
        }

        private Vector3 CanvasToLocal(Vector2 canvasPoint)
        {
            float normalizedX = 0.5f - canvasPoint.x / CanvasWidth;
            float normalizedY = 0.5f - canvasPoint.y / CanvasHeight;
            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            float height = Mathf.Max(0.1f, bounds.size.y);
            return bounds.center + new Vector3(0f, normalizedY * height, normalizedX * length);
        }
    }
}
