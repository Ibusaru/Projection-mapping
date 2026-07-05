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
}
