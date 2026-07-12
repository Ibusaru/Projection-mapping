using UnityEngine;

/// <summary>
/// Shared layout for the single generated beach.  Water, sand, props, terrain and
/// cameras must derive their shoreline coordinates from this class so they cannot
/// drift into separate beach layouts.
/// </summary>
internal static class OceanShorelineLayout
{
    private const float ShoreNoiseScale = 0.018f;
    // The seabed begins rising before the visible wet/dry sand boundary. Keep
    // a short shallow shelf so the water reaches the first parasol row instead
    // of ending at the start of the terrain slope.
    private const float SandMaterialTransitionInland = 0.09f;
    public static readonly Vector2 ShowcaseHalfSize = new Vector2(54f, 46f);

    public static float ActiveHalfX(Vector2 oceanSize, Vector2 activeAreaSize)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        return Mathf.Min(Mathf.Abs(activeAreaSize.x) * 0.5f, Mathf.Max(1f, halfX - 3f));
    }

    public static float UsableWidth(Vector2 oceanSize, Vector2 activeAreaSize, float requestedWidth)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        return Mathf.Clamp(requestedWidth, 3f, Mathf.Max(3f, halfX - ActiveHalfX(oceanSize, activeAreaSize) - 2f));
    }

    public static float StartX(Vector2 oceanSize, Vector2 activeAreaSize, float requestedWidth, int seed, float z)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        float halfZ = Mathf.Max(1f, oceanSize.y * 0.5f);
        float activeHalfX = ActiveHalfX(oceanSize, activeAreaSize);
        float usableWidth = UsableWidth(oceanSize, activeAreaSize, requestedWidth);
        float normalizedZ = z / halfZ;
        float inlet = Mathf.Sin(normalizedZ * Mathf.PI * 2.7f + seed * 0.017f) * usableWidth * 0.08f;
        float grain = (Mathf.PerlinNoise(seed * 0.013f, z * ShoreNoiseScale + 19.7f) - 0.5f) * usableWidth * 0.16f;
        float minimumStart = activeHalfX + 10f;
        float maximumStart = Mathf.Max(minimumStart, halfX - Mathf.Max(52f, usableWidth * 0.78f));
        return Mathf.Clamp(halfX - usableWidth + inlet + grain, minimumStart, maximumStart);
    }

    public static float SandMaterialTransitionX(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        float z)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        float shoreX = StartX(oceanSize, activeAreaSize, requestedWidth, seed, z);
        float dryInset = Mathf.Max(7f, (halfX - shoreX) * 0.055f);
        float dryStart = Mathf.Min(halfX - 7f, shoreX + dryInset);
        float dryEnd = Mathf.Max(dryStart, halfX - 7f);
        return Mathf.Lerp(dryStart, dryEnd, SandMaterialTransitionInland);
    }

    public static float SampleLandHeight(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        float shorelineSurfaceY,
        float x,
        float z)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        float shoreX = StartX(oceanSize, activeAreaSize, requestedWidth, seed, z);
        float inland = Mathf.Clamp01(Mathf.InverseLerp(shoreX, halfX, x));
        if (inland <= 0.0001f)
        {
            return shorelineSurfaceY;
        }

        float inlandRise = Smooth01(Mathf.InverseLerp(0.12f, 0.78f, inland));
        float dune = Mathf.Sin(z * 0.048f + inland * 2.4f + seed * 0.004f) * 0.16f;
        float grain = (Mathf.PerlinNoise(x * 0.052f + seed * 0.01f, z * 0.052f) - 0.5f) * 0.4f;
        float height = Mathf.Lerp(0.06f, 1.42f, inlandRise) + (dune + grain) * Mathf.Lerp(0.18f, 1f, inlandRise);
        return shorelineSurfaceY + Mathf.Max(0.035f, height);
    }

    public static Vector3 SampleDryBeachPoint(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        float shorelineSurfaceY,
        float inland,
        float alongShore)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        float halfZ = Mathf.Max(1f, oceanSize.y * 0.5f);
        float z = Mathf.Clamp(alongShore, -halfZ * 0.92f, halfZ * 0.92f);
        float shoreX = StartX(oceanSize, activeAreaSize, requestedWidth, seed, z);
        float dryInset = Mathf.Max(7f, (halfX - shoreX) * 0.055f);
        float dryEnd = halfX - 7f;
        float x = Mathf.Lerp(shoreX + dryInset, dryEnd, Mathf.Clamp01(inland));
        return new Vector3(x, SampleLandHeight(oceanSize, activeAreaSize, requestedWidth, seed, shorelineSurfaceY, x, z), z);
    }

    public static bool IsDryBeachPoint(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        float shorelineSurfaceY,
        Vector3 point,
        float shorelineSafety)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        float halfZ = Mathf.Max(1f, oceanSize.y * 0.5f);
        if (Mathf.Abs(point.z) > halfZ * 0.94f || point.x > halfX - 3f)
        {
            return false;
        }

        float shoreX = SandMaterialTransitionX(oceanSize, activeAreaSize, requestedWidth, seed, point.z);
        float surface = SampleLandHeight(oceanSize, activeAreaSize, requestedWidth, seed, shorelineSurfaceY, point.x, point.z);
        return point.x >= shoreX + Mathf.Max(0f, shorelineSafety)
            && point.y >= shorelineSurfaceY + 0.03f
            && Mathf.Abs(point.y - surface) <= 1.2f;
    }

    public static bool IsUnderwaterPoint(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        Vector3 point,
        float shorelineSafety)
    {
        float halfX = Mathf.Max(1f, oceanSize.x * 0.5f);
        float halfZ = Mathf.Max(1f, oceanSize.y * 0.5f);
        if (Mathf.Abs(point.x) > halfX * 0.98f || Mathf.Abs(point.z) > halfZ * 0.98f)
        {
            return false;
        }

        float shoreX = SandMaterialTransitionX(oceanSize, activeAreaSize, requestedWidth, seed, point.z);
        return point.x <= shoreX - Mathf.Max(0f, shorelineSafety);
    }

    public static Vector3 ShowcaseCenter(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        float shorelineSurfaceY)
    {
        return SampleDryBeachPoint(oceanSize, activeAreaSize, requestedWidth, seed, shorelineSurfaceY, 0.5f, 0f);
    }

    public static bool IsInsideShowcaseZone(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        float shorelineSurfaceY,
        Vector3 point)
    {
        Vector3 center = ShowcaseCenter(oceanSize, activeAreaSize, requestedWidth, seed, shorelineSurfaceY);
        return Mathf.Abs(point.x - center.x) <= ShowcaseHalfSize.x
            && Mathf.Abs(point.z - center.z) <= ShowcaseHalfSize.y;
    }

    public static Vector3 WaterInterestPoint(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float requestedWidth,
        int seed,
        float waterSurfaceY)
    {
        float shoreX = StartX(oceanSize, activeAreaSize, requestedWidth, seed, 0f);
        // Keep the water anchor close enough to the active shore that both
        // story elements fit in the aerial composition.  The full ocean still
        // extends far beyond this interest point; it is only a camera framing
        // anchor, not a world-size limit.
        return new Vector3(shoreX - 46f, waterSurfaceY, 0f);
    }

    public static Vector3 Tangent(Vector2 oceanSize, Vector2 activeAreaSize, float requestedWidth, int seed, float z)
    {
        const float step = 1f;
        float before = StartX(oceanSize, activeAreaSize, requestedWidth, seed, z - step);
        float after = StartX(oceanSize, activeAreaSize, requestedWidth, seed, z + step);
        Vector3 tangent = new Vector3(after - before, 0f, step * 2f);
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }
}
