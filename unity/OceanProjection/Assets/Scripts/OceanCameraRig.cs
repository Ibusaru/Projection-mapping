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
    [SerializeField] private float cameraMaxTurnDegreesPerSecond = 72f;

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
    [SerializeField] private float cinematicExploreInterestLookBlend = 0.46f;
    [SerializeField] private float cinematicDroneHeightScale = 0.54f;
    [SerializeField] private float cinematicDroneMinimumHeightScale = 0.5f;
    [SerializeField] private float cinematicDroneFieldOfView = 64f;
    [SerializeField] private float cinematicDefaultFieldOfView = 48f;
    [SerializeField] private float cinematicFieldOfViewSmooth = 1.4f;
    [SerializeField] private float cinematicDronePathVariation = 0.34f;
    [SerializeField] private float cinematicDroneLookVariation = 0.46f;
    [SerializeField] private float cinematicDroneLateralSweep = 0.2f;
    [SerializeField] private float cinematicSurfaceHeight = 1.35f;

    [Header("Diver Follow")]
    [SerializeField] private bool focusFish = true;
    [SerializeField] private float targetRefreshSeconds = 5f;
    [SerializeField] private bool prioritizeReleasedFish = true;
    [SerializeField] private float followDistance = 5.2f;
    [SerializeField] private float sideOffset = 0.48f;
    [SerializeField] private float heightOffset = 0.55f;
    [SerializeField] private float forwardLookAhead = 2.4f;
    [SerializeField] private float minFocusDistance = 4.8f;
    [SerializeField] private float maxFocusDistance = 12.5f;
    [SerializeField] private float focusRadiusMultiplier = 3.6f;
    [SerializeField] private float releasedFishApproachDistance = 5.3f;
    [SerializeField] private float releasedFishFocusRadiusMultiplier = 1.35f;
    [SerializeField] private float minReleasedFishFocusDistance = 4.8f;
    [SerializeField] private float positionSmoothTime = 2.8f;
    [SerializeField] private float rotationSmooth = 1.45f;
    [SerializeField] private float driftAmplitude = 0.34f;
    [SerializeField] private float driftSpeed = 0.24f;
    [SerializeField] private float maximumCameraSpeed = 5.8f;
    [SerializeField] private float focusTransitionSeconds = 1.45f;
    [SerializeField] private float focusTransitionMaxSpeedMultiplier = 1.35f;
    [SerializeField] private float surfaceDiveSmoothTime = 0.075f;
    [SerializeField] private float surfaceDiveMaxSpeed = 92f;
    [SerializeField] private float surfaceDiveDiagonalOffset = 10f;
    [SerializeField] private float surfaceDiveDuration = 0.82f;
    [SerializeField] private float surfaceDiveArcHeight = 4.5f;
    [SerializeField] private float surfaceDiveArcForwardOffset = 18f;
    [SerializeField] private float surfaceDiveMinimumTargetDepth = 4.6f;
    [SerializeField] private float surfaceDiveDepthTrigger = 0.9f;
    [SerializeField] private float surfaceDiveFieldOfViewKick = 16f;
    [SerializeField] private float surfaceDiveFieldOfViewSmooth = 8.5f;
    [SerializeField] private bool showSurfaceDiveSpeedLines = true;
    [SerializeField] private int surfaceDiveSpeedLineCount = 22;
    [SerializeField] private float surfaceDiveSpeedLineDistance = 2.6f;
    [SerializeField] private float surfaceDiveSpeedLineLength = 0.48f;
    [SerializeField] private float surfaceDiveSpeedLineWidth = 0.022f;
    [SerializeField] private Color surfaceDiveSpeedLineColor = new Color(0.7f, 0.98f, 1f, 0.78f);

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
    [SerializeField] private float fishApproachDistance = 5.9f;
    [SerializeField] private float schoolApproachDistance = 9.2f;
    [SerializeField] private Vector2 fishObserveSeconds = new Vector2(6.5f, 7.5f);
    [SerializeField] private Vector2 goodAngleFishObserveSeconds = new Vector2(6.5f, 7.5f);
    [SerializeField] private Vector2 schoolObserveSeconds = new Vector2(5.5f, 9f);
    [SerializeField] private float focusObserveWaitForTagSeconds = 1.5f;
    [SerializeField] private float fishObserveStartDistance = 6.8f;
    [SerializeField] private Vector2 cruiseSeconds = new Vector2(7f, 12f);
    [SerializeField] private float cruiseInterestLookBlend = 0.42f;
    [SerializeField] private float cruiseInterestApproachBlend = 0.46f;
    [SerializeField] private float roamLookSweepBlend = 0.72f;
    [SerializeField] private float roamLookSweepDistance = 11f;
    [SerializeField] private float roamLookSweepSideOffset = 7.4f;
    [SerializeField] private float roamLookSweepVerticalOffset = 1.3f;
    [SerializeField] private float roamLookSweepSpeed = 0.58f;
    [SerializeField] private float diverLookAhead = 1.8f;
    [SerializeField] private float schoolRadiusPadding = 1.35f;
    [SerializeField] private float relaxedCameraSpeed = 2.7f;
    [SerializeField] private float approachCameraSpeed = 4.4f;
    [SerializeField] private float maxContinuousFishFocusSeconds = 8f;
    [SerializeField] private int recentFishFocusStackSize = 6;
    [SerializeField] private float recentFishFocusPenalty = 10f;
    [SerializeField] private float currentFishRepeatPenalty = 18f;
    [SerializeField] private bool showNearbyReleasedFishTagsWhileRoaming = true;
    [SerializeField] private float nearbyReleasedFishTagDistance = 6.4f;
    [SerializeField] private float nearbyReleasedFishTagHysteresis = 1.2f;
    [SerializeField, Range(-0.2f, 0.95f)] private float nearbyReleasedFishTagForwardDot = 0.18f;
    [SerializeField, Range(1, 4)] private int maxNearbyReleasedFishTags = 2;
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
    private float focusedFishObserveDistanceReachedAt;
    private readonly List<FishActor> recentFishFocusStack = new List<FishActor>();
    private readonly Queue<FishActor> newFishFocusQueue = new Queue<FishActor>();
    private readonly Queue<FishActor> releasedFishFocusQueue = new Queue<FishActor>();
    private readonly HashSet<FishActor> knownReleasedFish = new HashSet<FishActor>();
    private readonly HashSet<FishActor> queuedNewFishFocus = new HashSet<FishActor>();
    private readonly HashSet<FishActor> queuedReleasedFishFocus = new HashSet<FishActor>();
    private readonly List<FishActor> focusQueuePruneBuffer = new List<FishActor>();
    private readonly HashSet<FishActor> nearbyTaggedReleasedFish = new HashSet<FishActor>();
    private readonly List<FishActor> nearbyTagPruneBuffer = new List<FishActor>();
    private float focusedFishSinceTime;
    private static readonly OceanCinematicShotKind[] CinematicSequence =
    {
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.UnderwaterExplore,
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.ReefDive,
        OceanCinematicShotKind.FishFocus,
        OceanCinematicShotKind.DroneOverview,
        OceanCinematicShotKind.UnderwaterExplore
    };
    private OceanCinematicShotKind currentCinematicShot = OceanCinematicShotKind.FishFocus;
    private float cinematicShotStartedAt;
    private float cinematicShotDuration = 12f;
    private int cinematicShotIndex = -1;
    private bool hasCinematicShot;
    private Camera rigCamera;
    private float cinematicShotVariantSeed;
    private bool surfaceDiveArcActive;
    private Vector3 surfaceDiveStartPosition;
    private Vector3 surfaceDiveControlPosition;
    private Vector3 surfaceDiveTargetPosition;
    private float surfaceDiveStartedAt;
    private Transform surfaceDiveSpeedLineRoot;
    private LineRenderer[] surfaceDiveSpeedLines;
    private Material surfaceDiveSpeedLineMaterial;

    private void OnDisable()
    {
        SetFocusedFish(null);
        ClearNearbyReleasedFishTags();
        SetSurfaceDiveSpeedLinesVisible(false);
    }

    private void LateUpdate()
    {
        if (useCinematicShots)
        {
            UpdateCinematicCamera();
            UpdateNearbyReleasedFishTags();
            return;
        }

        if (focusFish)
        {
            UpdateDiverFollowCamera();
            UpdateNearbyReleasedFishTags();
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
        UpdateNearbyReleasedFishTags();
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
            ApplyCinematicFieldOfView(currentCinematicShot);
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
        ApplyCinematicFieldOfView(currentCinematicShot);
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
        SetSurfaceDiveSpeedLinesVisible(false);
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
            cinematicShotVariantSeed = Random.Range(0f, 1000f);
            hasCinematicShot = true;
            if (candidate != OceanCinematicShotKind.FishFocus)
            {
                SetFocusedFish(null);
                surfaceDiveArcActive = false;
                SetSurfaceDiveSpeedLinesVisible(false);
            }
            return;
        }

        currentCinematicShot = OceanCinematicShotKind.FishFocus;
        cinematicShotStartedAt = Time.time;
        cinematicShotDuration = RandomDuration(cinematicFishFocusShotSeconds, Mathf.Max(8f, cinematicShotSeconds.x));
        cinematicShotVariantSeed = Random.Range(0f, 1000f);
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
        Vector2 size = environment != null ? environment.ActiveAreaSize : new Vector2(Mathf.Max(1f, orbitSize.x * 4f), Mathf.Max(1f, orbitSize.z * 4f));
        float waterY = environment != null ? environment.WaterSurfaceY : center.y + orbitSize.y;
        Vector3 oceanCenter = OceanLocalToWorld(environment, new Vector3(0f, Mathf.Lerp(center.y, waterY - 3.5f, 0.45f), 0f));
        Vector3 reef = FeatureOrFallback(environment, OceanFeatureKind.Reef, new Vector3(size.x * 0.18f, waterY - 3f, size.y * 0.13f), size);
        Vector3 beach = FeatureOrFallback(environment, OceanFeatureKind.Beach, new Vector3(size.x * 0.86f, waterY + 0.4f, -size.y * 0.16f), size);
        Vector3 trench = FeatureOrFallback(environment, OceanFeatureKind.Trench, new Vector3(-size.x * 0.32f, waterY - 14f, size.y * 0.27f), size);
        Vector3 rock = FeatureOrFallback(environment, OceanFeatureKind.RockMountain, new Vector3(-size.x * 0.28f, waterY - 3f, -size.y * 0.28f), size);

        switch (shot)
        {
            case OceanCinematicShotKind.DroneOverview:
            {
                float maxSize = Mathf.Max(size.x, size.y);
                float pathVariation = Mathf.Clamp01(cinematicDronePathVariation);
                float lookVariation = Mathf.Clamp01(cinematicDroneLookVariation);
                float mirror = CinematicVariantSign(0.11f);
                float crossBend = CinematicVariantSigned(0.23f) * pathVariation;
                float heightScale = Mathf.Max(cinematicDroneHeightScale, cinematicDroneMinimumHeightScale)
                    * Mathf.Lerp(0.9f, 1.18f, CinematicVariant01(0.31f));
                float height = maxSize * heightScale;
                Vector3 p0 = OceanLocalToWorld(environment, new Vector3(
                    -size.x * Mathf.Lerp(0.66f, 0.48f, CinematicVariant01(0.41f)),
                    waterY + height * Mathf.Lerp(0.92f, 1.08f, CinematicVariant01(0.47f)),
                    mirror * size.y * Mathf.Lerp(0.46f, 0.62f, CinematicVariant01(0.53f))
                ));
                Vector3 p1 = OceanLocalToWorld(environment, new Vector3(
                    -size.x * Mathf.Lerp(0.54f, 0.34f, CinematicVariant01(0.59f)),
                    waterY + height * Mathf.Lerp(0.95f, 1.16f, CinematicVariant01(0.61f)),
                    mirror * size.y * Mathf.Lerp(0.26f, 0.5f, CinematicVariant01(0.67f))
                ));
                Vector3 p2 = OceanLocalToWorld(environment, new Vector3(
                    size.x * Mathf.Lerp(-0.12f, 0.16f, CinematicVariant01(0.71f)),
                    waterY + height * Mathf.Lerp(0.78f, 1.05f, CinematicVariant01(0.79f)),
                    -mirror * size.y * (0.02f + crossBend * 0.32f)
                ));
                Vector3 p3 = OceanLocalToWorld(environment, new Vector3(
                    size.x * Mathf.Lerp(0.2f, 0.46f, CinematicVariant01(0.83f)),
                    waterY + height * Mathf.Lerp(0.62f, 0.92f, CinematicVariant01(0.89f)),
                    -mirror * size.y * Mathf.Lerp(0.18f, 0.44f, CinematicVariant01(0.97f))
                ));
                position = CatmullRom(p0, p1, p2, p3, droneT);
                Vector3 lateralGlide = Vector3.Cross(Vector3.up, FlattenDirection(p3 - p0));
                if (lateralGlide.sqrMagnitude > 0.001f)
                {
                    lateralGlide.Normalize();
                    position += lateralGlide
                        * Mathf.Sin((droneT + CinematicVariant01(1.03f)) * Mathf.PI)
                        * maxSize
                        * Mathf.Max(0f, cinematicDroneLateralSweep)
                        * pathVariation;
                }

                Vector3 lead = CatmullRom(p0, p1, p2, p3, Mathf.Clamp01(droneT + 0.12f));
                Vector3 overviewCenter = OceanLocalToWorld(environment, new Vector3(
                    size.x * CinematicVariantSigned(1.11f) * 0.18f,
                    waterY - Mathf.Lerp(1.8f, 4.4f, CinematicVariant01(1.17f)),
                    size.y * CinematicVariantSigned(1.23f) * 0.18f
                ));
                Vector3 landSide = Vector3.Lerp(beach + Vector3.up * Mathf.Lerp(1.0f, 2.5f, CinematicVariant01(1.29f)), overviewCenter, 0.35f);
                Vector3 reefSweep = Vector3.Lerp(reef, trench + Vector3.up * 2.2f, Mathf.Clamp01(droneT * 0.42f + CinematicVariant01(1.31f) * 0.28f));
                Vector3 rockSweep = Vector3.Lerp(rock, landSide, Mathf.Clamp01(0.18f + droneT * 0.82f));
                Vector3 featureSweep = Vector3.Lerp(reefSweep, rockSweep, Mathf.Clamp01(0.22f + lookVariation * (0.18f + droneT * 0.54f)));
                lookTarget = Vector3.Lerp(featureSweep, lead + Vector3.down * maxSize * Mathf.Lerp(0.18f, 0.34f, lookVariation), 0.22f);
                lookTarget.y = Mathf.Lerp(waterY + 2.2f, waterY - Mathf.Lerp(4.2f, 7.0f, lookVariation), Mathf.Clamp01(droneT + 0.08f));
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
            case OceanCinematicShotKind.UnderwaterExplore:
            {
                position = UnderwaterExploreRoutePoint(environment, size, waterY, t)
                    + DiverDrift(Time.time * driftSpeed + 3.1f, 1f) * 0.24f;

                Vector3 lead = UnderwaterExploreRoutePoint(environment, size, waterY, Mathf.Clamp01(t + 0.08f));
                Vector3 routeLookTarget = Vector3.Lerp(
                    lead + Vector3.up * 0.55f,
                    Vector3.Lerp(reef, oceanCenter, 0.55f),
                    0.18f
                );
                routeLookTarget = ApplyRoamLookSweep(position, routeLookTarget, lead - position, t * 5.7f + 1.3f);
                lookTarget = BlendRoamLookTargetTowardInterest(
                    position,
                    routeLookTarget,
                    lead - position,
                    cinematicExploreInterestLookBlend
                );
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

    private Vector3 FeatureOrFallback(OceanEnvironment environment, OceanFeatureKind feature, Vector3 fallbackLocal, Vector2 tourSize)
    {
        if (environment != null && environment.TryGetFeaturePoint(feature, out Vector3 point))
        {
            Vector3 localPoint = environment.transform.InverseTransformPoint(point);
            if (Mathf.Abs(localPoint.x) <= tourSize.x * 0.72f && Mathf.Abs(localPoint.z) <= tourSize.y * 0.72f)
            {
                return point;
            }
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

    private Vector3 UnderwaterExploreRoutePoint(OceanEnvironment environment, Vector2 size, float waterY, float normalizedTime)
    {
        Vector3 p0 = SampleSwimPointWorld(environment, new Vector2(-size.x * 0.54f, -size.y * 0.18f), waterY - 6.4f, 3.2f, 2.6f);
        Vector3 p1 = SampleSwimPointWorld(environment, new Vector2(-size.x * 0.46f, size.y * 0.32f), waterY - 6.8f, 3.1f, 2.8f);
        Vector3 p2 = SampleSwimPointWorld(environment, new Vector2(-size.x * 0.18f, size.y * 0.42f), waterY - 7.2f, 3.2f, 3.0f);
        Vector3 p3 = SampleSwimPointWorld(environment, new Vector2(size.x * 0.18f, size.y * 0.28f), waterY - 6.7f, 3.2f, 2.8f);
        Vector3 p4 = SampleSwimPointWorld(environment, new Vector2(size.x * 0.46f, -size.y * 0.08f), waterY - 6.1f, 3.3f, 2.5f);
        Vector3 p5 = SampleSwimPointWorld(environment, new Vector2(size.x * 0.28f, -size.y * 0.36f), waterY - 6.9f, 3.1f, 2.8f);
        Vector3 p6 = SampleSwimPointWorld(environment, new Vector2(-size.x * 0.08f, -size.y * 0.3f), waterY - 7.1f, 3.2f, 3.0f);

        float routeT = Mathf.Clamp01(normalizedTime) * 4f;
        int segment = Mathf.Min(3, Mathf.FloorToInt(routeT));
        float localT = routeT - segment;
        switch (segment)
        {
            case 0:
                return CatmullRom(p0, p1, p2, p3, localT);
            case 1:
                return CatmullRom(p1, p2, p3, p4, localT);
            case 2:
                return CatmullRom(p2, p3, p4, p5, localT);
            default:
                return CatmullRom(p3, p4, p5, p6, localT);
        }
    }

    private Vector3 BlendRoamLookTargetTowardInterest(Vector3 cameraPosition, Vector3 routeLookTarget, Vector3 travelDirection, float maxBlend)
    {
        if (!TryFindRoamInterestTarget(out Vector3 interestPoint, out float interestStrength))
        {
            return routeLookTarget;
        }

        Vector3 toInterest = interestPoint - cameraPosition;
        if (toInterest.sqrMagnitude < 0.001f)
        {
            return routeLookTarget;
        }

        Vector3 flatTravel = FlattenDirection(travelDirection);
        Vector3 flatInterest = FlattenDirection(toInterest);
        float forwardness = Mathf.InverseLerp(-0.2f, 0.72f, Vector3.Dot(flatTravel, flatInterest));
        float blend = Mathf.Clamp01(maxBlend * interestStrength * forwardness);
        return Vector3.Lerp(routeLookTarget, interestPoint, blend);
    }

    private Vector3 ApplyRoamLookSweep(Vector3 cameraPosition, Vector3 routeLookTarget, Vector3 travelDirection, float phaseOffset)
    {
        Vector3 forward = FlattenDirection(travelDirection);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f)
        {
            right = transform.right;
        }
        else
        {
            right.Normalize();
        }

        float speed = Mathf.Max(0.05f, roamLookSweepSpeed);
        float sideSweep = Mathf.Sin(Time.time * speed + phaseOffset);
        float longSweep = Mathf.Sin(Time.time * speed * 0.43f + phaseOffset * 1.71f);
        float verticalSweep = Mathf.Sin(Time.time * speed * 0.61f + phaseOffset * 0.37f);
        Vector3 sweepTarget = cameraPosition
            + forward * Mathf.Max(1f, roamLookSweepDistance)
            + right * sideSweep * Mathf.Max(0f, roamLookSweepSideOffset)
            + right * longSweep * Mathf.Max(0f, roamLookSweepSideOffset) * 0.35f
            + Vector3.up * verticalSweep * Mathf.Max(0f, roamLookSweepVerticalOffset);

        return Vector3.Lerp(routeLookTarget, sweepTarget, Mathf.Clamp01(roamLookSweepBlend));
    }

    private float FocusedFishCenteringBlend()
    {
        if (focusedFish == null || !focusedFish.IsReleasedFish)
        {
            return 0f;
        }

        if (focusedFish.IsNicknameTagVisibleForCamera)
        {
            return 0.94f;
        }

        return waitingForFocusedFishTag ? 0.72f : 0.38f;
    }

    private bool TryFindRoamInterestTarget(out Vector3 interestPoint, out float interestStrength)
    {
        interestPoint = Vector3.zero;
        interestStrength = 0f;

        IReadOnlyList<FishActor> fishes = FishActor.AllActiveFishes;
        if (fishes == null || fishes.Count == 0)
        {
            return false;
        }

        FishActor releasedFish = PickRoamReleasedFish(fishes);
        if (releasedFish != null)
        {
            interestPoint = releasedFish.VisualCenter + Vector3.up * (releasedFish.CameraFocusRadius * 0.2f);
            interestStrength = 0.86f;
            return true;
        }

        if (TryFindRoamSchoolInterest(fishes, out Vector3 schoolCenter, out float schoolRadius, out int schoolSize))
        {
            interestPoint = schoolCenter + Vector3.up * Mathf.Clamp(schoolRadius * 0.12f, 0.25f, 1.2f);
            interestStrength = Mathf.Clamp01(0.38f + schoolSize * 0.035f);
            return true;
        }

        return false;
    }

    private FishActor PickRoamReleasedFish(IReadOnlyList<FishActor> fishes)
    {
        FishActor best = null;
        float bestScore = float.NegativeInfinity;
        float maxDistance = Mathf.Max(fishInterestDistance, schoolInterestDistance) * 1.35f;

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor fish = fishes[i];
            if (!IsReleasedFocusCandidate(fish))
            {
                continue;
            }

            Vector3 point = fish.VisualCenter;
            Vector3 toFish = point - transform.position;
            float distance = toFish.magnitude;
            if (distance > maxDistance)
            {
                continue;
            }

            float forwardScore = toFish.sqrMagnitude > 0.001f
                ? Vector3.Dot(transform.forward, toFish.normalized)
                : 1f;
            float score = forwardScore * 18f - distance * 0.22f - RecentFishFocusPenalty(fish) * 0.24f;
            if (score > bestScore)
            {
                bestScore = score;
                best = fish;
            }
        }

        return best;
    }

    private void UpdateNearbyReleasedFishTags()
    {
        if (!showNearbyReleasedFishTagsWhileRoaming || !ShouldShowNearbyTagsForCurrentCameraMode())
        {
            ClearNearbyReleasedFishTags();
            return;
        }

        IReadOnlyList<FishActor> fishes = FishActor.AllActiveFishes;
        if (fishes == null || fishes.Count == 0)
        {
            ClearNearbyReleasedFishTags();
            return;
        }

        nearbyTagPruneBuffer.Clear();
        foreach (FishActor fish in nearbyTaggedReleasedFish)
        {
            if (!IsNearbyReleasedFishTagCandidate(fish, true, fishes))
            {
                nearbyTagPruneBuffer.Add(fish);
            }
        }

        for (int i = 0; i < nearbyTagPruneBuffer.Count; i++)
        {
            FishActor fish = nearbyTagPruneBuffer[i];
            if (fish != null && fish != focusedFish)
            {
                fish.SetCameraFocused(false);
            }

            nearbyTaggedReleasedFish.Remove(fish);
        }

        int activeCount = nearbyTaggedReleasedFish.Count;
        int tagLimit = Mathf.Max(1, maxNearbyReleasedFishTags);
        for (int i = 0; i < fishes.Count && activeCount < tagLimit; i++)
        {
            FishActor fish = fishes[i];
            if (!IsNearbyReleasedFishTagCandidate(fish, false, fishes) || nearbyTaggedReleasedFish.Contains(fish))
            {
                continue;
            }

            fish.SetCameraFocused(true);
            nearbyTaggedReleasedFish.Add(fish);
            activeCount++;
        }
    }

    private bool ShouldShowNearbyTagsForCurrentCameraMode()
    {
        if (focusedFish != null && intent != DiverIntent.Cruise)
        {
            return false;
        }

        return !useCinematicShots
            || currentCinematicShot != OceanCinematicShotKind.FishFocus
            || intent == DiverIntent.Cruise;
    }

    private bool IsNearbyReleasedFishTagCandidate(FishActor fish, bool alreadyTagged, IReadOnlyList<FishActor> activeFishes)
    {
        if (fish == null
            || fish == focusedFish
            || !fish.IsReleasedFish
            || activeFishes == null
            || !ContainsFish(activeFishes, fish))
        {
            return false;
        }

        Vector3 toFish = fish.VisualCenter - transform.position;
        float distance = toFish.magnitude;
        float distanceLimit = Mathf.Max(0.5f, nearbyReleasedFishTagDistance)
            + (alreadyTagged ? Mathf.Max(0f, nearbyReleasedFishTagHysteresis) : 0f);
        if (distance > distanceLimit || distance <= 0.001f)
        {
            return false;
        }

        Vector3 direction = toFish / distance;
        if (Vector3.Dot(transform.forward, direction) < nearbyReleasedFishTagForwardDot)
        {
            return false;
        }

        Camera camera = ResolveRigCamera();
        if (camera == null)
        {
            return true;
        }

        Vector3 viewport = camera.WorldToViewportPoint(fish.VisualCenter);
        return viewport.z > 0.01f
            && viewport.x >= 0.04f
            && viewport.x <= 0.96f
            && viewport.y >= 0.08f
            && viewport.y <= 0.94f;
    }

    private void ClearNearbyReleasedFishTags()
    {
        if (nearbyTaggedReleasedFish.Count == 0)
        {
            return;
        }

        nearbyTagPruneBuffer.Clear();
        foreach (FishActor fish in nearbyTaggedReleasedFish)
        {
            nearbyTagPruneBuffer.Add(fish);
        }

        for (int i = 0; i < nearbyTagPruneBuffer.Count; i++)
        {
            FishActor fish = nearbyTagPruneBuffer[i];
            if (fish != null && fish != focusedFish)
            {
                fish.SetCameraFocused(false);
            }
        }

        nearbyTaggedReleasedFish.Clear();
    }

    private bool TryFindRoamSchoolInterest(IReadOnlyList<FishActor> fishes, out Vector3 schoolCenter, out float schoolRadius, out int schoolSize)
    {
        schoolCenter = Vector3.zero;
        schoolRadius = 0f;
        schoolSize = 0;

        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor anchor = fishes[i];
            if (anchor == null)
            {
                continue;
            }

            Vector3 centerSum = Vector3.zero;
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
                farthest = Mathf.Max(farthest, horizontalOffset.magnitude);
                count++;
            }

            if (count < minimumSchoolSize)
            {
                continue;
            }

            Vector3 candidateCenter = centerSum / count;
            Vector3 toSchool = candidateCenter - transform.position;
            float distance = toSchool.magnitude;
            if (distance > schoolInterestDistance * 1.15f)
            {
                continue;
            }

            float forwardScore = toSchool.sqrMagnitude > 0.001f
                ? Vector3.Dot(transform.forward, toSchool.normalized)
                : 1f;
            float score = count * 3f + forwardScore * 2.4f - distance * 0.09f;
            if (score > bestScore)
            {
                bestScore = score;
                schoolCenter = candidateCenter;
                schoolRadius = farthest;
                schoolSize = count;
            }
        }

        return schoolSize >= minimumSchoolSize;
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

    private void ApplyCinematicFieldOfView(OceanCinematicShotKind shot)
    {
        Camera camera = ResolveRigCamera();
        if (camera == null)
        {
            return;
        }

        float defaultFov = cinematicDefaultFieldOfView > 0f ? cinematicDefaultFieldOfView : camera.fieldOfView;
        float targetFov = shot == OceanCinematicShotKind.DroneOverview
            ? Mathf.Max(defaultFov, cinematicDroneFieldOfView)
            : defaultFov;
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, cinematicFieldOfViewSmooth) * Time.deltaTime);
        camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, targetFov, blend);
    }

    private void ApplySurfaceDiveCameraEffects(bool active)
    {
        ApplySurfaceDiveFieldOfView(active);
        UpdateSurfaceDiveSpeedLines(active);
    }

    private void ApplySurfaceDiveFieldOfView(bool active)
    {
        Camera camera = ResolveRigCamera();
        if (camera == null)
        {
            return;
        }

        float defaultFov = cinematicDefaultFieldOfView > 0f ? cinematicDefaultFieldOfView : camera.fieldOfView;
        float targetFov = active
            ? defaultFov + Mathf.Max(0f, surfaceDiveFieldOfViewKick)
            : defaultFov;
        float smooth = active ? surfaceDiveFieldOfViewSmooth : cinematicFieldOfViewSmooth;
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, smooth) * Time.deltaTime);
        camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, targetFov, blend);
    }

    private void UpdateSurfaceDiveSpeedLines(bool active)
    {
        if (!showSurfaceDiveSpeedLines || !active)
        {
            SetSurfaceDiveSpeedLinesVisible(false);
            return;
        }

        EnsureSurfaceDiveSpeedLines();
        if (surfaceDiveSpeedLines == null || surfaceDiveSpeedLines.Length == 0)
        {
            return;
        }

        SetSurfaceDiveSpeedLinesVisible(true);
        Camera camera = ResolveRigCamera();
        float distance = Mathf.Max(0.35f, surfaceDiveSpeedLineDistance);
        float aspect = camera != null ? Mathf.Max(0.2f, camera.aspect) : 16f / 9f;
        float fov = camera != null ? camera.fieldOfView : 60f;
        float halfHeight = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f) * distance;
        float halfWidth = halfHeight * aspect;
        float duration = Mathf.Max(0.35f, surfaceDiveDuration);
        float diveProgress = surfaceDiveArcActive
            ? Mathf.Clamp01((Time.time - surfaceDiveStartedAt) / duration)
            : 1f;
        float intensity = Mathf.Lerp(0.58f, 1f, Mathf.Sin(diveProgress * Mathf.PI));
        float baseWidth = Mathf.Max(0.002f, surfaceDiveSpeedLineWidth);
        float length = Mathf.Clamp(surfaceDiveSpeedLineLength, 0.1f, 0.82f);

        for (int i = 0; i < surfaceDiveSpeedLines.Length; i++)
        {
            LineRenderer line = surfaceDiveSpeedLines[i];
            if (line == null)
            {
                continue;
            }

            float angle = (i + PseudoRandom01(i, 0.17f) * 0.28f) / surfaceDiveSpeedLines.Length * Mathf.PI * 2f;
            float stream = Mathf.Repeat(Time.time * 3.4f + PseudoRandom01(i, 0.43f), 1f);
            float edgeScale = Mathf.Lerp(0.72f, 1.1f, stream);
            float lineLength = length * Mathf.Lerp(0.75f, 1.28f, PseudoRandom01(i, 0.67f)) * intensity;
            float x = Mathf.Cos(angle) * halfWidth * edgeScale;
            float y = Mathf.Sin(angle) * halfHeight * edgeScale;
            Vector3 outer = new Vector3(x, y, distance);
            Vector3 inner = new Vector3(x * (1f - lineLength), y * (1f - lineLength), distance + 0.08f);
            float alpha = surfaceDiveSpeedLineColor.a * Mathf.Lerp(0.38f, 1f, intensity) * Mathf.Lerp(0.48f, 1f, stream);

            line.positionCount = 2;
            line.startWidth = baseWidth * 0.18f;
            line.endWidth = baseWidth * Mathf.Lerp(0.85f, 1.55f, intensity);
            line.startColor = WithAlpha(surfaceDiveSpeedLineColor, alpha * 0.12f);
            line.endColor = WithAlpha(surfaceDiveSpeedLineColor, alpha);
            line.SetPosition(0, outer);
            line.SetPosition(1, inner);
            line.enabled = true;
        }
    }

    private void EnsureSurfaceDiveSpeedLines()
    {
        int count = Mathf.Clamp(surfaceDiveSpeedLineCount, 0, 48);
        if (count <= 0)
        {
            return;
        }

        if (surfaceDiveSpeedLineRoot == null)
        {
            GameObject rootObject = new GameObject("Surface Dive Speed Lines");
            surfaceDiveSpeedLineRoot = rootObject.transform;
            surfaceDiveSpeedLineRoot.SetParent(transform, false);
            surfaceDiveSpeedLineRoot.localPosition = Vector3.zero;
            surfaceDiveSpeedLineRoot.localRotation = Quaternion.identity;
            surfaceDiveSpeedLineRoot.localScale = Vector3.one;
        }

        if (surfaceDiveSpeedLines == null || surfaceDiveSpeedLines.Length != count)
        {
            surfaceDiveSpeedLines = new LineRenderer[count];
        }

        Material material = EnsureSurfaceDiveSpeedLineMaterial();
        for (int i = 0; i < surfaceDiveSpeedLines.Length; i++)
        {
            if (surfaceDiveSpeedLines[i] != null)
            {
                continue;
            }

            GameObject lineObject = new GameObject($"Dive Speed Line {i + 1:00}");
            lineObject.transform.SetParent(surfaceDiveSpeedLineRoot, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            surfaceDiveSpeedLines[i] = line;
        }
    }

    private Material EnsureSurfaceDiveSpeedLineMaterial()
    {
        if (surfaceDiveSpeedLineMaterial != null)
        {
            return surfaceDiveSpeedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        surfaceDiveSpeedLineMaterial = shader != null
            ? new Material(shader)
            : null;
        if (surfaceDiveSpeedLineMaterial != null)
        {
            surfaceDiveSpeedLineMaterial.name = "Generated Surface Dive Speed Lines";
            surfaceDiveSpeedLineMaterial.renderQueue = 4100;
            if (surfaceDiveSpeedLineMaterial.HasProperty("_Color"))
            {
                surfaceDiveSpeedLineMaterial.SetColor("_Color", Color.white);
            }

            if (surfaceDiveSpeedLineMaterial.HasProperty("_BaseColor"))
            {
                surfaceDiveSpeedLineMaterial.SetColor("_BaseColor", Color.white);
            }
        }

        return surfaceDiveSpeedLineMaterial;
    }

    private void SetSurfaceDiveSpeedLinesVisible(bool visible)
    {
        if (surfaceDiveSpeedLineRoot != null)
        {
            surfaceDiveSpeedLineRoot.gameObject.SetActive(visible);
        }
    }

    private Camera ResolveRigCamera()
    {
        if (rigCamera == null)
        {
            rigCamera = GetComponent<Camera>();
        }

        return rigCamera;
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

    private float CinematicVariant01(float salt)
    {
        return PseudoRandom01(Mathf.RoundToInt(cinematicShotVariantSeed * 10f), salt);
    }

    private float CinematicVariantSigned(float salt)
    {
        return CinematicVariant01(salt) * 2f - 1f;
    }

    private float CinematicVariantSign(float salt)
    {
        return CinematicVariant01(salt) < 0.5f ? -1f : 1f;
    }

    private static float PseudoRandom01(int index, float salt)
    {
        return Mathf.Repeat(Mathf.Sin(index * 12.9898f + salt * 78.233f) * 43758.5453f, 1f);
    }

    private static float SurfaceDiveProgress01(float value)
    {
        float t = Mathf.Clamp01(value);
        return 1f - Mathf.Pow(1f - t, 2.45f);
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

    private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return inverse * inverse * p0 + 2f * inverse * t * p1 + t * t * p2;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static bool IsDroneStyleShot(OceanCinematicShotKind shot)
    {
        return shot == OceanCinematicShotKind.DroneOverview;
    }

    private static bool IsUnderwaterScenicShot(OceanCinematicShotKind shot)
    {
        return shot == OceanCinematicShotKind.UnderwaterExplore
            || shot == OceanCinematicShotKind.ReefDive
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
        lookTarget = Vector3.Lerp(lookTarget, currentFocusPoint, FocusedFishCenteringBlend());

        bool surfaceDive = TryShapeSurfaceDive(ref desiredPosition, focusForward);
        if (surfaceDive)
        {
            focusTransitionActive = false;
        }
        else
        {
            ApplyFocusTransition(ref desiredPosition, ref lookTarget);
        }

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
        ApplySurfaceDiveCameraEffects(surfaceDive);
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
        if (surfaceDiveArcActive)
        {
            if (!targetIsUnderwater)
            {
                surfaceDiveArcActive = false;
                return false;
            }

            UpdateSurfaceDiveArc(ref desiredPosition, focusForward, waterY);
            return true;
        }

        if (!startsAtSurfaceOrAbove || !targetIsUnderwater)
        {
            return false;
        }

        desiredPosition = ForceSurfaceDiveTargetDepth(desiredPosition, waterY);
        BeginSurfaceDiveArc(desiredPosition, focusForward);
        UpdateSurfaceDiveArc(ref desiredPosition, focusForward, waterY);
        return true;
    }

    private void BeginSurfaceDiveArc(Vector3 desiredPosition, Vector3 focusForward)
    {
        surfaceDiveArcActive = true;
        surfaceDiveStartedAt = Time.time;
        surfaceDiveStartPosition = transform.position;
        surfaceDiveTargetPosition = desiredPosition;
        positionVelocity = Vector3.zero;

        Vector3 targetDirection = FlattenDirection(surfaceDiveTargetPosition - surfaceDiveStartPosition);
        if (targetDirection.sqrMagnitude < 0.001f)
        {
            targetDirection = FlattenDirection(focusForward);
        }

        Vector3 lateralDirection = Vector3.Cross(Vector3.up, targetDirection);
        if (lateralDirection.sqrMagnitude < 0.001f)
        {
            lateralDirection = transform.right;
        }
        else
        {
            lateralDirection.Normalize();
        }

        if (Vector3.Dot(lateralDirection, transform.right) < 0f)
        {
            lateralDirection = -lateralDirection;
        }

        Vector3 midpoint = Vector3.Lerp(surfaceDiveStartPosition, surfaceDiveTargetPosition, 0.46f);
        surfaceDiveControlPosition = midpoint
            + targetDirection * Mathf.Max(0f, surfaceDiveArcForwardOffset * 0.48f)
            + lateralDirection * Mathf.Max(0f, surfaceDiveDiagonalOffset);
        float verticalDrop = Mathf.Max(0f, surfaceDiveStartPosition.y - surfaceDiveTargetPosition.y);
        float descendingArcY = Mathf.Lerp(surfaceDiveStartPosition.y, surfaceDiveTargetPosition.y, 0.62f)
            + Mathf.Min(Mathf.Max(0f, surfaceDiveArcHeight), verticalDrop * 0.06f);
        float highestControlY = Mathf.Max(
            surfaceDiveTargetPosition.y + 1.5f,
            surfaceDiveStartPosition.y - Mathf.Max(0.8f, verticalDrop * 0.18f)
        );
        surfaceDiveControlPosition.y = Mathf.Clamp(
            descendingArcY,
            surfaceDiveTargetPosition.y + 1.15f,
            highestControlY
        );
    }

    private void UpdateSurfaceDiveArc(ref Vector3 desiredPosition, Vector3 focusForward, float waterY)
    {
        Vector3 latestTarget = ForceSurfaceDiveTargetDepth(desiredPosition, waterY);
        surfaceDiveTargetPosition = Vector3.Lerp(surfaceDiveTargetPosition, latestTarget, 0.18f);

        float duration = Mathf.Max(0.35f, surfaceDiveDuration);
        float normalizedTime = Mathf.Clamp01((Time.time - surfaceDiveStartedAt) / duration);
        float eased = SurfaceDiveProgress01(normalizedTime);
        desiredPosition = QuadraticBezier(surfaceDiveStartPosition, surfaceDiveControlPosition, surfaceDiveTargetPosition, eased);

        if (normalizedTime >= 1f)
        {
            surfaceDiveArcActive = false;
        }
    }

    private Vector3 ForceSurfaceDiveTargetDepth(Vector3 position, float waterY)
    {
        float minimumDepth = Mathf.Max(surfaceDiveDepthTrigger + 0.6f, surfaceDiveMinimumTargetDepth);
        position.y = Mathf.Min(position.y, waterY - minimumDepth);
        return position;
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
        Vector3 forward = FlattenDirection(cruiseDestination - transform.position);
        bool surfaceDive = TryShapeSurfaceDive(ref desiredPosition, forward);
        float moveSmoothTime = surfaceDive
            ? Mathf.Min(positionSmoothTime * 1.1f, Mathf.Max(0.05f, surfaceDiveSmoothTime))
            : positionSmoothTime * 1.1f;
        float moveSpeed = surfaceDive
            ? Mathf.Max(surfaceDiveMaxSpeed, Mathf.Max(0.1f, maximumCameraSpeed))
            : Mathf.Min(relaxedCameraSpeed, Mathf.Max(0.1f, maximumCameraSpeed));

        bool snapped = MoveCamera(
            desiredPosition,
            moveSmoothTime,
            moveSpeed
        );

        Vector3 routeLookTarget = transform.position
            + forward * lookAhead
            + Vector3.up * Mathf.Sin(driftTime) * 0.2f;
        routeLookTarget = ApplyRoamLookSweep(transform.position, routeLookTarget, forward, SpawnTimeSeed() * 0.27f);
        Vector3 lookTarget = target != null
            ? target.position
            : BlendRoamLookTargetTowardInterest(transform.position, routeLookTarget, forward, cruiseInterestLookBlend);
        LookAtTarget(lookTarget, rotationSmooth * 0.72f, snapped);
        ApplySurfaceDiveCameraEffects(surfaceDive);
    }

    private void PickCruiseDestination()
    {
        float t = Time.time / Mathf.Max(1f, orbitSeconds * 1.35f) * Mathf.PI * 2f;
        OceanEnvironment environment = ResolveOceanEnvironment();
        if (environment != null)
        {
            Vector2 roamSize = environment.ActiveAreaSize;
            cruiseDestination = OceanLocalToWorld(
                environment,
                new Vector3(
                    Mathf.Sin(t + Random.Range(-0.65f, 0.65f)) * roamSize.x * 0.46f,
                    environment.WaterSurfaceY - Random.Range(4.8f, 7.4f),
                    Mathf.Cos(t * 0.82f + Random.Range(-0.65f, 0.65f)) * roamSize.y * 0.46f
                )
            );
        }
        else
        {
            cruiseDestination = center + new Vector3(
                Mathf.Sin(t + Random.Range(-0.65f, 0.65f)) * orbitSize.x * 0.9f,
                Mathf.Sin(t * 0.37f + Random.Range(-0.4f, 0.4f)) * orbitSize.y * 0.55f,
                Mathf.Cos(t * 0.82f + Random.Range(-0.65f, 0.65f)) * orbitSize.z * 0.9f
            );
        }

        if (TryFindRoamInterestTarget(out Vector3 interestPoint, out float interestStrength))
        {
            Vector3 fromInterest = transform.position - interestPoint;
            Vector3 approachDirection = fromInterest.sqrMagnitude > 0.001f
                ? FlattenDirection(fromInterest)
                : -FlattenDirection(cruiseDestination - transform.position);
            Vector3 interestDestination = interestPoint + approachDirection * Mathf.Lerp(13f, 7f, interestStrength);
            interestDestination.y = Mathf.Lerp(cruiseDestination.y, interestPoint.y + 0.8f, 0.55f);
            cruiseDestination = Vector3.Lerp(
                cruiseDestination,
                interestDestination,
                Mathf.Clamp01(cruiseInterestApproachBlend * interestStrength)
            );
        }

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
        float underwaterY = SampleWaterSurfaceWorldY(environment)
            - Mathf.Max(MinimumUnderwaterSurfaceDepth, surfaceDiveMinimumTargetDepth);
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
        focusedFishObserveDistanceReachedAt = 0f;
        waitingForFocusedFishTag = focusedFish != null;
        focusedFishSinceTime = 0f;
        intentUntilTime = Time.time + Mathf.Max(ScanDelay(), pendingFishObserveSeconds);
    }

    private void UpdateFocusedFishObservationTimer()
    {
        if (!waitingForFocusedFishTag
            || focusedFish == null
            || (intent != DiverIntent.ApproachFish && intent != DiverIntent.DriftPast))
        {
            return;
        }

        if (!HasReachedFocusedFishObserveStartDistance())
        {
            focusedFishObserveDistanceReachedAt = 0f;
            return;
        }

        if (focusedFishObserveDistanceReachedAt <= 0f)
        {
            focusedFishObserveDistanceReachedAt = Time.time;
        }

        bool tagVisible = focusedFish.IsNicknameTagVisibleForCamera;
        bool waitExpired = Time.time >= focusedFishObserveDistanceReachedAt + Mathf.Max(0.5f, focusObserveWaitForTagSeconds);
        if (!tagVisible && !waitExpired)
        {
            return;
        }

        StartFocusedFishObservationTimer();
    }

    private bool HasReachedFocusedFishObserveStartDistance()
    {
        if (focusedFish == null)
        {
            return false;
        }

        float radiusPadding = Mathf.Max(0f, currentFocusRadius) * 0.28f;
        float startDistance = Mathf.Max(0.5f, fishObserveStartDistance + radiusPadding);
        return Vector3.Distance(transform.position, focusedFish.VisualCenter) <= startDistance;
    }

    private void StartFocusedFishObservationTimer()
    {
        waitingForFocusedFishTag = false;
        focusedFishSinceTime = Time.time;
        intentUntilTime = Time.time + Mathf.Max(1f, pendingFishObserveSeconds);
    }

    private bool ShouldHoldCurrentIntent()
    {
        if ((intent == DiverIntent.ApproachFish || intent == DiverIntent.DriftPast)
            && focusedFish != null
            && waitingForFocusedFishTag)
        {
            return true;
        }

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
        focusedFishObserveDistanceReachedAt = 0f;

        if (focusedFish != null)
        {
            focusedFish.SetCameraFocused(true);
            focusedFishSinceTime = 0f;
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
        bool canSnap = snap && !hasLastLookTarget;
        lastLookTarget = lookTarget;
        hasLastLookTarget = true;

        if (canSnap)
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

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        Quaternion blendedRotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Mathf.Clamp01(smooth * Time.deltaTime)
        );
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            blendedRotation,
            Mathf.Max(8f, cameraMaxTurnDegreesPerSecond) * Time.deltaTime
        );
    }
}
