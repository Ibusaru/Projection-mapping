using UnityEngine;

/// <summary>
/// Aerial route framed around the active water/beach story, never the complete
/// generated ocean bounds.
/// </summary>
internal static class OceanDroneOverview
{
    public static bool TryEvaluate(
        OceanEnvironment environment,
        float normalizedTime,
        out Vector3 position,
        out Vector3 lookTarget)
    {
        return TryEvaluate(environment, normalizedTime, 0f, out position, out lookTarget);
    }

    public static bool TryEvaluate(
        OceanEnvironment environment,
        float normalizedTime,
        float routeVariant,
        out Vector3 position,
        out Vector3 lookTarget)
    {
        position = Vector3.zero;
        lookTarget = Vector3.zero;
        if (environment == null)
        {
            return false;
        }

        environment.GetDroneInterestPoints(out Vector3 beachCenter, out Vector3 waterInterest);
        float t = Smooth01(normalizedTime);
        Vector3 shoreward = beachCenter - waterInterest;
        shoreward.y = 0f;
        shoreward = shoreward.sqrMagnitude > 0.001f ? shoreward.normalized : Vector3.right;
        Vector3 lateral = Vector3.Cross(Vector3.up, shoreward).normalized;
        // Keep the shoreline, shallow water and the decorative showcase inside
        // one stable region of the frame.  The previous route sat almost on the
        // beach axis, which made the ground fill the image and clipped the shore
        // at the edges.  A shallow lateral orbit gives the shot a recognisable
        // drone perspective without changing the generated world.
        Vector3 roiCenter = Vector3.Lerp(waterInterest, beachCenter, 0.32f);
        roiCenter.y = environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY + 0.7f, 0f)).y;

        // Travel through a broad, shallow figure-eight instead of hovering on
        // one aerial tripod. The route stays on the ocean side of the beach so
        // the extended water backdrop continues to hide the finite world edge.
        float phaseOffset = Mathf.Sin(routeVariant * 0.731f) * 0.28f * Mathf.PI;
        float routeMirror = Mathf.Sin(routeVariant * 1.913f + 0.6f) >= 0f ? 1f : -1f;
        float phase = Mathf.Lerp(-0.72f, 0.82f, t) * Mathf.PI + phaseOffset;
        float lateralOffset = Mathf.Sin(phase) * 31f * routeMirror;
        float oceanOffset = 98f + Mathf.Cos(phase * 1.15f) * 14f;
        float heightVariation = Mathf.Sin(routeVariant * 1.271f) * 4f;
        float height = 73f + heightVariation + Mathf.Sin(phase * 0.8f + 0.45f) * 9f;
        position = roiCenter - shoreward * oceanOffset + lateral * lateralOffset + Vector3.up * height;
        // Aim slightly below the shoreline center so the opening frame gives
        // the sea more vertical weight and crops the finite end caps above
        // the frame instead of presenting them as hard corner edges.
        float shoreLookBlend = Mathf.Lerp(0.1f, 0.2f, 0.5f + 0.5f * Mathf.Sin(phase * 0.7f));
        lookTarget = Vector3.Lerp(waterInterest, beachCenter, shoreLookBlend)
            + lateral * Mathf.Sin(phase * 0.55f) * 5.5f * routeMirror
            // Keep the beach below the upper safe-frame edge at both ends of
            // the route while preserving plenty of water in the lower frame.
            + Vector3.up * 1.2f;
        return IsFinite(position) && IsFinite(lookTarget);
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
