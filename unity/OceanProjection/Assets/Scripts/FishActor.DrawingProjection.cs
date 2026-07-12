using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class FishActor
{
    private const string DrawingProjectionShaderName = "OceanProjection/Drawing Fish Projection";
    private const string DrawingUvShaderName = "OceanProjection/Drawing Fish UV";
    private static readonly Color DrawingProjectionFallbackBaseColor = new Color(0.52f, 0.68f, 0.72f, 1f);

    private bool ApplyAuthoredUvDrawingTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return false;
        }

        Shader shader = Shader.Find(DrawingUvShaderName);
        if (shader == null)
        {
            shader = Resources.Load<Shader>("Shaders/DrawingFishUv");
        }

        if (!IsUsableProjectionShader(shader))
        {
            Debug.LogWarning($"FishActor: shader '{DrawingUvShaderName}' was not found or is unsupported; falling back to projected texture mapping.");
            return false;
        }

        Renderer[] visualTextureRenderers = FishRendererUtility.GetVisualRenderers(gameObject, true);
        if (visualTextureRenderers.Length == 0 || !DrawingTextureMapper.HasAuthoredDrawingUvs(visualTextureRenderers))
        {
            return false;
        }

        Texture2D uvTexture = DrawingTextureMapper.CreateDisplayTexture(texture) ?? texture;
        uvTexture.wrapMode = TextureWrapMode.Clamp;
        uvTexture.filterMode = FilterMode.Bilinear;

        ApplyUvDrawingTextureToRenderers(visualTextureRenderers, shader, uvTexture);
        HideDrawingFishVisual();
        return true;
    }

    private bool ApplyGeneratedUvDrawingTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return false;
        }

        Shader shader = Shader.Find(DrawingUvShaderName);
        if (shader == null)
        {
            shader = Resources.Load<Shader>("Shaders/DrawingFishUv");
        }

        if (!IsUsableProjectionShader(shader))
        {
            Debug.LogWarning($"FishActor: shader '{DrawingUvShaderName}' was not found or is unsupported; falling back to projected texture mapping.");
            return false;
        }

        Renderer[] visualTextureRenderers = FishRendererUtility.GetVisualRenderers(gameObject, true);
        if (visualTextureRenderers.Length == 0)
        {
            return false;
        }

        Transform projector = modelRoot != null ? modelRoot : transform;
        if (!DrawingTextureMapper.ApplyGeneratedUvs(visualTextureRenderers, projector, flipReleasedDrawingHorizontally))
        {
            return false;
        }

        Texture2D uvTexture = DrawingTextureMapper.CreateModelTexture(texture, remappedDrawingTextureSize, drawingAlphaThreshold);
        if (uvTexture == null)
        {
            return false;
        }

        uvTexture.wrapMode = TextureWrapMode.Clamp;
        uvTexture.filterMode = FilterMode.Bilinear;

        ApplyUvDrawingTextureToRenderers(visualTextureRenderers, shader, uvTexture);
        HideDrawingFishVisual();
        return true;
    }

    private void ApplyUvDrawingTextureToRenderers(Renderer[] visualTextureRenderers, Shader shader, Texture2D uvTexture)
    {
        textureRenderers = visualTextureRenderers;
        colorRenderers = visualTextureRenderers;
        subColorRenderers = visualTextureRenderers;
        drawingProjectionRoot = null;
        projectedDrawingMaterials = new Material[0];

        for (int i = 0; i < visualTextureRenderers.Length; i++)
        {
            Renderer item = visualTextureRenderers[i];
            if (item == null)
            {
                continue;
            }

            EnsureHierarchyActive(item.transform);
            item.enabled = true;
            EnableDrawingLighting(item);
            Material[] currentMaterials = item.sharedMaterials;
            int materialCount = currentMaterials != null && currentMaterials.Length > 0 ? currentMaterials.Length : 1;
            Material[] nextMaterials = new Material[materialCount];
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                Material sourceMaterial = currentMaterials != null && materialIndex < currentMaterials.Length
                    ? currentMaterials[materialIndex]
                    : null;
                Material material = new Material(shader)
                {
                    name = "Released Fish Drawing UV"
                };
                ConfigureUvDrawingMaterial(material, sourceMaterial, uvTexture);
                nextMaterials[materialIndex] = material;
            }

            item.materials = nextMaterials;
        }
    }

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

        if (!IsUsableProjectionShader(shader))
        {
            Debug.LogWarning($"FishActor: shader '{DrawingProjectionShaderName}' was not found or is unsupported; falling back to UV texture mapping.");
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

        Texture2D projectionTexture = DrawingTextureMapper.CreateModelTexture(texture, remappedDrawingTextureSize, drawingAlphaThreshold);
        if (projectionTexture == null)
        {
            return false;
        }

        projectionTexture.wrapMode = TextureWrapMode.Clamp;
        projectionTexture.filterMode = FilterMode.Bilinear;
        textureRenderers = visualTextureRenderers;
        colorRenderers = visualTextureRenderers;
        subColorRenderers = visualTextureRenderers;
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
            EnableDrawingLighting(item);
            Material[] currentMaterials = item.sharedMaterials;
            int materialCount = currentMaterials != null && currentMaterials.Length > 0 ? currentMaterials.Length : 1;
            Material[] nextMaterials = new Material[materialCount];
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                Material sourceMaterial = currentMaterials != null && materialIndex < currentMaterials.Length
                    ? currentMaterials[materialIndex]
                    : null;
                Material material = new Material(shader)
                {
                    name = "Released Fish Drawing Projection"
                };
                ConfigureProjectionMaterial(material, sourceMaterial, projectionTexture, worldToProjector, origin, uVector, vVector);
                nextMaterials[materialIndex] = material;
                projectionMaterials.Add(material);
            }

            item.materials = nextMaterials;
        }

        projectedDrawingMaterials = projectionMaterials.ToArray();
        if (projectedDrawingMaterials.Length > 0)
        {
            HideDrawingFishVisual();
        }

        return projectedDrawingMaterials.Length > 0;
    }

    private void ConfigureUvDrawingMaterial(Material material, Material sourceMaterial, Texture2D texture)
    {
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_DrawingTex", texture);
        material.SetColor("_Tint", Color.white);
        material.SetColor("_BaseColor", ReadProjectionBaseColor(sourceMaterial, texture));
        material.SetFloat("_AlphaClip", Mathf.Clamp01(drawingAlphaThreshold));
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

        HideDrawingFishVisual();
    }

    private void ConfigureProjectionMaterial(
        Material material,
        Material sourceMaterial,
        Texture2D texture,
        Matrix4x4 worldToProjector,
        Vector3 origin,
        Vector3 uVector,
        Vector3 vVector
    )
    {
        material.SetTexture("_DrawingTex", texture);
        material.SetColor("_Tint", Color.white);
        material.SetColor("_BaseColor", ReadProjectionBaseColor(sourceMaterial, texture));
        material.SetFloat("_AlphaClip", Mathf.Clamp01(drawingAlphaThreshold));
        material.SetMatrix("_DrawingWorldToProjector", worldToProjector);
        material.SetVector("_DrawingProjectorOrigin", origin);
        material.SetVector("_DrawingProjectorU", uVector);
        material.SetVector("_DrawingProjectorV", vVector);
    }

    private static Color ReadMaterialBaseColor(Material material)
    {
        if (material == null || IsBrokenSourceShader(material.shader))
        {
            return DrawingProjectionFallbackBaseColor;
        }

        Color color = DrawingProjectionFallbackBaseColor;
        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
        }
        else if (material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
        }

        return LooksLikeUnityErrorColor(color) ? DrawingProjectionFallbackBaseColor : color;
    }

    private static Color ReadProjectionBaseColor(Material sourceMaterial, Texture2D texture)
    {
        Color materialColor = ReadMaterialBaseColor(sourceMaterial);
        if (!LooksLikeBlankBaseColor(materialColor) || !TryAverageVisibleDrawingColor(texture, out Color drawingColor))
        {
            return materialColor;
        }

        Color color = Color.Lerp(DrawingProjectionFallbackBaseColor, drawingColor, 0.18f);
        color.a = materialColor.a;
        return color;
    }

    private static bool TryAverageVisibleDrawingColor(Texture2D texture, out Color color)
    {
        color = Color.white;
        if (texture == null)
        {
            return false;
        }

        Color32[] pixels;
        try
        {
            pixels = texture.GetPixels32();
        }
        catch (UnityException)
        {
            return false;
        }

        if (pixels == null || pixels.Length == 0)
        {
            return false;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;
        int step = Mathf.Max(1, pixels.Length / 4096);
        for (int i = 0; i < pixels.Length; i += step)
        {
            Color32 pixel = pixels[i];
            if (pixel.a < 24 || IsNearWhite(pixel))
            {
                continue;
            }

            sum += new Vector3(pixel.r, pixel.g, pixel.b) / 255f;
            count++;
        }

        if (count < 8)
        {
            return false;
        }

        Vector3 average = sum / count;
        color = new Color(average.x, average.y, average.z, 1f);
        return true;
    }

    private static bool IsNearWhite(Color32 color)
    {
        byte max = System.Math.Max(color.r, System.Math.Max(color.g, color.b));
        byte min = System.Math.Min(color.r, System.Math.Min(color.g, color.b));
        float luma = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
        return (max >= 244 && max - min <= 10)
            || (max >= 235 && max - min <= 46)
            || (luma >= 0.86f && max - min <= 32);
    }

    private static bool IsUsableProjectionShader(Shader shader)
    {
        return shader != null
            && shader.isSupported
            && shader.name != "Hidden/InternalErrorShader";
    }

    private static bool IsBrokenSourceShader(Shader shader)
    {
        if (!IsUsableProjectionShader(shader))
        {
            return true;
        }

        if (!IsUniversalRenderPipelineActiveForProjection())
        {
            return false;
        }

        string shaderName = shader.name.ToLowerInvariant();
        return shaderName == "standard"
            || shaderName.StartsWith("legacy shaders/")
            || shaderName.StartsWith("mobile/");
    }

    private static bool IsUniversalRenderPipelineActiveForProjection()
    {
        UnityEngine.Rendering.RenderPipelineAsset pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        return pipeline != null && pipeline.GetType().FullName.Contains("Universal");
    }

    private static bool LooksLikeUnityErrorColor(Color color)
    {
        return color.r > 0.85f && color.g < 0.24f && color.b > 0.75f;
    }

    private static bool LooksLikeBlankBaseColor(Color color)
    {
        float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return max > 0.88f && max - min < 0.08f;
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
        int lengthAxis = LargestAxis(size, -1);
        int heightAxis = ChooseProjectionHeightAxis(size, lengthAxis);
        float length = Mathf.Max(AxisValue(size, lengthAxis), 0.001f);
        float height = Mathf.Max(AxisValue(size, heightAxis), 0.001f);

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        origin = bounds.min;
        SetAxisValue(ref origin, lengthAxis, flipHorizontal ? AxisValue(max, lengthAxis) : AxisValue(min, lengthAxis));
        SetAxisValue(ref origin, heightAxis, AxisValue(min, heightAxis));
        uVector = AxisVector(lengthAxis, flipHorizontal ? -length : length);
        vVector = AxisVector(heightAxis, height);
    }

    private static int LargestAxis(Vector3 value, int ignoredAxis)
    {
        int bestAxis = ignoredAxis == 0 ? 1 : 0;
        float bestValue = AxisValue(value, bestAxis);
        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == ignoredAxis)
            {
                continue;
            }

            float candidate = AxisValue(value, axis);
            if (candidate > bestValue)
            {
                bestAxis = axis;
                bestValue = candidate;
            }
        }

        return bestAxis;
    }

    private static int ChooseProjectionHeightAxis(Vector3 size, int lengthAxis)
    {
        const int unityUpAxis = 1;
        if (lengthAxis != unityUpAxis
            && AxisValue(size, unityUpAxis) >= AxisValue(size, lengthAxis) * 0.08f)
        {
            return unityUpAxis;
        }

        return LargestAxis(size, lengthAxis);
    }

    private static float AxisValue(Vector3 value, int axis)
    {
        return axis switch
        {
            0 => value.x,
            1 => value.y,
            _ => value.z
        };
    }

    private static void SetAxisValue(ref Vector3 value, int axis, float axisValue)
    {
        if (axis == 0)
        {
            value.x = axisValue;
        }
        else if (axis == 1)
        {
            value.y = axisValue;
        }
        else
        {
            value.z = axisValue;
        }
    }

    private static Vector3 AxisVector(int axis, float magnitude)
    {
        return axis switch
        {
            0 => Vector3.right * magnitude,
            1 => Vector3.up * magnitude,
            _ => Vector3.forward * magnitude
        };
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

            Bounds localBounds = renderer.localBounds;
            if (localBounds.size.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, localBounds.min);
            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, new Vector3(localBounds.min.x, localBounds.min.y, localBounds.max.z));
            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, new Vector3(localBounds.min.x, localBounds.max.y, localBounds.min.z));
            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, new Vector3(localBounds.min.x, localBounds.max.y, localBounds.max.z));
            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, new Vector3(localBounds.max.x, localBounds.min.y, localBounds.min.z));
            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, new Vector3(localBounds.max.x, localBounds.min.y, localBounds.max.z));
            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, new Vector3(localBounds.max.x, localBounds.max.y, localBounds.min.z));
            EncapsulateRendererLocalCorner(ref bounds, ref hasBounds, projector, renderer, localBounds.max);
        }

        return hasBounds;
    }

    private static void EnableDrawingLighting(Renderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }

    private static void EncapsulateRendererLocalCorner(
        ref Bounds bounds,
        ref bool hasBounds,
        Transform projector,
        Renderer renderer,
        Vector3 rendererLocalPoint
    )
    {
        Vector3 worldPoint = renderer.transform.TransformPoint(rendererLocalPoint);
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
