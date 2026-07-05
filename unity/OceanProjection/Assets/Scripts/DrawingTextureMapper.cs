using UnityEngine;

public static class DrawingTextureMapper
{
    private const float CanvasWidth = 1024f;
    private const float CanvasHeight = 512f;
    private const float VisualCanvasXMin = 91f;
    private const float VisualCanvasYMin = 82f;
    private const float VisualCanvasXMax = 990f;
    private const float VisualCanvasYMax = 505f;

    public static Texture2D CreateProjectionTexture(Texture2D source, float alphaThreshold)
    {
        return CreateProjectionTexture(source, alphaThreshold, Vector2.zero);
    }

    public static Texture2D CreateProjectionTexture(Texture2D source, float alphaThreshold, Vector2 projectionPaddingRatio)
    {
        if (source == null)
        {
            return null;
        }

        Color32[] sourcePixels = source.GetPixels32();
        Color32[] outputPixels = new Color32[sourcePixels.Length];
        Rect sourceRect = TryFindPaintedBounds(sourcePixels, source.width, source.height, alphaThreshold, out RectInt paintedBounds)
            ? ExpandPaintedBounds(paintedBounds, source.width, source.height, projectionPaddingRatio)
            : CalculateProjectionSourceRect(source.width, source.height, projectionPaddingRatio);

        for (int y = 0; y < source.height; y++)
        {
            float v = source.height <= 1 ? 0f : y / (float)(source.height - 1);
            int sourceY = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(sourceRect.yMin, sourceRect.yMax, v)), 0, source.height - 1);

