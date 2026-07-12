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

        // Keep the ocean dominant and crop the finite rear/side land edges.
        // The beach remains a secondary band in the upper part of the frame.
        float height = 78f + Mathf.Sin(t * Mathf.PI) * 2f;
        // Stay offset from the shoreline normal.  Passing directly over the
        // normal puts the near-water anchor below the lower frame edge.
        // Keep the shoreline centered in the opening frame. A large fixed
        // side offset made the finite shoreline caps read like clipped map
        // edges at the upper corners, especially on a 16:9 Game view.
        float lateralOffset = Mathf.Lerp(-8f, 8f, t);
        position = roiCenter - shoreward * 96f + lateral * lateralOffset + Vector3.up * height;
        // Aim slightly below the shoreline center so the opening frame gives
        // the sea more vertical weight and crops the finite end caps above
        // the frame instead of presenting them as hard corner edges.
        lookTarget = Vector3.Lerp(waterInterest, beachCenter, Mathf.Lerp(0.11f, 0.17f, t)) + Vector3.down * 1.8f;
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
