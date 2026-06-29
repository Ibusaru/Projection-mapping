using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public partial class FishActor
{
    private void AutoWireVisuals()
    {
        if (!autoWireRenderers)
        {
            return;
        }

        if (modelRoot == null)
        {
            modelRoot = transform;
        }

        initialModelScale = modelRoot.localScale;
        baseModelLocalRotation = modelRoot.localRotation;

        Renderer[] visualRenderers = FishRendererUtility.GetVisualRenderers(gameObject, true);
        if (visualRenderers.Length > 0)
        {
            colorRenderers = visualRenderers;
            subColorRenderers = visualRenderers;
            textureRenderers = visualRenderers;
        }
        else if (colorRenderers == null || colorRenderers.Length == 0)
        {
            colorRenderers = GetComponentsInChildren<Renderer>(true);
            subColorRenderers = colorRenderers;
            textureRenderers = colorRenderers;
        }

        EnsureRenderersVisible(colorRenderers);
        EnsureRenderersVisible(subColorRenderers);
        EnsureRenderersVisible(textureRenderers);
    }

    private void EnsureReleasedFishMaterials()
    {
        if (!releasedFish)
        {
            return;
        }

        EnsureRendererMaterials(colorRenderers);
        EnsureRendererMaterials(subColorRenderers);
        EnsureRendererMaterials(textureRenderers);
    }

    private static void ApplyColor(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer item in renderers)
        {
            if (item != null)
            {
                EnsureHierarchyActive(item.transform);
                item.enabled = true;
                item.material.color = color;
                if (item.material.HasProperty("_BaseColor"))
                {
                    item.material.SetColor("_BaseColor", color);
                }

                if (item.material.HasProperty("_Color"))
                {
                    item.material.SetColor("_Color", color);
                }
            }
        }
    }

    private void ApplyRemoteTexture(string textureUrl)
    {
        if (string.IsNullOrWhiteSpace(textureUrl) || textureUrl == appliedTextureUrl)
        {
            return;
        }

        if (textureCoroutine != null)
        {
            StopCoroutine(textureCoroutine);
        }

        textureCoroutine = StartCoroutine(DownloadAndApplyTexture(textureUrl));
    }

    private IEnumerator DownloadAndApplyTexture(string textureUrl)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(textureUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"FishActor: texture download failed for '{Nickname}': {request.error}");
            textureCoroutine = null;
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);
        if (releasedFish)
        {
            ApplyReleasedDrawingTexture(texture);
            appliedTextureUrl = textureUrl;
            textureCoroutine = null;
            yield break;
        }

        if (remapDrawingTextureForModel)
        {
            texture = DrawingTextureMapper.CreateModelTexture(texture, remappedDrawingTextureSize, drawingAlphaThreshold);
        }

        Renderer[] visualTextureRenderers = FishRendererUtility.GetVisualRenderers(gameObject, true);
        if (visualTextureRenderers.Length > 0)
        {
            textureRenderers = visualTextureRenderers;
        }

        ApplyTexture(textureRenderers, texture);
        appliedTextureUrl = textureUrl;
        textureCoroutine = null;
    }

    private void ApplyReleasedDrawingTexture(Texture2D texture)
    {
        Bounds visualBounds = TryGetVisualBounds(out Bounds bounds)
            ? bounds
            : new Bounds(transform.position, new Vector3(1.8f, 0.9f, 0.1f));

        Renderer[] originalRenderers = FishRendererUtility.GetVisualRenderers(gameObject, false);
        drawingFishVisual = EnsureDrawingFishVisual();
        drawingFishVisual.Apply(texture, visualBounds);

        for (int i = 0; i < originalRenderers.Length; i++)
        {
            Renderer item = originalRenderers[i];
            if (item != null && item != drawingFishVisual.Renderer)
            {
                item.enabled = false;
            }
        }

        Renderer drawingRenderer = drawingFishVisual.Renderer;
        colorRenderers = new[] { drawingRenderer };
        subColorRenderers = colorRenderers;
        textureRenderers = colorRenderers;
    }

    private DrawingFishVisual EnsureDrawingFishVisual()
    {
        if (drawingFishVisual != null)
        {
            return drawingFishVisual;
        }

        drawingFishVisual = GetComponentInChildren<DrawingFishVisual>(true);
        if (drawingFishVisual != null)
        {
            return drawingFishVisual;
        }

        GameObject visualObject = new GameObject("Drawing Fish Visual");
        visualObject.transform.SetParent(transform, false);
        drawingFishVisual = visualObject.AddComponent<DrawingFishVisual>();
        return drawingFishVisual;
    }

    private static void ApplyTexture(Renderer[] renderers, Texture2D texture)
    {
        if (renderers == null || texture == null)
        {
            return;
        }

        foreach (Renderer item in renderers)
        {
            if (item == null)
            {
                continue;
            }

            EnsureHierarchyActive(item.transform);
            item.enabled = true;
            item.material.color = Color.white;
            if (item.material.HasProperty("_BaseColor"))
            {
                item.material.SetColor("_BaseColor", Color.white);
            }

            if (item.material.HasProperty("_Color"))
            {
                item.material.SetColor("_Color", Color.white);
            }

            item.material.mainTexture = texture;
            item.material.mainTextureScale = Vector2.one;
            item.material.mainTextureOffset = Vector2.zero;
            if (item.material.HasProperty("_BaseMap"))
            {
                item.material.SetTexture("_BaseMap", texture);
            }
        }
    }

    private static void EnsureRenderersVisible(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer item in renderers)
        {
            if (item != null)
            {
                EnsureHierarchyActive(item.transform);
                item.enabled = true;
            }
        }
    }

    private static void EnsureRendererMaterials(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return;
        }

        foreach (Renderer item in renderers)
        {
            if (item == null)
            {
                continue;
            }

            Material[] currentMaterials = item.sharedMaterials;
            int materialCount = currentMaterials != null && currentMaterials.Length > 0 ? currentMaterials.Length : 1;
            Material[] nextMaterials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                nextMaterials[i] = currentMaterials != null && i < currentMaterials.Length && currentMaterials[i] != null
                    ? currentMaterials[i]
                    : new Material(shader);
            }

            item.sharedMaterials = nextMaterials;
            EnsureHierarchyActive(item.transform);
            item.enabled = true;
        }
    }

    private static void EnsureHierarchyActive(Transform leaf)
    {
        Transform current = leaf;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            current = current.parent;
        }
    }

    private void RemoveLegacyDrawingBillboards()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (child.name != "Drawing Image Left" && child.name != "Drawing Image Right")
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
}
