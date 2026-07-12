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
        float entrySeconds)
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
            // Quintic ease-in/out gives zero velocity at both ends. The
            // 4t(1-t) term is an explicit parabolic arc, keeping forward
            // movement visible while the camera descends through the surface.
            float arcHeight = Mathf.Clamp(Vector3.Distance(startPosition, endPosition) * 0.09f, 6f, 18f);
            float parabola = 4f * eased * (1f - eased);
            position = Vector3.Lerp(startPosition, endPosition, eased) + Vector3.up * (arcHeight * parabola);

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
            Vector3 midpoint = Vector3.Lerp(startPosition, endPosition, 0.5f);
            float arcHeight = Mathf.Clamp(Vector3.Distance(startPosition, endPosition) * 0.12f, 4f, 30f);
            midpoint += Vector3.up * arcHeight;
            position = QuadraticBezier(startPosition, midpoint, endPosition, eased);
            lookTarget = Vector3.Lerp(startLookTarget, endLookTarget, eased);
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

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
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
