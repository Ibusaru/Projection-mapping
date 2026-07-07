using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DrawingFishVisual : MonoBehaviour
{
    private const float CanvasWidth = 1024f;
    private const float CanvasHeight = 512f;
    private const float VisualCanvasXMin = 54f;
    private const float VisualCanvasYMin = 74f;
    private const float VisualCanvasXMax = 1000f;
    private const float VisualCanvasYMax = 456f;
    private const int CurveSegments = 48;
    private const string ShadowObjectName = "Drawing Fish Soft Shadow";

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Material material;
    private MeshRenderer shadowRenderer;
    private MeshFilter shadowFilter;
    private Material shadowMaterial;

    public Renderer Renderer => meshRenderer;

    public void Apply(Texture2D texture, Bounds worldBounds)
    {
        Apply(texture, worldBounds, 0f);
    }

    public void Apply(Texture2D texture, Bounds worldBounds, float sideOffset)
    {
        if (texture == null)
        {
            return;
        }

        gameObject.SetActive(true);
        EnsureComponents();
        EnsureMaterial(texture);
        EnsureShadowMaterial(texture);

        Bounds localBounds = WorldBoundsToLocal(transform.parent != null ? transform.parent : transform, worldBounds);
        meshFilter.sharedMesh = CreateFishMesh(localBounds, sideOffset);
        shadowFilter.sharedMesh = CreateShadowMesh(localBounds, sideOffset);
        meshRenderer.sharedMaterial = material;
        shadowRenderer.sharedMaterial = shadowMaterial;
        meshRenderer.enabled = true;
        shadowRenderer.enabled = true;
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

            meshRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            meshRenderer.receiveShadows = true;
            meshRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        if (shadowFilter == null || shadowRenderer == null)
        {
            Transform shadowTransform = transform.Find(ShadowObjectName);
            GameObject shadowObject = shadowTransform != null
                ? shadowTransform.gameObject
                : new GameObject(ShadowObjectName);
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = Vector3.zero;
            shadowObject.transform.localRotation = Quaternion.identity;
            shadowObject.transform.localScale = Vector3.one;

            shadowFilter = shadowObject.GetComponent<MeshFilter>();
            if (shadowFilter == null)
            {
                shadowFilter = shadowObject.AddComponent<MeshFilter>();
            }

            shadowRenderer = shadowObject.GetComponent<MeshRenderer>();
            if (shadowRenderer == null)
            {
                shadowRenderer = shadowObject.AddComponent<MeshRenderer>();
            }

            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
        }
    }

    private void EnsureMaterial(Texture2D texture)
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            material = new Material(shader)
            {
                name = "Released Fish Drawing Lit",
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

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.34f);
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

    private void EnsureShadowMaterial(Texture2D texture)
    {
        if (shadowMaterial == null)
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

            shadowMaterial = new Material(shader)
            {
                name = "Released Fish Drawing Soft Shadow",
                renderQueue = (int)RenderQueue.Transparent - 1
            };
            shadowMaterial.SetOverrideTag("RenderType", "Transparent");
        }

        shadowMaterial.mainTexture = texture;
        if (shadowMaterial.HasProperty("_MainTex"))
        {
            shadowMaterial.SetTexture("_MainTex", texture);
        }

        if (shadowMaterial.HasProperty("_BaseMap"))
        {
            shadowMaterial.SetTexture("_BaseMap", texture);
        }

        Color shadowColor = new Color(0f, 0f, 0f, 0.28f);
        if (shadowMaterial.HasProperty("_Color"))
        {
            shadowMaterial.SetColor("_Color", shadowColor);
        }

        if (shadowMaterial.HasProperty("_BaseColor"))
        {
            shadowMaterial.SetColor("_BaseColor", shadowColor);
        }

        if (shadowMaterial.HasProperty("_Surface"))
        {
            shadowMaterial.SetFloat("_Surface", 1f);
        }

        if (shadowMaterial.HasProperty("_Blend"))
        {
            shadowMaterial.SetFloat("_Blend", 0f);
        }

        if (shadowMaterial.HasProperty("_Cull"))
        {
            shadowMaterial.SetFloat("_Cull", 0f);
        }

        shadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        shadowMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static Mesh CreateFishMesh(Bounds localBounds, float sideOffset)
    {
        return CreateFishMesh(localBounds, sideOffset, 0f, 1f, 1f);
    }

    private static Mesh CreateShadowMesh(Bounds localBounds, float sideOffset)
    {
        float length = Mathf.Max(localBounds.size.x, localBounds.size.z);
        float height = Mathf.Max(0.1f, localBounds.size.y);
        float shadowSideOffset = sideOffset - Mathf.Max(0.015f, length * 0.018f);
        float shadowVerticalOffset = -Mathf.Max(0.02f, height * 0.055f);
        return CreateFishMesh(localBounds, shadowSideOffset, shadowVerticalOffset, 1.035f, 1.02f);
    }

    private static Mesh CreateFishMesh(
        Bounds localBounds,
        float sideOffset,
        float verticalOffset,
        float lengthScale,
        float heightScale
    )
    {
        MeshBuilder builder = new MeshBuilder(localBounds, sideOffset, verticalOffset, lengthScale, heightScale);

        List<Vector2> body = new List<Vector2>
        {
            new Vector2(54f, 258f),
            new Vector2(205f, 158f),
            new Vector2(292f, 132f),
            new Vector2(358f, 74f),
            new Vector2(472f, 122f),
            new Vector2(426f, 158f),
            new Vector2(530f, 130f),
            new Vector2(598f, 206f),
            new Vector2(747f, 184f),
            new Vector2(790f, 218f),
            new Vector2(812f, 258f),
            new Vector2(790f, 296f),
            new Vector2(742f, 310f),
            new Vector2(624f, 300f),
            new Vector2(578f, 350f),
            new Vector2(456f, 368f),
            new Vector2(374f, 352f),
            new Vector2(328f, 402f),
            new Vector2(292f, 356f),
            new Vector2(188f, 344f)
        };
        builder.AddFan(body);

        List<Vector2> tail = new List<Vector2>
        {
            new Vector2(754f, 188f),
            new Vector2(955f, 148f),
            new Vector2(1000f, 260f),
            new Vector2(954f, 340f),
            new Vector2(754f, 304f),
            new Vector2(806f, 258f)
        };
        builder.AddFan(tail);

        List<Vector2> frontFin = new List<Vector2>
        {
            new Vector2(334f, 246f),
            new Vector2(424f, 236f),
            new Vector2(414f, 322f),
            new Vector2(336f, 338f)
        };
        builder.AddFan(frontFin);

        List<Vector2> bellyFin = new List<Vector2>
        {
            new Vector2(404f, 358f),
            new Vector2(478f, 382f),
            new Vector2(516f, 456f),
            new Vector2(454f, 424f)
        };
        builder.AddFan(bellyFin);

        List<Vector2> longFin = new List<Vector2>
        {
            new Vector2(522f, 342f),
            new Vector2(610f, 386f),
            new Vector2(566f, 430f),
            new Vector2(492f, 368f)
        };
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
        private readonly float sideOffset;
        private readonly float verticalOffset;
        private readonly float lengthScale;
        private readonly float heightScale;
        private readonly bool useZLength;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public MeshBuilder(Bounds localBounds, float sideOffset, float verticalOffset, float lengthScale, float heightScale)
        {
            bounds = localBounds.size.sqrMagnitude > 0.001f
                ? localBounds
                : new Bounds(Vector3.zero, new Vector3(1.8f, 0.9f, 0.1f));
            this.sideOffset = sideOffset;
            this.verticalOffset = verticalOffset;
            this.lengthScale = Mathf.Max(0.1f, lengthScale);
            this.heightScale = Mathf.Max(0.1f, heightScale);
            useZLength = bounds.size.z >= bounds.size.x;
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
            float normalizedX = 0.5f - Mathf.InverseLerp(VisualCanvasXMin, VisualCanvasXMax, canvasPoint.x);
            float normalizedY = 0.5f - Mathf.InverseLerp(VisualCanvasYMin, VisualCanvasYMax, canvasPoint.y);
            float length = Mathf.Max(bounds.size.x, bounds.size.z) * lengthScale;
            float height = Mathf.Max(0.1f, bounds.size.y) * heightScale;
            return useZLength
                ? bounds.center + new Vector3(sideOffset, normalizedY * height + verticalOffset, normalizedX * length)
                : bounds.center + new Vector3(normalizedX * length, normalizedY * height + verticalOffset, sideOffset);
        }
    }
}
