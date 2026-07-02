using UnityEngine;

public static class DrawingTextureMapper
{
    public static Texture2D CreateProjectionTexture(Texture2D source, float alphaThreshold)
    {
        if (source == null)
        {
            return null;
        }

        Color32[] sourcePixels = source.GetPixels32();
        Color32[] outputPixels = new Color32[sourcePixels.Length];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Color32 color = sourcePixels[i];
            outputPixels[i] = color.a / 255f < alphaThreshold ? Transparent : color;
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
        if (color.a / 255f < alphaThreshold)
        {
            color = fallbackColor;
        }
        return color;
    }

    private static bool TryFindPaintedBounds(Color32[] pixels, int width, int height, float alphaThreshold, out RectInt bounds)
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
            if (color.a < alphaByteThreshold)
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
                if (color.a < alphaByteThreshold)
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
