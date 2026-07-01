using System.Collections.Generic;
using UnityEngine;

public partial class FishActor
{
    private const string DrawingProjectionShaderName = "OceanProjection/Drawing Fish Projection";

    private bool ApplyProjectedDrawingTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return false;
        }

        Shader shader = Shader.Find(DrawingProjectionShaderName);
        if (shader == null)
        {
            shader = Resources.Load<Shader>("Shaders/DrawingFishProjection");
        }

        if (shader == null)
        {
            Debug.LogWarning($"FishActor: shader '{DrawingProjectionShaderName}' was not found; falling back to UV texture mapping.");
            return false;
        }

        Renderer[] visualTextureRenderers = FishRendererUtility.GetVisualRenderers(gameObject, true);
        if (visualTextureRenderers.Length == 0)
        {
            return false;
        }

        Transform projector = modelRoot != null ? modelRoot : transform;
        if (!TryCalculateProjectionBounds(visualTextureRenderers, projector, out Bounds projectionBounds))
        {
            return false;
        }

        Texture2D projectionTexture = DrawingTextureMapper.CreateProjectionTexture(texture, drawingAlphaThreshold);
        if (projectionTexture == null)
        {
            return false;
        }

        projectionTexture.wrapMode = TextureWrapMode.Clamp;
        projectionTexture.filterMode = FilterMode.Bilinear;
        textureRenderers = visualTextureRenderers;
        drawingProjectionRoot = projector;

        CreateProjectionFrame(
            projectionBounds,
            flipReleasedDrawingHorizontally,
            out Vector3 origin,
            out Vector3 uVector,
            out Vector3 vVector
        );

        Matrix4x4 worldToProjector = projector.worldToLocalMatrix;
        List<Material> projectionMaterials = new List<Material>();
        for (int i = 0; i < visualTextureRenderers.Length; i++)
        {
            Renderer item = visualTextureRenderers[i];
            if (item == null)
            {
                continue;
            }

            EnsureHierarchyActive(item.transform);
            item.enabled = true;
            Material[] currentMaterials = item.sharedMaterials;
            int materialCount = currentMaterials != null && currentMaterials.Length > 0 ? currentMaterials.Length : 1;
            Material[] nextMaterials = new Material[materialCount];
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                Material material = new Material(shader)
                {
                    name = "Released Fish Drawing Projection"
                };
                ConfigureProjectionMaterial(material, projectionTexture, worldToProjector, origin, uVector, vVector);
                nextMaterials[materialIndex] = material;
                projectionMaterials.Add(material);
            }

            item.materials = nextMaterials;
        }

        projectedDrawingMaterials = projectionMaterials.ToArray();
        return projectedDrawingMaterials.Length > 0;
    }

    private void UpdateDrawingProjectionMatrix()
    {
        if (drawingProjectionRoot == null || projectedDrawingMaterials == null)
        {
            return;
        }

        Matrix4x4 worldToProjector = drawingProjectionRoot.worldToLocalMatrix;
        for (int i = 0; i < projectedDrawingMaterials.Length; i++)
        {
            Material material = projectedDrawingMaterials[i];
            if (material != null)
            {
                material.SetMatrix("_DrawingWorldToProjector", worldToProjector);
            }
        }
    }

    private void ConfigureProjectionMaterial(
        Material material,
        Texture2D texture,
        Matrix4x4 worldToProjector,
        Vector3 origin,
        Vector3 uVector,
        Vector3 vVector
    )
    {
        material.SetTexture("_DrawingTex", texture);
        material.SetColor("_Tint", Color.white);
        material.SetFloat("_AlphaClip", Mathf.Clamp01(drawingAlphaThreshold));
        material.SetMatrix("_DrawingWorldToProjector", worldToProjector);
        material.SetVector("_DrawingProjectorOrigin", origin);
        material.SetVector("_DrawingProjectorU", uVector);
        material.SetVector("_DrawingProjectorV", vVector);
    }

    private static void CreateProjectionFrame(
        Bounds bounds,
        bool flipHorizontal,
        out Vector3 origin,
        out Vector3 uVector,
        out Vector3 vVector
    )
    {
        Vector3 size = bounds.size;
        bool useZLength = size.z >= size.x;
        float length = Mathf.Max(useZLength ? size.z : size.x, 0.001f);
        float height = Mathf.Max(size.y, 0.001f);

        origin = bounds.min;
        origin.y = bounds.min.y;
        vVector = Vector3.up * height;

        if (useZLength)
        {
            origin.z = flipHorizontal ? bounds.max.z : bounds.min.z;
            uVector = (flipHorizontal ? Vector3.back : Vector3.forward) * length;
            return;
        }

        origin.x = flipHorizontal ? bounds.max.x : bounds.min.x;
        uVector = (flipHorizontal ? Vector3.left : Vector3.right) * length;
    }

    private static bool TryCalculateProjectionBounds(Renderer[] renderers, Transform projector, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        if (renderers == null || projector == null)
        {
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            if (worldBounds.size.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, worldBounds.min);
            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, new Vector3(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z));
            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z));
            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z));
            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z));
            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z));
            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, new Vector3(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z));
            EncapsulateWorldBoundsCorner(ref bounds, ref hasBounds, projector, worldBounds.max);
        }

        return hasBounds;
    }

    private static void EncapsulateWorldBoundsCorner(
        ref Bounds bounds,
        ref bool hasBounds,
        Transform projector,
        Vector3 worldPoint
    )
    {
        Vector3 localPoint = projector.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            bounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(localPoint);
    }
}
