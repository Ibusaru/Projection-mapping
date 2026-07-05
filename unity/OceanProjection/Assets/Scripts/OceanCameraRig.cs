using System.Collections.Generic;
using UnityEngine;

public class OceanCameraRig : MonoBehaviour
{
    private const float ReleasedFishFocusDistanceFloor = 3f;
    private const float ReleasedFishApproachDistanceFloor = 3.15f;
    private const float ReleasedFishRadiusMultiplierFloor = 0.95f;
    private const float MinimumUnderwaterSurfaceDepth = 1.25f;

    private enum DiverIntent
    {
        Cruise,
        ApproachFish,
        ApproachSchool,
        DriftPast
    }

    [SerializeField] private Transform target;
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 orbitSize = new Vector3(10f, 3f, 8f);
    [SerializeField] private float orbitSeconds = 38f;
    [SerializeField] private float lookAhead = 2.5f;

    [Header("Cinematic Auto Tour")]
    [SerializeField] private bool useCinematicShots = true;
    [SerializeField] private OceanEnvironment oceanEnvironment;
    [SerializeField] private Vector2 cinematicShotSeconds = new Vector2(10f, 17f);
    [SerializeField] private Vector2 cinematicDroneShotSeconds = new Vector2(7f, 12f);
    [SerializeField] private Vector2 cinematicUnderwaterShotSeconds = new Vector2(18f, 28f);
    [SerializeField] private Vector2 cinematicFishFocusShotSeconds = new Vector2(24f, 38f);
    [SerializeField] private float cinematicSmoothTime = 3.2f;
    [SerializeField] private float cinematicRotationSmooth = 1.35f;
    [SerializeField] private float cinematicMaxSpeed = 22f;
    [SerializeField] private float cinematicUnderwaterMaxSpeed = 5.2f;
    [SerializeField] private float cinematicDroneHeightScale = 0.28f;
    [SerializeField] private float cinematicSurfaceHeight = 1.35f;

    [Header("Diver Follow")]
    [SerializeField] private bool focusFish = true;
    [SerializeField] private float targetRefreshSeconds = 5f;
    [SerializeField] private bool prioritizeReleasedFish = true;
    [SerializeField] private float followDistance = 3.5f;
    [SerializeField] private float sideOffset = 0.48f;
    [SerializeField] private float heightOffset = 0.55f;
    [SerializeField] private float forwardLookAhead = 2.4f;
    [SerializeField] private float minFocusDistance = 3.2f;
    [SerializeField] private float maxFocusDistance = 8.5f;
    [SerializeField] private float focusRadiusMultiplier = 3f;
    [SerializeField] private float releasedFishApproachDistance = 3.15f;
    [SerializeField] private float releasedFishFocusRadiusMultiplier = 0.95f;
    [SerializeField] private float minReleasedFishFocusDistance = 3f;
    [SerializeField] private float positionSmoothTime = 2.8f;
    [SerializeField] private float rotationSmooth = 1.45f;
    [SerializeField] private float driftAmplitude = 0.34f;
    [SerializeField] private float driftSpeed = 0.24f;
    [SerializeField] private float maximumCameraSpeed = 5.8f;
    [SerializeField] private float focusTransitionSeconds = 1.45f;
    [SerializeField] private float focusTransitionMaxSpeedMultiplier = 1.35f;
    [SerializeField] private float surfaceDiveSmoothTime = 0.75f;
    [SerializeField] private float surfaceDiveMaxSpeed = 14f;
    [SerializeField] private float surfaceDiveDiagonalOffset = 5.5f;
    [SerializeField] private float surfaceDiveDepthTrigger = 0.9f;

    [Header("Diver Terrain Avoidance")]
    [SerializeField] private bool avoidSeabed = true;
    [SerializeField] private float cameraSeabedClearance = 2.25f;
    [SerializeField] private float cameraSeabedProbeDistance = 2.6f;
    [SerializeField] private float cameraSeabedSideStep = 0.9f;

    [Header("Diver Behavior")]
    [SerializeField] private float scanIntervalSeconds = 2.2f;
    [SerializeField] private float fishInterestDistance = 64f;
    [SerializeField] private float schoolInterestDistance = 72f;
    [SerializeField] private float schoolNeighborRadius = 6.5f;
    [SerializeField] private int minimumSchoolSize = 5;
    [SerializeField] private float fishApproachDistance = 4.1f;
    [SerializeField] private float schoolApproachDistance = 7.4f;
    [SerializeField] private Vector2 fishObserveSeconds = new Vector2(9f, 14f);
    [SerializeField] private Vector2 goodAngleFishObserveSeconds = new Vector2(12f, 18f);
    [SerializeField] private Vector2 schoolObserveSeconds = new Vector2(5.5f, 9f);
    [SerializeField] private float focusObserveWaitForTagSeconds = 18f;
    [SerializeField] private Vector2 cruiseSeconds = new Vector2(4f, 8f);
    [SerializeField] private float diverLookAhead = 1.8f;
    [SerializeField] private float schoolRadiusPadding = 1.35f;
    [SerializeField] private float relaxedCameraSpeed = 2.7f;
    [SerializeField] private float approachCameraSpeed = 4.4f;
    [SerializeField] private float maxContinuousFishFocusSeconds = 56f;
    [SerializeField] private int recentFishFocusStackSize = 6;
    [SerializeField] private float recentFishFocusPenalty = 10f;
    [SerializeField] private float currentFishRepeatPenalty = 18f;
    [Header("Diver Observation Angles")]
    [SerializeField] private Vector2 frontViewAngleRange = new Vector2(10f, 22f);
    [SerializeField] private Vector2 diagonalViewAngleRange = new Vector2(26f, 48f);
    [SerializeField] private Vector2 sideViewAngleRange = new Vector2(58f, 72f);
    [SerializeField] private Vector2 rearApproachTurnAngleRange = new Vector2(68f, 88f);
    [SerializeField] private float maximumObservationAngleFromFront = 68f;
    [SerializeField] private float goodObservationAngleDegrees = 52f;
    [SerializeField] private float observationOrbitSmoothTime = 1.8f;
    [SerializeField] private float observationOrbitMaxDegreesPerSecond = 85f;

    private FishActor focusedFish;
    private DiverIntent intent = DiverIntent.Cruise;
    private Vector3 positionVelocity;
    private Vector3 currentFocusPoint;
    private Vector3 currentFocusForward = Vector3.forward;
    private Vector3 cruiseDestination;
    private Vector3 lastSchoolCenter;
    private float currentFocusRadius = 1f;
    private float currentObservationAngleDegrees = 70f;
    private float targetObservationAngleDegrees = 70f;
    private float observationAngleVelocity;
    private float nextTargetRefreshTime;
    private float intentUntilTime;
    private bool hasObservationAngle;
    private bool hasPlacedInitialCamera;
    private bool focusTransitionActive;
    private bool hasLastLookTarget;
    private Vector3 focusTransitionStartPosition;
    private Vector3 focusTransitionStartLookTarget;
    private Vector3 lastLookTarget;
    private float focusTransitionStartedAt;
    private float focusTransitionDuration;
    private bool waitingForFocusedFishTag;
    private float pendingFishObserveSeconds;
    private float focusedFishSelectionStartedAt;
    private readonly List<FishActor> recentFishFocusStack = new List<FishActor>();
    private readonly Queue<FishActor> newFishFocusQueue = new Queue<FishActor>();
    private readonly Queue<FishActor> releasedFishFocusQueue = new Queue<FishActor>();
    private readonly HashSet<FishActor> knownReleasedFish = new HashSet<FishActor>();
    private readonly HashSet<FishActor> queuedNewFishFocus = new HashSet<FishActor>();
    private readonly HashSet<FishActor> queuedReleasedFishFocus = new HashSet<FishActor>();
    private readonly List<FishActor> focusQueuePruneBuffer = new List<FishActor>();
    private float focusedFishSinceTime;
    private static readonly OceanCinematicShotKind[] CinematicSequence =
    {
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.DroneOverview
    };
    private OceanCinematicShotKind currentCinematicShot = OceanCinematicShotKind.FishFocus;
    private float cinematicShotStartedAt;
    private float cinematicShotDuration = 12f;
    private int cinematicShotIndex = -1;
    private bool hasCinematicShot;

