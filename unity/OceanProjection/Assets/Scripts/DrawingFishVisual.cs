using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DrawingFishVisual : MonoBehaviour
{
    private const float CanvasWidth = 1024f;
    private const float CanvasHeight = 512f;
    private const int CurveSegments = 48;

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

        List<Vector2> body = new List<Vector2> { new Vector2(144f, 252f) };
        AddCubic(body, new Vector2(144f, 252f), new Vector2(168f, 164f), new Vector2(270f, 118f), new Vector2(410f, 118f), CurveSegments);
        AddCubic(body, new Vector2(410f, 118f), new Vector2(548f, 118f), new Vector2(674f, 158f), new Vector2(754f, 222f), CurveSegments);
        body.Add(new Vector2(808f, 224f));
        AddCubic(body, new Vector2(808f, 224f), new Vector2(830f, 236f), new Vector2(842f, 245f), new Vector2(848f, 256f), CurveSegments);
        AddCubic(body, new Vector2(848f, 256f), new Vector2(842f, 267f), new Vector2(830f, 276f), new Vector2(808f, 288f), CurveSegments);
        body.Add(new Vector2(754f, 290f));
        AddCubic(body, new Vector2(754f, 290f), new Vector2(674f, 354f), new Vector2(548f, 394f), new Vector2(410f, 394f), CurveSegments);
        AddCubic(body, new Vector2(410f, 394f), new Vector2(270f, 394f), new Vector2(168f, 348f), new Vector2(144f, 260f), CurveSegments);
        AddCubic(body, new Vector2(144f, 260f), new Vector2(143f, 257f), new Vector2(143f, 255f), new Vector2(144f, 252f), 4);
        builder.AddFan(body);

        List<Vector2> tail = new List<Vector2> { new Vector2(804f, 224f), new Vector2(968f, 128f) };
        AddCubic(tail, new Vector2(968f, 128f), new Vector2(940f, 186f), new Vector2(925f, 229f), new Vector2(925f, 256f), CurveSegments);
        AddCubic(tail, new Vector2(925f, 256f), new Vector2(925f, 283f), new Vector2(940f, 326f), new Vector2(968f, 384f), CurveSegments);
        tail.Add(new Vector2(804f, 288f));
        builder.AddFan(tail);

        List<Vector2> topFin = new List<Vector2> { new Vector2(350f, 123f) };
        AddCubic(topFin, new Vector2(350f, 123f), new Vector2(420f, 50f), new Vector2(548f, 70f), new Vector2(606f, 144f), CurveSegments);
        topFin.Add(new Vector2(514f, 150f));
        AddCubic(topFin, new Vector2(514f, 150f), new Vector2(470f, 130f), new Vector2(410f, 120f), new Vector2(350f, 123f), CurveSegments);
        builder.AddFan(topFin);

        List<Vector2> bottomFin = new List<Vector2> { new Vector2(455f, 382f) };
        AddCubic(bottomFin, new Vector2(455f, 382f), new Vector2(515f, 454f), new Vector2(625f, 432f), new Vector2(666f, 354f), CurveSegments);
        bottomFin.Add(new Vector2(570f, 366f));
        AddCubic(bottomFin, new Vector2(570f, 366f), new Vector2(528f, 380f), new Vector2(490f, 386f), new Vector2(455f, 382f), CurveSegments);
        builder.AddFan(bottomFin);

        return builder.ToMesh();
    }

    private static void AddCubic(List<Vector2> points, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int segments)
    {
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float inv = 1f - t;
            points.Add(inv * inv * inv * a + 3f * inv * inv * t * b + 3f * inv * t * t * c + t * t * t * d);
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
