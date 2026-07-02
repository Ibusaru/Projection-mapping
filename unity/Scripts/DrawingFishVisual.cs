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

        List<Vector2> body = new List<Vector2> { new Vector2(932f, 252f) };
        AddCubic(body, new Vector2(932f, 252f), new Vector2(920f, 190f), new Vector2(846f, 145f), new Vector2(749f, 134f), CurveSegments);
        AddCubic(body, new Vector2(749f, 134f), new Vector2(720f, 102f), new Vector2(676f, 82f), new Vector2(618f, 84f), CurveSegments);
        AddCubic(body, new Vector2(618f, 84f), new Vector2(558f, 86f), new Vector2(512f, 119f), new Vector2(504f, 152f), CurveSegments);
        AddCubic(body, new Vector2(504f, 152f), new Vector2(478f, 134f), new Vector2(438f, 128f), new Vector2(404f, 146f), CurveSegments);
        AddCubic(body, new Vector2(404f, 146f), new Vector2(376f, 160f), new Vector2(350f, 187f), new Vector2(320f, 202f), CurveSegments);
        AddCubic(body, new Vector2(320f, 202f), new Vector2(294f, 216f), new Vector2(266f, 216f), new Vector2(238f, 222f), CurveSegments);
        AddCubic(body, new Vector2(238f, 222f), new Vector2(224f, 238f), new Vector2(217f, 256f), new Vector2(220f, 275f), CurveSegments);
        AddCubic(body, new Vector2(220f, 275f), new Vector2(254f, 284f), new Vector2(288f, 286f), new Vector2(314f, 304f), CurveSegments);
        AddCubic(body, new Vector2(314f, 304f), new Vector2(364f, 342f), new Vector2(440f, 368f), new Vector2(554f, 379f), CurveSegments);
        AddCubic(body, new Vector2(554f, 379f), new Vector2(684f, 393f), new Vector2(824f, 372f), new Vector2(894f, 316f), CurveSegments);
        AddCubic(body, new Vector2(894f, 316f), new Vector2(920f, 294f), new Vector2(933f, 273f), new Vector2(932f, 252f), CurveSegments);
        builder.AddFan(body);

        List<Vector2> tail = new List<Vector2> { new Vector2(244f, 220f) };
        AddCubic(tail, new Vector2(244f, 220f), new Vector2(204f, 198f), new Vector2(154f, 178f), new Vector2(89f, 181f), CurveSegments);
        AddCubic(tail, new Vector2(89f, 181f), new Vector2(52f, 184f), new Vector2(33f, 205f), new Vector2(36f, 244f), CurveSegments);
        AddCubic(tail, new Vector2(36f, 244f), new Vector2(39f, 270f), new Vector2(39f, 294f), new Vector2(34f, 326f), CurveSegments);
        AddCubic(tail, new Vector2(34f, 326f), new Vector2(72f, 341f), new Vector2(124f, 335f), new Vector2(174f, 313f), CurveSegments);
        AddCubic(tail, new Vector2(174f, 313f), new Vector2(204f, 300f), new Vector2(228f, 288f), new Vector2(244f, 286f), CurveSegments);
        builder.AddFan(tail);

        List<Vector2> frontFin = new List<Vector2> { new Vector2(796f, 360f) };
        AddCubic(frontFin, new Vector2(796f, 360f), new Vector2(764f, 424f), new Vector2(682f, 416f), new Vector2(650f, 348f), CurveSegments);
        frontFin.Add(new Vector2(710f, 360f));
        AddCubic(frontFin, new Vector2(710f, 360f), new Vector2(738f, 366f), new Vector2(770f, 366f), new Vector2(796f, 360f), CurveSegments);
        builder.AddFan(frontFin);

        List<Vector2> bellyFin = new List<Vector2> { new Vector2(522f, 354f) };
        AddCubic(bellyFin, new Vector2(522f, 354f), new Vector2(484f, 430f), new Vector2(398f, 438f), new Vector2(356f, 360f), CurveSegments);
        bellyFin.Add(new Vector2(420f, 370f));
        AddCubic(bellyFin, new Vector2(420f, 370f), new Vector2(456f, 370f), new Vector2(492f, 364f), new Vector2(522f, 354f), CurveSegments);
        builder.AddFan(bellyFin);

        List<Vector2> longFin = new List<Vector2> { new Vector2(548f, 346f) };
        AddCubic(longFin, new Vector2(548f, 346f), new Vector2(584f, 392f), new Vector2(592f, 460f), new Vector2(560f, 500f), CurveSegments);
        AddCubic(longFin, new Vector2(560f, 500f), new Vector2(528f, 505f), new Vector2(505f, 486f), new Vector2(506f, 448f), CurveSegments);
        AddCubic(longFin, new Vector2(506f, 448f), new Vector2(508f, 404f), new Vector2(520f, 370f), new Vector2(548f, 346f), CurveSegments);
        builder.AddFan(longFin);

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