    private void OnDisable()
    {
        SetFocusedFish(null);
    }

    private void LateUpdate()
    {
        if (useCinematicShots)
        {
            UpdateCinematicCamera();
            return;
        }

        if (focusFish)
        {
            UpdateDiverFollowCamera();
            return;
        }

        float t = Time.time / Mathf.Max(1f, orbitSeconds) * Mathf.PI * 2f;
        Vector3 position = center + new Vector3(
            Mathf.Sin(t) * orbitSize.x,
            Mathf.Sin(t * 0.45f) * orbitSize.y,
            Mathf.Cos(t) * orbitSize.z
        );

        transform.position = position;

        Vector3 lookTarget = target != null
            ? target.position
            : center + new Vector3(Mathf.Sin(t + 0.8f) * lookAhead, 0f, Mathf.Cos(t + 0.8f) * lookAhead);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookTarget - transform.position),
            1.8f * Time.deltaTime
        );
    }

    public bool TryEvaluateCinematicShot(OceanCinematicShotKind shot, float normalizedTime, out Vector3 position, out Vector3 lookTarget)
    {
        return TryEvaluateCinematicShot(shot, normalizedTime, ResolveOceanEnvironment(), out position, out lookTarget);
    }

    private void UpdateCinematicCamera()
    {
        if (!hasCinematicShot || Time.time >= cinematicShotStartedAt + cinematicShotDuration)
        {
            BeginNextCinematicShot();
        }

        if (currentCinematicShot == OceanCinematicShotKind.FishFocus)
        {
            UpdateDiverFollowCamera();
            return;
        }

        float normalizedTime = Mathf.Clamp01((Time.time - cinematicShotStartedAt) / Mathf.Max(0.1f, cinematicShotDuration));
        if (!TryEvaluateCinematicShot(currentCinematicShot, normalizedTime, ResolveOceanEnvironment(), out Vector3 desiredPosition, out Vector3 lookTarget))
        {
            UpdateFallbackDiverCruise();
            return;
        }

        bool droneStyleShot = IsDroneStyleShot(currentCinematicShot);
        bool underwaterScenicShot = IsUnderwaterScenicShot(currentCinematicShot);
        float cinematicPositionSmoothTime = droneStyleShot
            ? cinematicSmoothTime * 0.56f
            : underwaterScenicShot
                ? Mathf.Max(positionSmoothTime * 1.18f, cinematicSmoothTime * 0.92f)
                : cinematicSmoothTime;
        float maximumSpeed = droneStyleShot
            ? cinematicMaxSpeed * 1.25f
            : underwaterScenicShot
                ? Mathf.Min(cinematicMaxSpeed, Mathf.Max(0.5f, cinematicUnderwaterMaxSpeed))
                : cinematicMaxSpeed;
        bool snapped = MoveCamera(
            desiredPosition,
            Mathf.Max(0.1f, cinematicPositionSmoothTime),
            Mathf.Max(0.1f, maximumSpeed)
        );

        float rotationSmooth = droneStyleShot
            ? cinematicRotationSmooth * 1.45f
            : underwaterScenicShot
                ? this.rotationSmooth * 0.9f
                : cinematicRotationSmooth;
        LookAtTarget(lookTarget, rotationSmooth, snapped);
    }

    private void BeginNextCinematicShot()
    {
        OceanEnvironment environment = ResolveOceanEnvironment();
        for (int attempt = 0; attempt < CinematicSequence.Length; attempt++)
        {
            cinematicShotIndex = (cinematicShotIndex + 1) % CinematicSequence.Length;
            OceanCinematicShotKind candidate = CinematicSequence[cinematicShotIndex];
            if (candidate == OceanCinematicShotKind.FishFocus && (!focusFish || !HasAnyActiveFish()))
            {
                continue;
            }

            if (candidate != OceanCinematicShotKind.FishFocus
                && !TryEvaluateCinematicShot(candidate, 0f, environment, out _, out _))
            {
                continue;
            }

            currentCinematicShot = candidate;
            cinematicShotStartedAt = Time.time;
            cinematicShotDuration = RandomDurationForShot(candidate);
            hasCinematicShot = true;
            if (candidate != OceanCinematicShotKind.FishFocus)
            {
                SetFocusedFish(null);
            }
            return;
        }

        currentCinematicShot = OceanCinematicShotKind.FishFocus;
        cinematicShotStartedAt = Time.time;
        cinematicShotDuration = RandomDuration(cinematicFishFocusShotSeconds, Mathf.Max(8f, cinematicShotSeconds.x));
        hasCinematicShot = true;
    }

    private bool TryEvaluateCinematicShot(
        OceanCinematicShotKind shot,
        float normalizedTime,
        OceanEnvironment environment,
        out Vector3 position,
        out Vector3 lookTarget)
    {
        float t = SmoothStep01(normalizedTime);
        float droneT = SmootherStep01(normalizedTime);
        Vector2 size = environment != null ? environment.OceanSize : new Vector2(Mathf.Max(1f, orbitSize.x * 4f), Mathf.Max(1f, orbitSize.z * 4f));
        float waterY = environment != null ? environment.WaterSurfaceY : center.y + orbitSize.y;
        Vector3 oceanCenter = OceanLocalToWorld(environment, new Vector3(0f, Mathf.Lerp(center.y, waterY - 3.5f, 0.45f), 0f));
        Vector3 reef = FeatureOrFallback(environment, OceanFeatureKind.Reef, new Vector3(size.x * 0.18f, waterY - 3f, size.y * 0.13f));
        Vector3 beach = FeatureOrFallback(environment, OceanFeatureKind.Beach, new Vector3(size.x * 0.38f, waterY - 0.6f, -size.y * 0.16f));
        Vector3 trench = FeatureOrFallback(environment, OceanFeatureKind.Trench, new Vector3(-size.x * 0.32f, waterY - 14f, size.y * 0.27f));
        Vector3 rock = FeatureOrFallback(environment, OceanFeatureKind.RockMountain, new Vector3(-size.x * 0.28f, waterY - 3f, -size.y * 0.28f));

        switch (shot)
        {
            case OceanCinematicShotKind.DroneOverview:
            {
                float maxSize = Mathf.Max(size.x, size.y);
                float height = waterY + maxSize * cinematicDroneHeightScale;
                Vector3 p0 = OceanLocalToWorld(environment, new Vector3(-size.x * 0.58f, height * 0.96f, -size.y * 0.54f));
                Vector3 p1 = OceanLocalToWorld(environment, new Vector3(-size.x * 0.45f, height, -size.y * 0.42f));
                Vector3 p2 = OceanLocalToWorld(environment, new Vector3(size.x * 0.2f, waterY + maxSize * cinematicDroneHeightScale * 0.78f, size.y * 0.18f));
                Vector3 p3 = OceanLocalToWorld(environment, new Vector3(size.x * 0.48f, waterY + maxSize * cinematicDroneHeightScale * 0.62f, size.y * 0.42f));
                position = CatmullRom(p0, p1, p2, p3, droneT);

                Vector3 lead = CatmullRom(p0, p1, p2, p3, Mathf.Clamp01(droneT + 0.12f));
                Vector3 featureSweep = Vector3.Lerp(rock, Vector3.Lerp(reef, trench, 0.45f), droneT);
                lookTarget = Vector3.Lerp(featureSweep, lead + Vector3.down * maxSize * 0.18f, 0.36f);
                lookTarget.y = Mathf.Lerp(waterY + 0.8f, waterY - 3.8f, Mathf.Clamp01(droneT + 0.08f));
                break;
            }
            case OceanCinematicShotKind.SurfaceSkim:
            {
                Vector3 p0 = beach + new Vector3(-size.x * 0.24f, waterY - 0.35f - beach.y, -size.y * 0.24f);
                Vector3 p1 = beach + new Vector3(-size.x * 0.1f, waterY - 0.65f - beach.y, -size.y * 0.16f);
                Vector3 p2 = reef + new Vector3(-size.x * 0.16f, waterY - 1.1f - reef.y, size.y * 0.08f);
                Vector3 p3 = reef + new Vector3(size.x * 0.05f, waterY - 1.35f - reef.y, size.y * 0.22f);
                position = CatmullRom(p0, p1, p2, p3, droneT);
                position.y = waterY - Mathf.Lerp(0.45f, Mathf.Max(0.75f, cinematicSurfaceHeight), droneT)
                    + Mathf.Sin(droneT * Mathf.PI * 2f) * 0.08f;

                Vector3 lead = CatmullRom(p0, p1, p2, p3, Mathf.Clamp01(droneT + 0.1f));
                lookTarget = Vector3.Lerp(lead, Vector3.Lerp(reef, oceanCenter, 0.42f), 0.34f);
                lookTarget.y = waterY - Mathf.Lerp(1.0f, 2.2f, droneT);
                break;
            }
            case OceanCinematicShotKind.ReefDive:
            {
                Vector3 p0 = SampleSwimPointWorld(environment, new Vector2(size.x * 0.04f, -size.y * 0.16f), waterY - 6.4f, 3.5f, 1.7f);
                Vector3 p1 = SampleSwimPointWorld(environment, new Vector2(size.x * 0.09f, -size.y * 0.08f), waterY - 6.8f, 3.4f, 2.0f);
                Vector3 p2 = SampleSwimPointWorld(environment, new Vector2(size.x * 0.12f, size.y * 0.02f), waterY - 7.4f, 3.2f, 2.4f);
                Vector3 p3 = SampleSwimPointWorld(environment, new Vector2(size.x * 0.07f, size.y * 0.1f), waterY - 7.8f, 3.1f, 2.8f);
                position = CatmullRom(p0, p1, p2, p3, t) + DiverDrift(Time.time * driftSpeed, 1f) * 0.28f;
                lookTarget = Vector3.Lerp(reef + Vector3.up * 1.2f, trench + Vector3.up * 2.8f, t * 0.28f);
                break;
            }
            case OceanCinematicShotKind.TrenchRun:
            {
                Vector3 start = SampleOceanFloorWorld(environment, new Vector2(-size.x * 0.44f, size.y * 0.36f), waterY - 12f) + Vector3.up * 3.5f;
                Vector3 midA = SampleOceanFloorWorld(environment, new Vector2(-size.x * 0.34f, size.y * 0.26f), waterY - 12f) + Vector3.up * 3.1f;
                Vector3 midB = SampleOceanFloorWorld(environment, new Vector2(-size.x * 0.22f, size.y * 0.16f), waterY - 12f) + Vector3.up * 3.2f;
                Vector3 end = SampleOceanFloorWorld(environment, new Vector2(-size.x * 0.1f, size.y * 0.06f), waterY - 12f) + Vector3.up * 3.4f;
                position = CatmullRom(start, midA, midB, end, t) + DiverDrift(Time.time * driftSpeed + 1.7f, 1f) * 0.18f;
                position.y = Mathf.Min(position.y, waterY - 5.5f);
                Vector3 lookAheadPoint = CatmullRom(start, midA, midB, end, Mathf.Clamp01(t + 0.16f));
                lookTarget = Vector3.Lerp(lookAheadPoint, trench + Vector3.up * 1.7f, 0.35f);
                break;
            }
            case OceanCinematicShotKind.RockMountainReveal:
            {
                Vector3 p0 = rock + new Vector3(-size.x * 0.18f, waterY - 4.8f - rock.y, -size.y * 0.18f);
                Vector3 p1 = rock + new Vector3(-size.x * 0.1f, waterY - 4.2f - rock.y, -size.y * 0.1f);
                Vector3 p2 = rock + new Vector3(size.x * 0.02f, waterY - 2.3f - rock.y, size.y * 0.02f);
                Vector3 p3 = rock + new Vector3(size.x * 0.14f, waterY - 3.2f - rock.y, size.y * 0.12f);
                position = CatmullRom(p0, p1, p2, p3, droneT) + DiverDrift(Time.time * driftSpeed + 2.3f, 1f) * 0.22f;
                lookTarget = rock + Vector3.up * Mathf.Lerp(0.7f, 2.2f, droneT);
                break;
            }
            case OceanCinematicShotKind.FishFocus:
                position = transform.position;
                lookTarget = transform.position + transform.forward * Mathf.Max(1f, lookAhead);
                break;
            default:
                position = transform.position;
                lookTarget = oceanCenter;
                return false;
        }

        return IsFinite(position) && IsFinite(lookTarget);
    }

    private OceanEnvironment ResolveOceanEnvironment()
    {
        if (oceanEnvironment == null)
        {
            oceanEnvironment = FindAnyObjectByType<OceanEnvironment>();
        }

        return oceanEnvironment;
    }

    private Vector3 FeatureOrFallback(OceanEnvironment environment, OceanFeatureKind feature, Vector3 fallbackLocal)
    {
        if (environment != null && environment.TryGetFeaturePoint(feature, out Vector3 point))
        {
            return point;
        }

        return OceanLocalToWorld(environment, fallbackLocal);
    }

    private Vector3 SampleOceanFloorWorld(OceanEnvironment environment, Vector2 localXZ, float fallbackY)
    {
        if (environment == null)
        {
            return new Vector3(center.x + localXZ.x, fallbackY, center.z + localXZ.y);
        }

        float y = environment.SampleSeabedHeight(localXZ.x, localXZ.y);
        return environment.transform.TransformPoint(new Vector3(localXZ.x, y, localXZ.y));
    }

    private Vector3 SampleSwimPointWorld(OceanEnvironment environment, Vector2 localXZ, float fallbackY, float floorClearance, float surfaceDepth)
    {
        Vector3 point = SampleOceanFloorWorld(environment, localXZ, fallbackY);
        point.y += Mathf.Max(0.5f, floorClearance);
        if (environment == null)
        {
            return point;
        }

        float waterY = SampleWaterSurfaceWorldY(environment);
        float underwaterY = waterY - Mathf.Max(0.4f, surfaceDepth);
        float floorY = SampleSeabedWorldY(environment, point);
        float minimumY = floorY + Mathf.Max(0.5f, Mathf.Min(floorClearance, cameraSeabedClearance + 0.4f));
        if (minimumY <= underwaterY)
        {
            point.y = Mathf.Clamp(point.y, minimumY, underwaterY);
        }
        else
        {
            point.y = minimumY;
        }

        return point;
    }

    private Vector3 OceanLocalToWorld(OceanEnvironment environment, Vector3 local)
    {
        return environment != null ? environment.transform.TransformPoint(local) : center + local;
    }

    private bool HasAnyActiveFish()
    {
        IReadOnlyList<FishActor> fishes = FishActor.AllActiveFishes;
        return fishes != null && fishes.Count > 0;
    }

    private float RandomDurationForShot(OceanCinematicShotKind shot)
    {
        if (shot == OceanCinematicShotKind.FishFocus)
        {
            return RandomDuration(cinematicFishFocusShotSeconds, Mathf.Max(8f, cinematicShotSeconds.x));
        }

        if (shot == OceanCinematicShotKind.DroneOverview)
        {
            return RandomDuration(cinematicDroneShotSeconds, Mathf.Max(5f, cinematicShotSeconds.x * 0.65f));
        }

        if (IsUnderwaterScenicShot(shot))
        {
            return RandomDuration(cinematicUnderwaterShotSeconds, Mathf.Max(12f, cinematicShotSeconds.y));
        }

        return RandomDuration(cinematicShotSeconds, 8f);
    }

    private static float RandomDuration(Vector2 range, float fallback)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        if (max <= 0f)
        {
            min = fallback;
            max = fallback;
        }

        min = Mathf.Max(2f, min);
        max = Mathf.Max(min, max);
        return Random.Range(min, max);
    }

    private static float SmoothStep01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private static float SmootherStep01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float value)
    {
        float t = Mathf.Clamp01(value);
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private static bool IsDroneStyleShot(OceanCinematicShotKind shot)
    {
        return shot == OceanCinematicShotKind.DroneOverview;
    }

    private static bool IsUnderwaterScenicShot(OceanCinematicShotKind shot)
    {
        return shot == OceanCinematicShotKind.ReefDive
            || shot == OceanCinematicShotKind.TrenchRun
            || shot == OceanCinematicShotKind.RockMountainReveal
            || shot == OceanCinematicShotKind.SurfaceSkim;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void UpdateDiverFollowCamera()
    {
        RefreshDiverIntentIfNeeded();

        if (intent == DiverIntent.Cruise || (focusedFish == null && intent != DiverIntent.ApproachSchool))
        {
            UpdateFallbackDiverCruise();
            return;
        }

        UpdateCurrentFocus();
        Vector3 focusForward = FlattenDirection(currentFocusForward);
        Vector3 focusRight = Vector3.Cross(Vector3.up, focusForward).normalized;
        if (focusRight.sqrMagnitude < 0.001f)
        {
            focusRight = transform.right;
        }
        Vector3 observationDirection = ObservationDirectionFromFocus(focusForward, focusRight);

        float driftTime = Time.time * driftSpeed;
        float desiredDistance = CalculateDesiredFocusDistance();
        float dynamicLookAhead = forwardLookAhead + diverLookAhead + currentFocusRadius * 0.45f;

        Vector3 breathingDrift = DiverDrift(driftTime, currentFocusRadius);
        Vector3 passOffset = intent == DiverIntent.DriftPast
            ? focusRight * Mathf.Sin(Time.time * 0.35f + SpawnTimeSeed()) * 2.2f
            : Vector3.zero;
        float observationSide = currentObservationAngleDegrees >= 0f ? 1f : -1f;

        Vector3 desiredPosition = currentFocusPoint
            + observationDirection * desiredDistance
            + focusRight * observationSide * (sideOffset + currentFocusRadius * 0.18f)
            + Vector3.up * (heightOffset + currentFocusRadius * 0.12f)
            + breathingDrift
            + passOffset;

        float speed = intent == DiverIntent.ApproachFish || intent == DiverIntent.ApproachSchool
            ? approachCameraSpeed
            : relaxedCameraSpeed;
        speed = Mathf.Min(speed, Mathf.Max(0.1f, maximumCameraSpeed));

        float trailingLookAheadBlend = Mathf.InverseLerp(105f, 170f, Mathf.Abs(currentObservationAngleDegrees));
        float observationLead = Mathf.Lerp(0.45f + currentFocusRadius * 0.12f, dynamicLookAhead, trailingLookAheadBlend);
        Vector3 lookTarget = currentFocusPoint
            + focusForward * observationLead
            + Vector3.up * Mathf.Sin(driftTime) * 0.18f;

        bool surfaceDive = TryShapeSurfaceDive(ref desiredPosition, focusForward);
        ApplyFocusTransition(ref desiredPosition, ref lookTarget);

        float moveSmoothTime = positionSmoothTime;
        if (surfaceDive)
        {
            moveSmoothTime = Mathf.Min(moveSmoothTime, Mathf.Max(0.05f, surfaceDiveSmoothTime));
            speed = Mathf.Max(speed, surfaceDiveMaxSpeed);
        }

        if (focusTransitionActive)
        {
            moveSmoothTime = Mathf.Min(moveSmoothTime, Mathf.Max(0.05f, focusTransitionDuration * 0.7f));
            speed *= Mathf.Max(1f, focusTransitionMaxSpeedMultiplier);
        }

        bool snapped = MoveCamera(desiredPosition, moveSmoothTime, speed);
        LookAtTarget(lookTarget, rotationSmooth, snapped);
        UpdateFocusedFishObservationTimer();
    }

    private void ApplyFocusTransition(ref Vector3 desiredPosition, ref Vector3 lookTarget)
    {
        if (!focusTransitionActive)
        {
            return;
        }

        float duration = Mathf.Max(0.05f, focusTransitionDuration);
        float normalizedTime = (Time.time - focusTransitionStartedAt) / duration;
        float eased = SmootherStep01(normalizedTime);
        desiredPosition = Vector3.Lerp(focusTransitionStartPosition, desiredPosition, eased);
        lookTarget = Vector3.Lerp(focusTransitionStartLookTarget, lookTarget, eased);

        if (normalizedTime >= 1f)
        {
            focusTransitionActive = false;
        }
    }

    private bool TryShapeSurfaceDive(ref Vector3 desiredPosition, Vector3 focusForward)
    {
        OceanEnvironment environment = ResolveOceanEnvironment();
        if (environment == null)
        {
            return false;
        }

        float waterY = SampleWaterSurfaceWorldY(environment);
        bool startsAtSurfaceOrAbove = transform.position.y > waterY - 0.15f;
        bool targetIsUnderwater = desiredPosition.y < waterY - Mathf.Max(0.2f, surfaceDiveDepthTrigger);
        if (!startsAtSurfaceOrAbove || !targetIsUnderwater)
        {
            return false;
        }

        Vector3 diagonalDirection = new Vector3(
            transform.position.x - desiredPosition.x,
            0f,
            transform.position.z - desiredPosition.z
        );
        if (diagonalDirection.sqrMagnitude < 0.001f)
        {
            diagonalDirection = -FlattenDirection(focusForward);
        }
        else
        {
            diagonalDirection.Normalize();
        }

        desiredPosition += diagonalDirection * Mathf.Max(0f, surfaceDiveDiagonalOffset);
        return true;
    }

    private void UpdateCurrentFocus()
    {
        if (intent == DiverIntent.ApproachSchool)
        {
            currentFocusPoint = lastSchoolCenter;
            return;
        }

        if (focusedFish == null)
        {
            return;
        }

        Transform fishTransform = focusedFish.transform;
        currentFocusPoint = focusedFish.VisualCenter;
        currentFocusForward = fishTransform.forward.sqrMagnitude > 0.001f
            ? fishTransform.forward.normalized
            : currentFocusForward;
        currentFocusRadius = focusedFish.CameraFocusRadius;
    }

    private float CalculateDesiredFocusDistance()
    {
        if (intent == DiverIntent.ApproachSchool)
        {
            return schoolApproachDistance + currentFocusRadius * schoolRadiusPadding;
        }

        if (focusedFish != null && focusedFish.IsReleasedFish)
        {
            float approachDistance = Mathf.Max(releasedFishApproachDistance, ReleasedFishApproachDistanceFloor);
            float radiusMultiplier = Mathf.Max(releasedFishFocusRadiusMultiplier, ReleasedFishRadiusMultiplierFloor);
            float minimumDistance = Mathf.Max(minReleasedFishFocusDistance, ReleasedFishFocusDistanceFloor);
            return Mathf.Clamp(
                approachDistance + currentFocusRadius * radiusMultiplier,
                minimumDistance,
                maxFocusDistance
            );
        }

        return Mathf.Clamp(fishApproachDistance + currentFocusRadius * focusRadiusMultiplier, minFocusDistance, maxFocusDistance);
    }

    private Vector3 ObservationDirectionFromFocus(Vector3 focusForward, Vector3 focusRight)
    {
        if (!hasObservationAngle)
        {
            ChooseObservationAngle(currentFocusPoint, focusForward);
        }

        currentObservationAngleDegrees = Mathf.SmoothDampAngle(
            currentObservationAngleDegrees,
            targetObservationAngleDegrees,
            ref observationAngleVelocity,
            observationOrbitSmoothTime,
            observationOrbitMaxDegreesPerSecond
        );
        currentObservationAngleDegrees = ClampObservationAngle(currentObservationAngleDegrees);

        float radians = currentObservationAngleDegrees * Mathf.Deg2Rad;
        Vector3 direction = focusForward * Mathf.Cos(radians) + focusRight * Mathf.Sin(radians);
        if (direction.sqrMagnitude < 0.001f)
        {
            return focusRight;
        }

        return direction.normalized;
    }

    private void ChooseObservationAngle(Vector3 focusPoint, Vector3 focusForward)
    {
        Vector3 flatForward = FlattenDirection(focusForward);
        Vector3 focusRight = Vector3.Cross(Vector3.up, flatForward).normalized;
        if (focusRight.sqrMagnitude < 0.001f)
        {
            focusRight = transform.right;
        }

        Vector3 toCamera = transform.position - focusPoint;
        Vector3 flatToCamera = new Vector3(toCamera.x, 0f, toCamera.z);
        bool hasCameraDirection = flatToCamera.sqrMagnitude > 0.001f;
        float side = 1f;
        if (hasCameraDirection)
        {
            flatToCamera.Normalize();
            side = Vector3.Dot(flatToCamera, focusRight) >= 0f ? 1f : -1f;
        }

        if (Random.value < 0.28f)
        {
            side *= -1f;
        }

        bool approachedFromBehind = hasCameraDirection && Vector3.Dot(flatToCamera, flatForward) < -0.25f;
        float angleMagnitude = approachedFromBehind
            ? PickBehindRecoveryAngleMagnitude()
            : PickObservationAngleMagnitude();

        targetObservationAngleDegrees = angleMagnitude * side;
        float startingAngle = hasCameraDirection
            ? Vector3.SignedAngle(flatForward, flatToCamera, Vector3.up)
            : targetObservationAngleDegrees;
        currentObservationAngleDegrees = Mathf.Abs(startingAngle) > maximumObservationAngleFromFront
            ? targetObservationAngleDegrees
            : ClampObservationAngle(startingAngle);
        observationAngleVelocity = 0f;
        hasObservationAngle = true;
    }

    private float PickObservationAngleMagnitude()
    {
        float roll = Random.value;
        if (focusedFish != null && focusedFish.IsReleasedFish)
        {
            if (roll < 0.2f)
            {
                return RandomInRange(frontViewAngleRange);
            }

            if (roll < 0.97f)
            {
                return RandomInRange(diagonalViewAngleRange);
            }

            return Mathf.Min(RandomInRange(sideViewAngleRange), maximumObservationAngleFromFront);
        }

        if (roll < 0.28f)
        {
            return RandomInRange(frontViewAngleRange);
        }

        if (roll < 0.9f)
        {
            return RandomInRange(diagonalViewAngleRange);
        }

        return Mathf.Min(RandomInRange(sideViewAngleRange), maximumObservationAngleFromFront);
    }

    private float PickBehindRecoveryAngleMagnitude()
    {
        if (focusedFish != null && focusedFish.IsReleasedFish)
        {
            return RandomInRange(diagonalViewAngleRange);
        }

        return Mathf.Min(RandomInRange(rearApproachTurnAngleRange), maximumObservationAngleFromFront);
    }

    private float ClampObservationAngle(float angle)
    {
        float limit = Mathf.Clamp(maximumObservationAngleFromFront, 25f, 89f);
        return Mathf.Clamp(angle, -limit, limit);
    }

    private static float RandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private Vector3 DiverDrift(float driftTime, float radius)
    {
        float scale = Mathf.Clamp(1f + radius * 0.08f, 1f, 1.35f);
        return new Vector3(
            Mathf.Sin(driftTime * 1.7f) * driftAmplitude * scale,
            Mathf.Sin(driftTime * 2.1f + 0.4f) * driftAmplitude * 0.55f,
            Mathf.Cos(driftTime * 1.3f) * driftAmplitude * scale
        );
    }

    private float SpawnTimeSeed()
    {
        return currentFocusPoint.x * 0.13f + currentFocusPoint.z * 0.17f;
    }

    private void UpdateFallbackDiverCruise()
    {
        if (Time.time >= intentUntilTime || cruiseDestination == Vector3.zero)
        {
            PickCruiseDestination();
        }

        float driftTime = Time.time * driftSpeed;
        Vector3 desiredPosition = cruiseDestination + DiverDrift(driftTime, 1f);

        bool snapped = MoveCamera(
            desiredPosition,
            positionSmoothTime * 1.1f,
            Mathf.Min(relaxedCameraSpeed, Mathf.Max(0.1f, maximumCameraSpeed))
        );

        Vector3 forward = FlattenDirection(cruiseDestination - transform.position);
        Vector3 lookTarget = target != null
            ? target.position
            : transform.position + forward * lookAhead + Vector3.up * Mathf.Sin(driftTime) * 0.2f;
        LookAtTarget(lookTarget, rotationSmooth * 0.9f, snapped);
    }

    private void PickCruiseDestination()
    {
        float t = Time.time / Mathf.Max(1f, orbitSeconds * 1.35f) * Mathf.PI * 2f;
        cruiseDestination = center + new Vector3(
            Mathf.Sin(t + Random.Range(-0.65f, 0.65f)) * orbitSize.x * 0.75f,
            Mathf.Sin(t * 0.37f + Random.Range(-0.4f, 0.4f)) * orbitSize.y * 0.55f,
            Mathf.Cos(t * 0.82f + Random.Range(-0.65f, 0.65f)) * orbitSize.z * 0.75f
        );
        cruiseDestination = ResolveCruiseDestination(cruiseDestination);
        intentUntilTime = Time.time + Random.Range(cruiseSeconds.x, cruiseSeconds.y);
    }

    private Vector3 ResolveCruiseDestination(Vector3 destination)
    {
        OceanEnvironment environment = ResolveOceanEnvironment();
        if (environment == null)
        {
            return destination;
        }

        float clearance = Mathf.Max(0.25f, cameraSeabedClearance);
        float floorY = SampleSeabedWorldY(environment, destination);
        float minimumY = floorY + clearance + 0.35f;
        float underwaterY = SampleWaterSurfaceWorldY(environment) - MinimumUnderwaterSurfaceDepth;
        if (minimumY <= underwaterY)
        {
            destination.y = Mathf.Clamp(destination.y, minimumY, underwaterY);
        }
        else
        {
            destination.y = minimumY;
        }

        return destination;
    }

    private void RefreshDiverIntentIfNeeded()
    {
        IReadOnlyList<FishActor> fishes = FishActor.AllActiveFishes;
        if (fishes == null || fishes.Count == 0)
        {
            ClearReleasedFishFocusQueues();
            SetFocusedFish(null);
            intent = DiverIntent.Cruise;
            nextTargetRefreshTime = Time.time + ScanDelay();
            return;
        }

        TrackReleasedFishFocusCandidates(fishes);

        if (ShouldHoldCurrentIntent())
        {
            return;
        }

        RequeueCompletedFocusedFish(fishes);

        FishActor fish = PickQueuedReleasedFish(fishes);
        bool selectedQueuedFish = fish != null;
        bool hasReleasedFish = HasReleasedFish(fishes);
        bool foundSchool = false;
        Vector3 schoolCenter = Vector3.zero;
        Vector3 schoolForward = transform.forward;
        float schoolRadius = 0f;
        int schoolSize = 0;

        if (!selectedQueuedFish)
        {
            bool releasedOnly = prioritizeReleasedFish && hasReleasedFish;
            foundSchool = TryFindSchool(fishes, out schoolCenter, out schoolForward, out schoolRadius, out schoolSize);
            fish = PickCinematicFish(fishes, releasedOnly);
            if (fish == focusedFish && HasExceededContinuousFishFocus())
            {
                fish = null;
            }
        }

        if (!selectedQueuedFish && !hasReleasedFish && foundSchool && (fish == null || schoolSize >= minimumSchoolSize + 2 || Random.value < 0.68f))
        {
            SetFocusedFish(null);
            intent = DiverIntent.ApproachSchool;
            lastSchoolCenter = schoolCenter;
            currentFocusPoint = schoolCenter;
            currentFocusForward = schoolForward;
            currentFocusRadius = Mathf.Max(1.5f, schoolRadius);
            ChooseObservationAngle(currentFocusPoint, currentFocusForward);
            intentUntilTime = Time.time + Random.Range(schoolObserveSeconds.x, schoolObserveSeconds.y);
        }
        else if (fish != null)
        {
            SetFocusedFish(fish);
            intent = fish.IsReleasedFish || Random.value >= 0.22f
                ? DiverIntent.ApproachFish
                : DiverIntent.DriftPast;
            currentFocusPoint = fish.VisualCenter;
            currentFocusForward = fish.transform.forward;
            currentFocusRadius = fish.CameraFocusRadius;
            ChooseObservationAngle(currentFocusPoint, currentFocusForward);
            BeginFocusedFishObservationAfterTag(FishObserveDurationForCurrentAngle());
        }
        else
        {
            SetFocusedFish(null);
            intent = DiverIntent.Cruise;
            hasObservationAngle = false;
            PickCruiseDestination();
        }

        nextTargetRefreshTime = Time.time + ScanDelay();
    }

    private void BeginFocusedFishObservationAfterTag(float observeSeconds)
    {
        pendingFishObserveSeconds = Mathf.Max(1f, observeSeconds);
        focusedFishSelectionStartedAt = Time.time;
        waitingForFocusedFishTag = focusedFish != null;
        focusedFishSinceTime = waitingForFocusedFishTag ? 0f : Time.time;
        intentUntilTime = Time.time
            + Mathf.Max(
                pendingFishObserveSeconds,
                pendingFishObserveSeconds + Mathf.Max(0.5f, focusObserveWaitForTagSeconds)
            );
    }

    private void UpdateFocusedFishObservationTimer()
    {
        if (!waitingForFocusedFishTag
            || focusedFish == null
            || (intent != DiverIntent.ApproachFish && intent != DiverIntent.DriftPast))
        {
            return;
        }

        bool tagVisible = focusedFish.IsNicknameTagVisibleForCamera;
        bool waitExpired = Time.time >= focusedFishSelectionStartedAt + Mathf.Max(0.5f, focusObserveWaitForTagSeconds);
        if (!tagVisible && !waitExpired)
        {
            intentUntilTime = Mathf.Max(intentUntilTime, Time.time + 0.5f);
            return;
        }

        waitingForFocusedFishTag = false;
        focusedFishSinceTime = Time.time;
        intentUntilTime = Time.time + Mathf.Max(1f, pendingFishObserveSeconds);
    }

    private bool ShouldHoldCurrentIntent()
    {
        if (Time.time >= intentUntilTime)
        {
            return false;
        }

        if ((intent == DiverIntent.ApproachFish || intent == DiverIntent.DriftPast) && focusedFish != null)
        {
            return true;
        }

        if (intent == DiverIntent.ApproachSchool)
        {
            return true;
        }

        return Time.time < nextTargetRefreshTime;
    }

    private void TrackReleasedFishFocusCandidates(IReadOnlyList<FishActor> fishes)
    {
        PruneKnownReleasedFish(fishes);

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor fish = fishes[i];
            if (!IsReleasedFocusCandidate(fish) || knownReleasedFish.Contains(fish))
            {
                continue;
            }

            knownReleasedFish.Add(fish);
            EnqueueNewFishFocus(fish);
        }
    }

    private void RequeueCompletedFocusedFish(IReadOnlyList<FishActor> activeFishes)
    {
        if ((intent == DiverIntent.ApproachFish || intent == DiverIntent.DriftPast)
            && IsValidQueuedReleasedFish(focusedFish, activeFishes))
        {
            EnqueueReleasedFishFocus(focusedFish);
        }
    }

    private void EnqueueNewFishFocus(FishActor fish)
    {
        if (!IsReleasedFocusCandidate(fish)
            || queuedNewFishFocus.Contains(fish)
            || queuedReleasedFishFocus.Contains(fish))
        {
            return;
        }

        newFishFocusQueue.Enqueue(fish);
        queuedNewFishFocus.Add(fish);
    }

    private void EnqueueReleasedFishFocus(FishActor fish)
    {
        if (!IsReleasedFocusCandidate(fish)
            || queuedNewFishFocus.Contains(fish)
            || queuedReleasedFishFocus.Contains(fish))
        {
            return;
        }

        releasedFishFocusQueue.Enqueue(fish);
        queuedReleasedFishFocus.Add(fish);
    }

    private FishActor PickQueuedReleasedFish(IReadOnlyList<FishActor> activeFishes)
    {
        FishActor fish = DequeueValidQueuedFish(newFishFocusQueue, queuedNewFishFocus, activeFishes);
        return fish != null
            ? fish
            : DequeueValidQueuedFish(releasedFishFocusQueue, queuedReleasedFishFocus, activeFishes);
    }

    private FishActor DequeueValidQueuedFish(Queue<FishActor> queue, HashSet<FishActor> queuedSet, IReadOnlyList<FishActor> activeFishes)
    {
        while (queue.Count > 0)
        {
            FishActor fish = queue.Dequeue();
            queuedSet.Remove(fish);
            if (IsValidQueuedReleasedFish(fish, activeFishes))
            {
                return fish;
            }
        }

        return null;
    }

    private void PruneKnownReleasedFish(IReadOnlyList<FishActor> activeFishes)
    {
        focusQueuePruneBuffer.Clear();
        foreach (FishActor fish in knownReleasedFish)
        {
            if (!IsValidQueuedReleasedFish(fish, activeFishes))
            {
                focusQueuePruneBuffer.Add(fish);
            }
        }

        for (int i = 0; i < focusQueuePruneBuffer.Count; i++)
        {
            knownReleasedFish.Remove(focusQueuePruneBuffer[i]);
        }

        focusQueuePruneBuffer.Clear();
    }

    private void ClearReleasedFishFocusQueues()
    {
        newFishFocusQueue.Clear();
        releasedFishFocusQueue.Clear();
        knownReleasedFish.Clear();
        queuedNewFishFocus.Clear();
        queuedReleasedFishFocus.Clear();
        focusQueuePruneBuffer.Clear();
    }

    private static bool IsReleasedFocusCandidate(FishActor fish)
    {
        return fish != null && fish.IsReleasedFish;
    }

    private static bool IsValidQueuedReleasedFish(FishActor fish, IReadOnlyList<FishActor> activeFishes)
    {
        return IsReleasedFocusCandidate(fish) && ContainsFish(activeFishes, fish);
    }

    private static bool ContainsFish(IReadOnlyList<FishActor> fishes, FishActor fish)
    {
        if (fish == null || fishes == null)
        {
            return false;
        }

        for (int i = 0; i < fishes.Count; i++)
        {
            if (fishes[i] == fish)
            {
                return true;
            }
        }

        return false;
    }

    private void SetFocusedFish(FishActor fish)
    {
        if (focusedFish == fish)
        {
            if (focusedFish != null)
            {
                focusedFish.SetCameraFocused(true);
            }

            return;
        }

        BeginFocusTransition();

        if (focusedFish != null)
        {
            focusedFish.SetCameraFocused(false);
        }

        focusedFish = fish;
        waitingForFocusedFishTag = false;
        pendingFishObserveSeconds = 0f;
        focusedFishSelectionStartedAt = Time.time;

        if (focusedFish != null)
        {
            focusedFish.SetCameraFocused(true);
            focusedFishSinceTime = Time.time;
            PushRecentFishFocus(focusedFish);
        }
        else
        {
            focusedFishSinceTime = 0f;
        }
    }

    private void BeginFocusTransition()
    {
        if (!hasPlacedInitialCamera)
        {
            return;
        }

        focusTransitionActive = true;
        focusTransitionStartedAt = Time.time;
        focusTransitionDuration = Mathf.Max(0.05f, focusTransitionSeconds);
        focusTransitionStartPosition = transform.position;
        focusTransitionStartLookTarget = hasLastLookTarget
            ? lastLookTarget
            : transform.position + transform.forward * Mathf.Max(1f, lookAhead);
        positionVelocity = Vector3.zero;
    }

    private bool TryFindSchool(IReadOnlyList<FishActor> fishes, out Vector3 schoolCenter, out Vector3 schoolForward, out float schoolRadius, out int schoolSize)
    {
        schoolCenter = Vector3.zero;
        schoolForward = transform.forward;
        schoolRadius = 0f;
        schoolSize = 0;

        Vector3 bestCenter = Vector3.zero;
        Vector3 bestForward = Vector3.zero;
        float bestRadius = 0f;
        float bestScore = float.NegativeInfinity;
        int bestCount = 0;

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor anchor = fishes[i];
            if (anchor == null)
            {
                continue;
            }

            Vector3 centerSum = Vector3.zero;
            Vector3 forwardSum = Vector3.zero;
            float farthest = 0f;
            int count = 0;

            for (int j = 0; j < fishes.Count; j++)
            {
                FishActor candidate = fishes[j];
                if (candidate == null)
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - anchor.transform.position;
                Vector3 horizontalOffset = new Vector3(offset.x, 0f, offset.z);
                if (horizontalOffset.magnitude > schoolNeighborRadius || Mathf.Abs(offset.y) > 2.2f)
                {
                    continue;
                }

                centerSum += candidate.transform.position;
                forwardSum += FlattenDirection(candidate.transform.forward);
                farthest = Mathf.Max(farthest, horizontalOffset.magnitude);
                count++;
            }

            if (count < minimumSchoolSize)
            {
                continue;
            }

            Vector3 candidateCenter = centerSum / count;
            float distance = Vector3.Distance(transform.position, candidateCenter);
            if (distance > schoolInterestDistance)
            {
                continue;
            }

            float forwardScore = Vector3.Dot(transform.forward, (candidateCenter - transform.position).normalized);
            float score = count * 3f + forwardScore * 2f - distance * 0.12f + Random.Range(0f, 1.2f);
            if (score > bestScore)
            {
                bestScore = score;
                bestCenter = candidateCenter;
                bestForward = forwardSum.sqrMagnitude > 0.001f ? forwardSum.normalized : transform.forward;
                bestRadius = farthest;
                bestCount = count;
            }
        }

        if (bestCount < minimumSchoolSize)
        {
            return false;
        }

        schoolCenter = bestCenter;
        schoolForward = FlattenDirection(bestForward);
        schoolRadius = bestRadius;
        schoolSize = bestCount;
        return true;
    }

    private Vector3 FlattenDirection(Vector3 direction)
    {
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude < 0.001f)
        {
            flat = Vector3.forward;
        }

        return flat.normalized;
    }

    private float ScanDelay()
    {
        return Mathf.Max(0.6f, Mathf.Min(scanIntervalSeconds, targetRefreshSeconds));
    }

    private float FishObserveDurationForCurrentAngle()
    {
        float angle = Mathf.Abs(targetObservationAngleDegrees);
        bool goodAngle = angle <= Mathf.Clamp(goodObservationAngleDegrees, 10f, maximumObservationAngleFromFront);
        Vector2 range = goodAngle ? goodAngleFishObserveSeconds : fishObserveSeconds;
        float min = Mathf.Max(1f, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return Random.Range(min, max);
    }

    private FishActor PickCinematicFish(IReadOnlyList<FishActor> fishes, bool releasedOnly)
    {
        FishActor best = null;
        float bestScore = float.NegativeInfinity;
        int eligibleCount = CountEligibleFish(fishes, releasedOnly);
        bool shouldAvoidCurrentFish = eligibleCount > 1 && HasExceededContinuousFishFocus();

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor fish = fishes[i];
            if (fish == null)
            {
                continue;
            }

            if (releasedOnly && !fish.IsReleasedFish)
            {
                continue;
            }

            Vector3 toFish = fish.transform.position - transform.position;
            float distance = toFish.magnitude;
            if (shouldAvoidCurrentFish && fish == focusedFish)
            {
                continue;
            }

            float forwardScore = Vector3.Dot(transform.forward, toFish.normalized);
            float idealDistance = releasedOnly ? followDistance * 1.15f : followDistance * 1.6f;
            float distanceScore = -Mathf.Abs(distance - idealDistance);
            float centerScore = -Vector3.Distance(fish.transform.position, center) * 0.08f;
            float releasedScore = fish.IsReleasedFish ? 60f : 0f;
            float farPenalty = releasedOnly ? 0f : Mathf.Max(0f, distance - fishInterestDistance) * 0.12f;
            float score = releasedScore + forwardScore * 3.5f + distanceScore + centerScore + Random.Range(0f, 0.75f) - farPenalty;
            score -= RecentFishFocusPenalty(fish);

            if (fish == focusedFish && focusedFishSinceTime > 0f)
            {
                float focusAge = Time.time - focusedFishSinceTime;
                float repeatBlend = Mathf.InverseLerp(maxContinuousFishFocusSeconds * 0.45f, maxContinuousFishFocusSeconds, focusAge);
                score -= currentFishRepeatPenalty * repeatBlend;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = fish;
            }
        }

        return best;
    }

    private int CountEligibleFish(IReadOnlyList<FishActor> fishes, bool releasedOnly)
    {
        int count = 0;
        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor fish = fishes[i];
            if (fish == null)
            {
                continue;
            }

            if (releasedOnly && !fish.IsReleasedFish)
            {
                continue;
            }

            if (!releasedOnly && Vector3.Distance(transform.position, fish.transform.position) > fishInterestDistance)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private bool HasExceededContinuousFishFocus()
    {
        return focusedFish != null
            && focusedFishSinceTime > 0f
            && Time.time - focusedFishSinceTime >= Mathf.Max(1f, maxContinuousFishFocusSeconds);
    }

    private void PushRecentFishFocus(FishActor fish)
    {
        PruneRecentFishFocusStack();
        recentFishFocusStack.Remove(fish);
        recentFishFocusStack.Insert(0, fish);

        int maxStackSize = Mathf.Max(0, recentFishFocusStackSize);
        while (recentFishFocusStack.Count > maxStackSize)
        {
            recentFishFocusStack.RemoveAt(recentFishFocusStack.Count - 1);
        }
    }

    private float RecentFishFocusPenalty(FishActor fish)
    {
        if (fish == null || recentFishFocusStackSize <= 0)
        {
            return 0f;
        }

        PruneRecentFishFocusStack();
        int index = recentFishFocusStack.IndexOf(fish);
        if (index < 0)
        {
            return 0f;
        }

        float recency = 1f - index / (float)Mathf.Max(1, recentFishFocusStackSize);
        return recentFishFocusPenalty * Mathf.Clamp01(recency);
    }

    private void PruneRecentFishFocusStack()
    {
        for (int i = recentFishFocusStack.Count - 1; i >= 0; i--)
        {
            if (recentFishFocusStack[i] == null)
            {
                recentFishFocusStack.RemoveAt(i);
            }
        }
    }

    private static bool HasReleasedFish(IReadOnlyList<FishActor> fishes)
    {
        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor fish = fishes[i];
            if (fish != null && fish.IsReleasedFish)
            {
                return true;
            }
        }

        return false;
    }

    private bool MoveCamera(Vector3 desiredPosition, float smoothTime, float maxSpeed)
    {
        Vector3 desiredMoveDirection = desiredPosition - transform.position;
        desiredPosition = ResolveSafeDiverPosition(desiredPosition, desiredMoveDirection, true);
        if (!hasPlacedInitialCamera)
        {
            transform.position = desiredPosition;
            positionVelocity = Vector3.zero;
            hasPlacedInitialCamera = true;
            return true;
        }

        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            smoothTime,
            maxSpeed
        );
        Vector3 unclampedNextPosition = nextPosition;
        nextPosition = ResolveSafeDiverPosition(nextPosition, desiredPosition - transform.position, false);
        if (nextPosition.y > unclampedNextPosition.y + 0.001f && positionVelocity.y < 0f)
        {
            positionVelocity.y = 0f;
        }

        transform.position = nextPosition;

        return false;
    }

    private Vector3 ResolveSafeDiverPosition(Vector3 desiredPosition, Vector3 movementDirection, bool allowSideStep)
    {
        if (!avoidSeabed)
        {
            return desiredPosition;
        }

        OceanEnvironment environment = ResolveOceanEnvironment();
        if (environment == null)
        {
            return desiredPosition;
        }

        float clearance = Mathf.Max(0.25f, cameraSeabedClearance);
        Vector3 safePosition = RaiseAboveSeabed(environment, desiredPosition, clearance);
        if (!allowSideStep)
        {
            return safePosition;
        }

        Vector3 travelDirection = FlattenDirection(movementDirection);
        if (travelDirection.sqrMagnitude < 0.001f)
        {
            return safePosition;
        }

        Vector3 aheadPosition = safePosition + travelDirection * Mathf.Max(0.5f, cameraSeabedProbeDistance);
        float floorY = SampleSeabedWorldY(environment, safePosition);
        float aheadFloorY = SampleSeabedWorldY(environment, aheadPosition);
        if (aheadFloorY <= floorY + 0.08f)
        {
            return safePosition;
        }

        float aheadPressure = Mathf.InverseLerp(clearance * 1.4f, clearance * 0.55f, safePosition.y - aheadFloorY);
        if (aheadPressure <= 0f)
        {
            return safePosition;
        }

        Vector3 sideDirection = LowerSeabedSide(environment, safePosition, travelDirection);
        safePosition += sideDirection * Mathf.Max(0.25f, cameraSeabedSideStep) * aheadPressure * 0.55f;
        return RaiseAboveSeabed(environment, safePosition, clearance);
    }

    private Vector3 RaiseAboveSeabed(OceanEnvironment environment, Vector3 position, float clearance)
    {
        float floorY = SampleSeabedWorldY(environment, position);
        float minimumY = floorY + clearance;
        if (position.y < minimumY)
        {
            position.y = minimumY;
        }

        return position;
    }

    private Vector3 LowerSeabedSide(OceanEnvironment environment, Vector3 position, Vector3 travelDirection)
    {
        Vector3 right = Vector3.Cross(Vector3.up, travelDirection);
        if (right.sqrMagnitude < 0.001f)
        {
            right = transform.right;
        }

        right.Normalize();
        float step = Mathf.Max(0.25f, cameraSeabedSideStep);
        float leftFloorY = SampleSeabedWorldY(environment, position - right * step);
        float rightFloorY = SampleSeabedWorldY(environment, position + right * step);
        return rightFloorY < leftFloorY ? right : -right;
    }

    private static float SampleSeabedWorldY(OceanEnvironment environment, Vector3 worldPosition)
    {
        Vector3 local = environment.transform.InverseTransformPoint(worldPosition);
        float localFloorY = environment.SampleSeabedHeight(local.x, local.z);
        return environment.transform.TransformPoint(new Vector3(local.x, localFloorY, local.z)).y;
    }

    private static float SampleWaterSurfaceWorldY(OceanEnvironment environment)
    {
        return environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY, 0f)).y;
    }

    private void LookAtTarget(Vector3 lookTarget, float smooth, bool snap)
    {
        lastLookTarget = lookTarget;
        hasLastLookTarget = true;

        if (snap)
        {
            SnapLookAt(lookTarget);
            return;
        }

        SmoothLookAt(lookTarget, smooth);
    }

    private void SnapLookAt(Vector3 lookTarget)
    {
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private void SmoothLookAt(Vector3 lookTarget, float smooth)
    {
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookDirection.normalized, Vector3.up),
            smooth * Time.deltaTime
        );
    }
}
