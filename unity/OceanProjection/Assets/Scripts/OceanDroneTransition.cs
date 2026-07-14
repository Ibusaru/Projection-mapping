using UnityEngine;

internal enum OceanDroneTourState
{
    Underwater,
    Surfacing,
    DroneOverview,
    WaterEntry,
    UnderwaterBlend
}

/// <summary>
/// Owns the camera state between the aerial drone route and the underwater
/// route.  The entry is a single continuous move through the water surface;
/// there is deliberately no black fade or scene-like cut.
/// </summary>
internal sealed class OceanDroneTransition
{
    private OceanDroneTourState state = OceanDroneTourState.Underwater;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 startLookTarget;
    private Vector3 endLookTarget;
    private float startedAt;
    private float duration;
    private float waterSurfaceY;

    public OceanDroneTourState State => state;
    public bool IsActive => state == OceanDroneTourState.Surfacing
        || state == OceanDroneTourState.WaterEntry
        || state == OceanDroneTourState.UnderwaterBlend;
    public float FadeAlpha { get; private set; }

    public void EnterDroneOverview()
    {
        state = OceanDroneTourState.DroneOverview;
        FadeAlpha = 0f;
    }

    public void EnterUnderwater()
    {
        state = OceanDroneTourState.Underwater;
        FadeAlpha = 0f;
    }

    public void Begin(
        OceanDroneTourState transitionState,
        Vector3 fromPosition,
        Vector3 fromLookTarget,
        Vector3 toPosition,
        Vector3 toLookTarget,
        float startedAt,
        float duration)
    {
        state = transitionState;
        startPosition = fromPosition;
        endPosition = toPosition;
        startLookTarget = fromLookTarget;
        endLookTarget = toLookTarget;
        this.startedAt = startedAt;
        this.duration = Mathf.Clamp(duration, 2.5f, 5f);
        FadeAlpha = 0f;
    }

    public void BeginWaterEntry(
        Vector3 aerialPosition,
        Vector3 aerialLookTarget,
        Vector3 underwaterPosition,
        Vector3 underwaterLookTarget,
        float startedAt,
        float entrySeconds,
        float waterSurfaceY)
    {
        state = OceanDroneTourState.WaterEntry;
        startPosition = aerialPosition;
        startLookTarget = aerialLookTarget;
        endPosition = underwaterPosition;
        Vector3 levelDirection = underwaterLookTarget - underwaterPosition;
        levelDirection.y = 0f;
        if (levelDirection.sqrMagnitude < 0.001f)
        {
            levelDirection = underwaterPosition - aerialPosition;
            levelDirection.y = 0f;
        }

        levelDirection = levelDirection.sqrMagnitude > 0.001f ? levelDirection.normalized : Vector3.forward;
        endLookTarget = underwaterPosition + levelDirection * 14f;
        this.startedAt = startedAt;
        duration = Mathf.Clamp(entrySeconds, 3f, 6f);
        this.waterSurfaceY = waterSurfaceY;
        FadeAlpha = 0f;
    }

    public bool TryEvaluate(float now, out Vector3 position, out Vector3 lookTarget, out float progress)
    {
        progress = duration <= 0f ? 1f : Mathf.Clamp01((now - startedAt) / duration);
        float eased = Smoother01(progress);
        if (state == OceanDroneTourState.UnderwaterBlend)
        {
            position = endPosition;
            lookTarget = endLookTarget;
            FadeAlpha = 0f;
            if (progress >= 1f)
            {
                state = OceanDroneTourState.Underwater;
            }

            return true;
        }

        if (state == OceanDroneTourState.WaterEntry)
        {
            // Descend monotonically through the surface. The old parabolic
            // lift climbed even higher before diving, which exposed the finite
            // coastline and made a manually requested return feel backwards.
            Vector3 controlA = Vector3.Lerp(startPosition, endPosition, 0.32f);
            Vector3 controlB = Vector3.Lerp(startPosition, endPosition, 0.68f);
            controlA.y = Mathf.Lerp(
                startPosition.y,
                Mathf.Min(startPosition.y, waterSurfaceY + 4f),
                0.56f
            );
            controlB.y = Mathf.Clamp(waterSurfaceY + 0.12f, endPosition.y, controlA.y);
            position = CubicBezier(startPosition, controlA, controlB, endPosition, eased);

            Vector3 startView = startLookTarget - startPosition;
            Vector3 levelView = endLookTarget - endPosition;
            Vector3 startDirection = startView.sqrMagnitude > 0.001f ? startView.normalized : levelView.normalized;
            Vector3 levelDirection = levelView.sqrMagnitude > 0.001f ? levelView.normalized : Vector3.forward;
            levelDirection.y = 0f;
            levelDirection.Normalize();
            float levelProgress = Smoother01(Mathf.InverseLerp(0.08f, 0.72f, progress));
            Vector3 viewDirection = Vector3.Slerp(startDirection, levelDirection, levelProgress).normalized;
            float lookDistance = Mathf.Lerp(Mathf.Max(8f, startView.magnitude), 14f, eased);
            lookTarget = position + viewDirection * lookDistance;
        }
        else
        {
            // Surfacing is also monotonic. Blend view directions around the
            // moving camera instead of interpolating two distant world points;
            // this avoids sweeping across the side caps of the generated map.
            position = Vector3.Lerp(startPosition, endPosition, eased);
            Vector3 startView = startLookTarget - startPosition;
            Vector3 endView = endLookTarget - endPosition;
            Vector3 startDirection = startView.sqrMagnitude > 0.001f ? startView.normalized : Vector3.forward;
            Vector3 endDirection = endView.sqrMagnitude > 0.001f ? endView.normalized : startDirection;
            Vector3 viewDirection = Vector3.Slerp(startDirection, endDirection, Smooth01(progress)).normalized;
            float lookDistance = Mathf.Lerp(Mathf.Max(8f, startView.magnitude), Mathf.Max(12f, endView.magnitude), eased);
            lookTarget = position + viewDirection * lookDistance;
        }
        FadeAlpha = 0f;

        if (progress >= 1f)
        {
            if (state == OceanDroneTourState.WaterEntry)
            {
                // Hold the underwater end for one short blend interval so the
                // water surface optical change can settle without a cut.
                state = OceanDroneTourState.UnderwaterBlend;
                startedAt = now;
                duration = 0.35f;
                progress = 0f;
            }
            else
            {
                state = OceanDroneTourState.DroneOverview;
            }
        }

        return true;
    }

    public static float FovForState(float defaultFov, float droneFov, OceanDroneTourState state, float progress)
    {
        float cappedDroneFov = Mathf.Min(droneFov, defaultFov + 12f);
        if (state == OceanDroneTourState.DroneOverview)
        {
            return cappedDroneFov;
        }

        if (state == OceanDroneTourState.Surfacing)
        {
            return Mathf.Lerp(defaultFov, cappedDroneFov, Smooth01(progress));
        }

        if (state == OceanDroneTourState.WaterEntry)
        {
            return Mathf.Lerp(cappedDroneFov, defaultFov, Smoother01(progress));
        }

        if (state == OceanDroneTourState.UnderwaterBlend)
        {
            return defaultFov;
        }

        return defaultFov;
    }

    private static Vector3 CubicBezier(Vector3 start, Vector3 controlA, Vector3 controlB, Vector3 end, float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return inverse * inverse * inverse * start
            + 3f * inverse * inverse * t * controlA
            + 3f * inverse * t * t * controlB
            + t * t * t * end;
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private static float Smoother01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