            for (int x = 0; x < source.width; x++)
            {
                int outputIndex = y * source.width + x;
                float u = source.width <= 1 ? 0f : x / (float)(source.width - 1);
                int sourceX = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(sourceRect.xMin, sourceRect.xMax, u)), 0, source.width - 1);
                Color32 color = sourcePixels[sourceY * source.width + sourceX];
                outputPixels[outputIndex] = IsPaintedPixel(color, alphaThreshold) ? color : Transparent;
            }
        }

        Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
        {
            name = $"{source.name}_ProjectionCanvas",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(outputPixels);
        texture.Apply(true, false);
        return texture;
    }

    private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

    private static Rect CalculateProjectionSourceRect(int width, int height, Vector2 paddingRatio)
    {
        float safePaddingX = Mathf.Clamp(paddingRatio.x, 0f, 0.45f);
        float safePaddingY = Mathf.Clamp(paddingRatio.y, 0f, 0.45f);
        float canvasPaddingX = (VisualCanvasXMax - VisualCanvasXMin) * safePaddingX;
        float canvasPaddingY = (VisualCanvasYMax - VisualCanvasYMin) * safePaddingY;

        float canvasXMin = Mathf.Max(0f, VisualCanvasXMin - canvasPaddingX);
        float canvasXMax = Mathf.Min(CanvasWidth, VisualCanvasXMax + canvasPaddingX);
        float canvasYMin = Mathf.Max(0f, VisualCanvasYMin - canvasPaddingY);
        float canvasYMax = Mathf.Min(CanvasHeight, VisualCanvasYMax + canvasPaddingY);

        float sourceXMin = canvasXMin / CanvasWidth * width;
        float sourceXMax = canvasXMax / CanvasWidth * width;
        float sourceYMin = (CanvasHeight - canvasYMax) / CanvasHeight * height;
        float sourceYMax = (CanvasHeight - canvasYMin) / CanvasHeight * height;

        return Rect.MinMaxRect(sourceXMin, sourceYMin, sourceXMax, sourceYMax);
    }

    private static Rect ExpandPaintedBounds(RectInt paintedBounds, int width, int height, Vector2 paddingRatio)
    {
        float safePaddingX = Mathf.Clamp(paddingRatio.x, 0f, 0.45f);
        float safePaddingY = Mathf.Clamp(paddingRatio.y, 0f, 0.45f);
        float paddingX = Mathf.Max(1f, paintedBounds.width * safePaddingX);
        float paddingY = Mathf.Max(1f, paintedBounds.height * safePaddingY);

        float sourceXMin = Mathf.Max(0f, paintedBounds.xMin - paddingX);
        float sourceXMax = Mathf.Min(width - 1f, paintedBounds.xMax - 1f + paddingX);
        float sourceYMin = Mathf.Max(0f, paintedBounds.yMin - paddingY);
        float sourceYMax = Mathf.Min(height - 1f, paintedBounds.yMax - 1f + paddingY);

        return Rect.MinMaxRect(sourceXMin, sourceYMin, sourceXMax, sourceYMax);
    }

    public static Texture2D CreateModelTexture(Texture2D source, int textureSize, float alphaThreshold)
    {
        if (source == null)
        {
            return null;
        }

        int outputSize = Mathf.Clamp(textureSize, 64, 2048);
        Color32[] sourcePixels = source.GetPixels32();
        if (!TryFindPaintedBounds(sourcePixels, source.width, source.height, alphaThreshold, out RectInt paintedBounds))
        {
            return CreateSolidTexture(source.name, outputSize, outputSize, Color.white, "ModelMappedFallback");
        }

        Color32 fallbackColor = AveragePaintedColor(sourcePixels, alphaThreshold);
        Color32[] columnColors = BuildColumnColors(sourcePixels, source.width, paintedBounds, fallbackColor, alphaThreshold);
        Color32[] outputPixels = new Color32[outputSize * outputSize];

        for (int y = 0; y < outputSize; y++)
        {
            float v = outputSize <= 1 ? 0f : y / (float)(outputSize - 1);
            int sourceY = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(paintedBounds.yMin, paintedBounds.yMax - 1, v)),
                0,
                source.height - 1
            );

            for (int x = 0; x < outputSize; x++)
            {
                float u = outputSize <= 1 ? 0f : x / (float)(outputSize - 1);
                int sourceX = Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Lerp(paintedBounds.xMin, paintedBounds.xMax - 1, u)),
                    0,
                    source.width - 1
                );

                outputPixels[y * outputSize + x] = SampleFilledColor(
                    sourcePixels,
                    source.width,
                    sourceX,
                    sourceY,
                    columnColors[sourceX],
                    alphaThreshold
                );
            }
        }

        Texture2D texture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, true)
        {
            name = $"{source.name}_ModelMapped",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(outputPixels);
        texture.Apply(true, false);
        return texture;
    }

    private static Color32 SampleFilledColor(
        Color32[] pixels,
        int width,
        int x,
        int y,
        Color32 fallbackColor,
        float alphaThreshold
    )
    {
        Color32 color = pixels[y * width + x];
        if (!IsPaintedPixel(color, alphaThreshold))
        {
            color = fallbackColor;
        }
        return color;
    }

    private static bool TryFindPaintedBounds(Color32[] pixels, int width, int height, float alphaThreshold, out RectInt bounds)
    {
        if (TryFindAlphaBounds(pixels, width, height, alphaThreshold, out RectInt alphaBounds)
            && (alphaBounds.width < width || alphaBounds.height < height))
        {
            bounds = alphaBounds;
            return true;
        }

        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        byte alphaByteThreshold = AlphaByteThreshold(alphaThreshold);

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (!IsPaintedPixel(pixels[row + x], alphaByteThreshold))
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = new RectInt(0, 0, width, height);
            return false;
        }

        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private static bool TryFindAlphaBounds(Color32[] pixels, int width, int height, float alphaThreshold, out RectInt bounds)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        byte alphaByteThreshold = AlphaByteThreshold(alphaThreshold);

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x].a < alphaByteThreshold)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = new RectInt(0, 0, width, height);
            return false;
        }

        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private static Color32 AveragePaintedColor(Color32[] pixels, float alphaThreshold)
    {
        byte alphaByteThreshold = AlphaByteThreshold(alphaThreshold);
        long r = 0;
        long g = 0;
        long b = 0;
        int count = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 color = pixels[i];
            if (!IsPaintedPixel(color, alphaByteThreshold))
            {
                continue;
            }

            r += color.r;
            g += color.g;
            b += color.b;
            count++;
        }

        if (count == 0)
        {
            return Color.white;
        }

        return new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
    }

    private static Color32[] BuildColumnColors(
        Color32[] pixels,
        int width,
        RectInt bounds,
        Color32 fallbackColor,
        float alphaThreshold
    )
    {
        Color32[] colors = new Color32[width];
        bool[] hasColor = new bool[width];
        byte alphaByteThreshold = AlphaByteThreshold(alphaThreshold);

        for (int x = 0; x < width; x++)
        {
            long r = 0;
            long g = 0;
            long b = 0;
            int count = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Color32 color = pixels[y * width + x];
                if (!IsPaintedPixel(color, alphaByteThreshold))
                {
                    continue;
                }

                r += color.r;
                g += color.g;
                b += color.b;
                count++;
            }

            colors[x] = count > 0
                ? new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255)
                : fallbackColor;
            hasColor[x] = count > 0;
        }

        FillEmptyColumnColors(colors, hasColor, fallbackColor);
        return colors;
    }

    private static void FillEmptyColumnColors(Color32[] colors, bool[] hasColor, Color32 fallbackColor)
    {
        int lastPainted = -1;
        int[] nearestLeft = new int[colors.Length];
        int[] nearestRight = new int[colors.Length];

        for (int x = 0; x < colors.Length; x++)
        {
            if (hasColor[x])
            {
                lastPainted = x;
            }

            nearestLeft[x] = lastPainted;
        }

        lastPainted = -1;
        for (int x = colors.Length - 1; x >= 0; x--)
        {
            if (hasColor[x])
            {
                lastPainted = x;
            }

            nearestRight[x] = lastPainted;
        }

        for (int x = 0; x < colors.Length; x++)
        {
            if (hasColor[x])
            {
                continue;
            }

            int left = nearestLeft[x];
            int right = nearestRight[x];
            if (left < 0 && right < 0)
            {
                colors[x] = fallbackColor;
            }
            else if (left < 0)
            {
                colors[x] = colors[right];
            }
            else if (right < 0)
            {
                colors[x] = colors[left];
            }
            else
            {
                colors[x] = x - left <= right - x ? colors[left] : colors[right];
            }
        }
    }

    private static byte AlphaByteThreshold(float alphaThreshold)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(alphaThreshold * 255f), 1, 255);
    }

    private static bool IsPaintedPixel(Color32 color, float alphaThreshold)
    {
        return IsPaintedPixel(color, AlphaByteThreshold(alphaThreshold));
    }

    private static bool IsPaintedPixel(Color32 color, byte alphaByteThreshold)
    {
        return color.a >= alphaByteThreshold && !LooksLikeCanvasBackground(color);
    }

    private static bool LooksLikeCanvasBackground(Color32 color)
    {
        int max = System.Math.Max(color.r, System.Math.Max(color.g, color.b));
        int min = System.Math.Min(color.r, System.Math.Min(color.g, color.b));
        int range = max - min;
        float luma = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;

        return (max >= 235 && range <= 46) || (luma >= 0.86f && range <= 32);
    }

    private static Texture2D CreateSolidTexture(string sourceName, int width, int height, Color32 color, string suffix)
    {
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = $"{sourceName}_{suffix}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(true, false);
        return texture;
    }

    public static bool ApplyGeneratedUvs(Renderer[] renderers, Transform projector, bool flipHorizontal)
    {
        if (renderers == null || projector == null || !TryCalculateProjectionBounds(renderers, projector, out Bounds bounds))
        {
            return false;
        }

        CreateProjectionFrame(bounds, flipHorizontal, out Vector3 origin, out Vector3 uVector, out Vector3 vVector);
        float uLengthSq = Mathf.Max(Vector3.Dot(uVector, uVector), 0.000001f);
        float vLengthSq = Mathf.Max(Vector3.Dot(vVector, vVector), 0.000001f);
        bool applied = false;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            Mesh mesh = CreateWritableMeshInstance(renderer);
            if (mesh == null)
            {
                continue;
            }

            Vector3[] vertices;
            try
            {
                vertices = mesh.vertices;
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"DrawingTextureMapper: mesh '{mesh.name}' is not readable, so generated drawing UVs could not be applied. {exception.Message}");
                continue;
            }

            Vector2[] uvs = new Vector2[vertices.Length];
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                Vector3 worldPoint = renderer.transform.TransformPoint(vertices[vertexIndex]);
                Vector3 projectorPoint = projector.InverseTransformPoint(worldPoint);
                Vector3 relative = projectorPoint - origin;
                float u = Vector3.Dot(relative, uVector) / uLengthSq;
                float v = Vector3.Dot(relative, vVector) / vLengthSq;
                uvs[vertexIndex] = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
            }

            mesh.uv = uvs;
            applied = true;
        }

        return applied;
    }

    private static Mesh CreateWritableMeshInstance(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned)
        {
            Mesh source = skinned.sharedMesh;
            if (!IsReadableSourceMesh(source))
            {
                return null;
            }

            if (source.name.EndsWith("_DrawingUV"))
            {
                return source;
            }

            Mesh copy = UnityEngine.Object.Instantiate(source);
            copy.name = $"{source.name}_DrawingUV";
            skinned.sharedMesh = copy;
            return copy;
        }

        MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
        if (meshFilter == null)
        {
            return null;
        }

        Mesh mesh = meshFilter.sharedMesh;
        if (!IsReadableSourceMesh(mesh))
        {
            return null;
        }

        if (mesh.name.EndsWith("_DrawingUV"))
        {
            return mesh;
        }

        Mesh meshCopy = UnityEngine.Object.Instantiate(mesh);
        meshCopy.name = $"{mesh.name}_DrawingUV";
        meshFilter.sharedMesh = meshCopy;
        return meshCopy;
    }

    private static bool IsReadableSourceMesh(Mesh mesh)
    {
        if (mesh == null)
        {
            return false;
        }

        if (mesh.name.EndsWith("_DrawingUV"))
        {
            return true;
        }

        if (mesh.isReadable)
        {
            return true;
        }

        Debug.LogWarning($"DrawingTextureMapper: mesh '{mesh.name}' is not readable. Enable Read/Write on the model import settings to use generated drawing UVs.");
        return false;
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

    private static void CreateProjectionFrame(
        Bounds bounds,
        bool flipHorizontal,
        out Vector3 origin,
        out Vector3 uVector,
        out Vector3 vVector
    )
    {
        Vector3 size = bounds.size;
        int upAxis = 1;
        int lengthAxis = LargestAxis(size, upAxis);
        int heightAxis = AxisValue(size, upAxis) >= AxisValue(size, lengthAxis) * 0.08f
            ? upAxis
            : LargestAxis(size, lengthAxis);
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
}
