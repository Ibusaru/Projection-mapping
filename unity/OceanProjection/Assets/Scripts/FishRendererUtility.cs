using System.Collections.Generic;
using UnityEngine;

public static class FishRendererUtility
{
    private static readonly string[] IgnoredNameParts =
    {
        "camera",
        "light",
        "image",
        "plane",
        "quad",
        "reference",
        "background",
        "billboard",
        "sprite",
        "canvas",
        "label",
        "nickname",
        "name tag",
        "tag line",
        "drawing fish",
        "drawing image"
    };

    public static Renderer[] GetVisualRenderers(GameObject root, bool disableIgnored)
    {
        if (root == null)
        {
            return new Renderer[0];
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        List<Renderer> visualRenderers = new List<Renderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (IsFishVisualRenderer(renderer))
            {
                visualRenderers.Add(renderer);
            }
            else if (disableIgnored && renderer != null)
            {
                renderer.enabled = false;
                renderer.gameObject.SetActive(false);
            }
        }

        return visualRenderers.Count > 0 ? visualRenderers.ToArray() : FallbackMeshRenderers(renderers);
    }

    public static Bounds CalculateVisualBounds(GameObject root)
    {
        Renderer[] renderers = GetVisualRenderers(root, false);
        Bounds bounds = root != null
            ? new Bounds(root.transform.position, Vector3.zero)
            : new Bounds(Vector3.zero, Vector3.one);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.one);
    }

    private static Renderer[] FallbackMeshRenderers(Renderer[] renderers)
    {
        List<Renderer> fallback = new List<Renderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsNonMeshRenderer(renderer))
            {
                continue;
            }

            Mesh mesh = GetMesh(renderer);
            if (HasIgnoredNamePart(renderer, mesh))
            {
                continue;
            }

            fallback.Add(renderer);
        }

        return fallback.ToArray();
    }

    private static bool IsFishVisualRenderer(Renderer renderer)
    {
        if (renderer == null || IsNonMeshRenderer(renderer))
        {
            return false;
        }

        Mesh mesh = GetMesh(renderer);
        if (mesh != null && IsSimpleFlatPanel(mesh))
        {
            return false;
        }

        if (HasIgnoredNamePart(renderer, mesh))
        {
            return false;
        }

        return true;
    }

    private static bool HasIgnoredNamePart(Renderer renderer, Mesh mesh)
    {
        string searchableName = RendererSearchName(renderer, mesh);
        for (int i = 0; i < IgnoredNameParts.Length; i++)
        {
            if (searchableName.Contains(IgnoredNameParts[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNonMeshRenderer(Renderer renderer)
    {
        return renderer is LineRenderer
            || renderer is TrailRenderer
            || renderer is ParticleSystemRenderer;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned)
        {
            return skinned.sharedMesh;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        return meshFilter != null ? meshFilter.sharedMesh : null;
    }

    private static bool IsSimpleFlatPanel(Mesh mesh)
    {
        if (mesh.vertexCount > 8)
        {
            return false;
        }

        Vector3 size = mesh.bounds.size;
        float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float middle = size.x + size.y + size.z - max - min;
        if (max <= 0.001f)
        {
            return false;
        }

        return min / max <= 0.08f && middle / max >= 0.2f;
    }

    private static string RendererSearchName(Renderer renderer, Mesh mesh)
    {
        string materialNames = "";
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
            {
                materialNames += " " + materials[i].name;
            }
        }

        string meshName = mesh != null ? mesh.name : "";
        return $"{renderer.name} {renderer.transform.parent?.name} {meshName} {materialNames}".ToLowerInvariant();
    }
}
