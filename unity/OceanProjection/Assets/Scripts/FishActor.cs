using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class FishActor : MonoBehaviour
{
    private static readonly List<FishActor> ActiveFishes = new List<FishActor>();
    private const int MaxSchoolFormationLanes = 11;
    private const int UnassignedSchoolGroupId = -1;

    [Header("Visual")]
    [SerializeField] private Renderer[] colorRenderers;
    [SerializeField] private Renderer[] subColorRenderers;
    [SerializeField] private Renderer[] textureRenderers;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private TMP_Text nicknameLabel;
    [SerializeField] private bool createNicknameLabelWhenMissing = true;
    [SerializeField] private float focusedLabelVisibleDistance = 140f;
    [SerializeField] private float nearbyLabelVisibleDistance = 48f;
    [SerializeField] private float labelForwardConeDistance = 96f;
    [SerializeField] private float labelForwardConeAngle = 70f;
    [SerializeField] private bool showDefaultNicknameWhenNearby = false;
    [SerializeField] private float defaultNearbyLabelVisibleDistance = 12f;
    [SerializeField] private float defaultForwardConeLabelDistance = 18f;
    [SerializeField] private float defaultFocusedLabelVisibleDistance = 28f;
    [SerializeField] private Vector2 nicknameTagOffset = new Vector2(0.075f, 0.095f);
    [SerializeField] private float nicknameTagAnchorLift = 0.18f;
    [SerializeField] private float nicknameTagLineWidth = 0.0032f;
    [SerializeField] private float nicknameTagFontSize = 0.9f;
    [SerializeField] private float nicknameTagHorizontalLength = 0.18f;
    [SerializeField] private float nicknameTagTextLift = 0.0035f;
    [SerializeField] private float nicknameTagTextViewportHeight = 0.064f;
    [SerializeField] private float nicknameTagTextMaxWidthRatio = 1.05f;
    [SerializeField] private float nicknameTagRevealDistance = 6.2f;
    [SerializeField] private float nicknameTagRevealHysteresisDistance = 0.75f;
    [SerializeField] private float nicknameTagMinApparentRadiusViewport = 0.045f;
    [SerializeField] private float nicknameTagNearScaleDistance = 1.2f;
    [SerializeField] private float nicknameTagFarScaleDistance = 9.5f;
    [SerializeField] private Vector2 nicknameTagDistanceScaleRange = new Vector2(0.82f, 1.12f);
    [SerializeField] private float nicknameTagMinFitScale = 0.48f;
    [SerializeField] private float nicknameTagRevealSeconds = 0.68f;
    [SerializeField] private float nicknameTagRetreatSeconds = 0.26f;
    [SerializeField] private float nicknameTagViewportPadding = 0.055f;
    [SerializeField] private bool useBuiltInNicknameFallback = true;
    [SerializeField] private bool logNicknameTagDebug = true;
    [SerializeField] private float nicknameTagDebugInterval = 4f;
    [SerializeField] private bool remapDrawingTextureForModel = true;
    [SerializeField] private int remappedDrawingTextureSize = 512;
    [SerializeField] private float drawingAlphaThreshold = 0.05f;
    [SerializeField] private bool flipReleasedDrawingHorizontally = true;
    [SerializeField] private Vector2 drawingProjectionPaddingRatio = new Vector2(0.16f, 0.18f);

    [Header("Movement")]
    [SerializeField] private float baseSpeed = 1.18f;
    [SerializeField] private float turnSpeed = 3.2f;
    [SerializeField] private float labelVisibleDistance = 72f;
    [SerializeField] private float boundsPadding = 0.7f;
    [SerializeField] private float acceleration = 2.65f;
    [SerializeField] private float turnSlowdown = 0.58f;
    [SerializeField] private bool autoWireRenderers = true;
    [SerializeField] private float boundsAvoidancePadding = 5.4f;
    [SerializeField] private float boundsAvoidanceWeight = 4.1f;
    [SerializeField] private float boundsReturnWeight = 6.2f;
    [SerializeField] private float edgeTurnBoost = 3.4f;
    [SerializeField] private float edgeMovementSteer = 0.78f;

    [Header("Natural Swim")]
    [SerializeField] private float targetReachDistance = 2.05f;
    [SerializeField] private float slowSwimMultiplier = 0.92f;
    [SerializeField] private float fastSwimMultiplier = 1.72f;
    [SerializeField] private float fastSwimChance = 0.28f;
    [SerializeField] private Vector2 slowSwimSecondsRange = new Vector2(3.5f, 7f);
    [SerializeField] private Vector2 fastSwimSecondsRange = new Vector2(0.9f, 2.2f);
    [SerializeField] private float waypointForwardBias = 0.18f;
    [SerializeField] private float defaultFishTargetAreaScale = 0.58f;
    [SerializeField] private float releasedFishTargetAreaScale = 0.72f;
    [SerializeField] private float targetTurnAngle = 126f;
    [SerializeField] private Vector2 targetDistanceRange = new Vector2(0.12f, 0.3f);
    [SerializeField] private float targetSideSlip = 0.28f;
    [SerializeField] private Vector2 wanderTurnSecondsRange = new Vector2(0.45f, 1.25f);
    [SerializeField] private float wanderTurnAngle = 108f;
    [SerializeField] private float wanderTurnWeight = 0.72f;

    [Header("Animation Sync")]
    [SerializeField] private float animationSpeedAtBaseSwim = 1f;
    [SerializeField] private float animationSpeedMultiplier = 0.92f;
    [SerializeField] private float minAnimationSpeed = 0.72f;
    [SerializeField] private float maxAnimationSpeed = 1.28f;
    [SerializeField] private float animationSmooth = 4.5f;
    [SerializeField] private float slowTailSwayDegrees = 2.4f;
    [SerializeField] private float fastTailSwayDegrees = 8.5f;
    [SerializeField] private float tailSwayFrequency = 4.8f;
    [SerializeField] private float proceduralSpineSwayMultiplier = 0.32f;
    [SerializeField] private float proceduralTailSwayMultiplier = 1.35f;

    [Header("Schooling")]
    [SerializeField] private bool enableSchooling = true;
    [SerializeField] private float neighborRadius = 6.8f;
    [SerializeField] private float separationRadius = 1.2f;
    [SerializeField] private float alignmentWeight = 0.48f;
    [SerializeField] private float cohesionWeight = 0.58f;
    [SerializeField] private float separationWeight = 0.95f;
    [SerializeField] private float schoolUpdateSeconds = 0.12f;
    [SerializeField] private float schoolModeChance = 0.92f;
    [SerializeField] private Vector2 schoolingSecondsRange = new Vector2(5.5f, 12f);
    [SerializeField] private Vector2 soloSecondsRange = new Vector2(3f, 8f);
    [SerializeField] private float schoolingBlendSpeed = 1.8f;
    [SerializeField] private float soloSeparationWeight = 0.55f;
    [SerializeField] private float verticalSchoolingWeight = 0.04f;
    [SerializeField] private float verticalSeparationRadius = 0.9f;
    [SerializeField] private float verticalSeparationWeight = 0.82f;
    [SerializeField] private float preferredDepthDrift = 0.06f;
    [SerializeField] private float maxVerticalSwimDirection = 0.2f;
    [SerializeField] private float sameColumnAvoidanceRadius = 1.2f;
    [SerializeField] private float sameColumnAvoidanceWeight = 1.5f;
    [SerializeField] private Vector2 schoolSlotSideRange = new Vector2(-2.6f, 2.6f);
    [SerializeField] private Vector2 schoolSlotForwardRange = new Vector2(-1.8f, 1.8f);
    [SerializeField] private Vector2 schoolSlotDepthRange = new Vector2(-0.52f, 0.52f);
    [SerializeField] private float schoolVisionAngle = 210f;
    [SerializeField] private float verticalCohesionScale = 0.22f;
    [SerializeField] private float releasedFishSchoolingMultiplier = 0.12f;
    [SerializeField] private float releasedFishNeighborWeight = 0.2f;
    [SerializeField] private float schoolNoiseWeight = 0.12f;
    [SerializeField] private float schoolGatherRadius = 8.5f;
    [SerializeField] private float schoolGatherDeadZone = 2.6f;
    [SerializeField] private float schoolGatherWeight = 0.38f;
    [SerializeField] private float schoolGatherForwardWeight = 0.12f;
    [SerializeField] private float schoolGroupHomeWeight = 0.32f;

    [Header("Awareness")]
    [SerializeField] private float cameraAwarenessDistance = 5.5f;
    [SerializeField] private float cameraLookWeight = 0.32f;
    [SerializeField] private float cameraAvoidanceWeight = 1.15f;
    [SerializeField] private float curiousLookSeconds = 1.15f;

    [Header("Terrain Avoidance")]
    [SerializeField] private bool avoidSeabed = true;
    [SerializeField] private float seabedClearance = 0.65f;
    [SerializeField] private float seabedLookAhead = 2.4f;
    [SerializeField] private float seabedAvoidanceWeight = 2.2f;
    [SerializeField] private float seabedSideProbeDistance = 1.4f;

    private Vector3 targetPosition;
    private Vector3 swimCenter;
    private Vector3 swimSize = new Vector3(16f, 7f, 10f);
    private Vector3 initialModelScale = Vector3.one;
    private Vector3 schoolDirection;
    private Vector3 schoolFormationOffset;
    private Vector3 wanderDirection = Vector3.forward;
    private Vector3 schoolGroupCenter;
    private Vector3 schoolGroupForward = Vector3.forward;
    private float schoolGroupRadius = 4f;
    private Quaternion baseModelLocalRotation = Quaternion.identity;
    private Transform proceduralTailRoot;
    private Transform proceduralSpineRoot;
    private Quaternion proceduralTailBaseLocalRotation = Quaternion.identity;
    private Quaternion proceduralSpineBaseLocalRotation = Quaternion.identity;
    private Animator[] animators = new Animator[0];
    private float[] animatorBaseSpeeds = new float[0];
    private bool[] animatorHasSwimSpeedParameter = new bool[0];
    private bool[] animatorHasTurnParameter = new bool[0];
    private float currentSpeed;
    private float currentAnimationSpeed = 1f;
    private float currentSwimEffort = 0.5f;
    private float currentBodySwayDegrees;
    private float currentTailSwayDegrees;
    private float currentSpeedMultiplier = 1f;
    private float schoolingNoiseSeed;
    private float nextSpeedModeTime;
    private float nextWanderTurnTime;
    private float activeTargetReachDistance;
    private float nextSchoolUpdateTime;
    private float nextSchoolModeTime;
    private float currentSchoolStrength;
    private float curiousLookUntil;
    private string species = "original";
    private string personality = "calm";
    private string appliedTextureUrl = "";
    private Camera mainCamera;
    private OceanEnvironment oceanEnvironment;
    private bool releasedFish;
    private bool cameraFocused;
    private int schoolGroupId = UnassignedSchoolGroupId;
    private LineRenderer nicknameTagLine;
    private TextMesh nicknameFallbackLabel;
    private Renderer nicknameFallbackRenderer;
    private bool isSchoolingMode;
    private float initialBaseSpeed;
    private float initialSchoolModeChance;
    private Coroutine textureCoroutine;
    private DrawingFishVisual drawingFishVisual;
    private Material[] projectedDrawingMaterials = new Material[0];
    private Transform drawingProjectionRoot;
    private static bool warnedMissingTmpResources;
    private float nextNicknameTagDebugTime;
    private float nicknameTagRevealProgress;
    private bool hasStableNicknameTagLayout;
    private int stableNicknameTagHorizontalSide = 1;
    private int stableNicknameTagTextSide = 1;
    private float stableNicknameTagFitScale = 1f;
    private float nextOceanEnvironmentSearchTime;

    public float SpawnTime { get; private set; }
    public string Nickname { get; private set; } = "";
    public string SourceId { get; private set; } = "";
    public bool IsReleasedFish => releasedFish;
    public float CameraFocusRadius => EstimateFocusRadius();
    public Vector3 VisualCenter => EstimateVisualCenter();
    public static IReadOnlyList<FishActor> AllActiveFishes => ActiveFishes;

    public bool TryGetVisualBounds(out Bounds bounds)
    {
        Renderer[] renderers = colorRenderers != null && colorRenderers.Length > 0
            ? colorRenderers
            : GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        Renderer labelRenderer = nicknameLabel != null ? nicknameLabel.GetComponent<Renderer>() : null;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (renderer == labelRenderer || renderer == nicknameFallbackRenderer || renderer == nicknameTagLine)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    public string DescribeVisualState()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        int enabledCount = 0;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (renderer.enabled)
            {
                enabledCount++;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds
            ? $"renderers={renderers.Length}, enabled={enabledCount}, boundsCenter={bounds.center}, boundsSize={bounds.size}, scale={transform.lossyScale}"
            : $"renderers={renderers.Length}, enabled={enabledCount}, bounds=none, scale={transform.lossyScale}";
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        oceanEnvironment = FindAnyObjectByType<OceanEnvironment>();
        SpawnTime = Time.time;
        initialBaseSpeed = baseSpeed;
        initialSchoolModeChance = schoolModeChance;
        schoolingNoiseSeed = Random.Range(0f, 1000f);
        RemoveLegacyDrawingBillboards();
        AutoWireVisuals();
        CacheProceduralAnimationBones();
        PickSchoolSlot();
        PickWanderDirection(true);
        CacheAnimators();
        PickNextSchoolingMode(true);
        PickNextSpeedMode(true);
        PickNextTarget();
    }

    private void OnEnable()
    {
        if (!ActiveFishes.Contains(this))
        {
            ActiveFishes.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveFishes.Remove(this);
    }

    public void SetReleasedFish(bool value)
    {
        releasedFish = value;
        if (releasedFish)
        {
            schoolDirection = Vector3.zero;
            currentSchoolStrength *= Mathf.Clamp01(releasedFishSchoolingMultiplier);
            EnsureReleasedFishMaterials();
        }

        HideNicknameTag();
    }

    public void SetCameraFocused(bool value)
    {
        cameraFocused = value;
        if (!cameraFocused)
        {
            nicknameTagRevealProgress = 0f;
            ResetStableNicknameTagLayout();
            HideNicknameTag();
        }
    }

    public void SetSwimBounds(Vector3 center, Vector3 size)
    {
        swimCenter = center;
        swimSize = new Vector3(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y),
            Mathf.Max(1f, size.z)
        );
        PickNextTarget();
    }

    public void ConfigureSchoolGroup(int groupId, Vector3 groupCenter, Vector3 groupForward, float groupRadius)
    {
        schoolGroupId = groupId;
        schoolGroupCenter = groupCenter;
        schoolGroupForward = StableHorizontalDirection(groupForward, transform.forward);
        schoolGroupRadius = Mathf.Max(1.5f, groupRadius);
        PickSchoolSlot();
        PickWanderDirection(true);
    }

    public void Apply(FishData data)
    {
        if (data == null)
        {
            return;
        }

        SourceId = string.IsNullOrWhiteSpace(data.id) ? SourceId : data.id.Trim();
        species = string.IsNullOrWhiteSpace(data.species) ? "original" : data.species;
        personality = string.IsNullOrWhiteSpace(data.personality) ? "calm" : data.personality;
        Nickname = SanitizeNickname(data.nickname);

        ApplyColor(colorRenderers, ParseColor(data.main_color, Color.cyan));
        ApplyColor(subColorRenderers, ParseColor(data.sub_color, Color.white));
        ApplyRemoteTexture(data.texture_url);

        Transform scaleRoot = modelRoot != null ? modelRoot : transform;
        scaleRoot.localScale = initialModelScale * SizeToScale(data.size);

        if (nicknameLabel != null)
        {
            nicknameLabel.text = Nickname;
            nicknameLabel.gameObject.SetActive(false);
        }

        if (nicknameFallbackLabel != null)
        {
            nicknameFallbackLabel.text = Nickname;
            nicknameFallbackLabel.gameObject.SetActive(false);
        }

        baseSpeed = initialBaseSpeed * PersonalitySpeedMultiplier(personality);
        float schoolingMultiplier = PersonalitySchoolingMultiplier(personality);
        if (releasedFish)
        {
            schoolingMultiplier *= Mathf.Clamp01(releasedFishSchoolingMultiplier);
        }

        schoolModeChance = Mathf.Clamp01(initialSchoolModeChance * schoolingMultiplier);
    }

    private void Update()
    {
        Swim();
        UpdateLabel();
    }

    private void LateUpdate()
    {
        ApplyProceduralSwimPose();
        UpdateDrawingProjectionMatrix();
    }

    private void Swim()
    {
        if (!IsInsideSwimBounds(transform.position, boundsPadding))
        {
            PickBoundsRecoveryTarget();
        }

        if (species == "jellyfish")
        {
            Vector3 floatMotion = new Vector3(
                Mathf.Sin(Time.time * 0.45f + transform.position.x) * 0.25f,
                Mathf.Sin(Time.time * 0.8f) * 0.45f + 0.22f,
                Mathf.Cos(Time.time * 0.35f + transform.position.z) * 0.22f
            );
            transform.position += floatMotion * Time.deltaTime;
            transform.position = ClampToSwimBounds(transform.position, 0f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(floatMotion.x, 0f, floatMotion.z).normalized + Vector3.forward),
                turnSpeed * 0.35f * Time.deltaTime
            );
            return;
        }

        Vector3 toTarget = targetPosition - transform.position;
        if (toTarget.magnitude < activeTargetReachDistance)
        {
            PickNextTarget();
            toTarget = targetPosition - transform.position;
        }

        if (Time.time >= nextSpeedModeTime)
        {
            PickNextSpeedMode(false);
        }

        if (Time.time >= nextWanderTurnTime)
        {
            PickWanderDirection(false);
        }

        UpdateSchoolingMode();

        Vector3 desiredDirection = toTarget.normalized;
        desiredDirection = BlendWanderDirection(desiredDirection);
        desiredDirection = BlendSchoolingDirection(desiredDirection);
        desiredDirection = BlendCameraAwareness(desiredDirection);
        desiredDirection = BlendBoundsAvoidance(desiredDirection);
        desiredDirection = BlendSeabedAvoidance(desiredDirection);
        desiredDirection = LimitVerticalSwim(desiredDirection);
        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            desiredDirection = transform.forward;
        }

        float edgePressure = HorizontalEdgePressure(transform.position, boundsAvoidancePadding);
        Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            turnSpeed * Mathf.Lerp(1f, edgeTurnBoost, edgePressure) * Time.deltaTime
        );

        float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDirection.normalized));
        float targetSpeed = baseSpeed * currentSpeedMultiplier * Mathf.Lerp(turnSlowdown, 1f, alignment);
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        Vector3 swimDirection = transform.forward;
        if (edgePressure > 0.001f)
        {
            swimDirection = Vector3.Slerp(
                transform.forward,
                desiredDirection.normalized,
                Mathf.Clamp01(edgePressure * edgeMovementSteer)
            ).normalized;
        }

        Vector3 nextPosition = transform.position + swimDirection * currentSpeed * Time.deltaTime;
        Vector3 clampedPosition = ClampAboveSeabed(ClampToSwimBounds(nextPosition, 0f));
        if ((nextPosition - clampedPosition).sqrMagnitude > 0.0001f)
        {
            currentSpeed = Mathf.Max(currentSpeed * 0.45f, baseSpeed * currentSpeedMultiplier * 0.28f);
            schoolDirection = Vector3.Lerp(schoolDirection, Vector3.zero, 0.55f);
            Vector3 recoveryDirection = StableHorizontalDirection(swimCenter - transform.position, -transform.forward);
            wanderDirection = recoveryDirection;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(LimitVerticalSwim(recoveryDirection), Vector3.up),
                Mathf.Clamp01(turnSpeed * edgeTurnBoost * Time.deltaTime)
            );
            PickBoundsRecoveryTarget();
        }

        transform.position = clampedPosition;
        UpdateSwimAnimation();
    }

    private void PickNextTarget()
    {
        if (IsNearHorizontalSwimBoundsEdge(transform.position, boundsAvoidancePadding * 0.72f))
        {
            PickBoundsRecoveryTarget();
            return;
        }

        float areaScale = Mathf.Clamp01(releasedFish ? releasedFishTargetAreaScale : defaultFishTargetAreaScale);
        float horizontalScale = Mathf.Lerp(0.22f, 0.5f, areaScale);
        float verticalScale = Mathf.Lerp(0.16f, 0.42f, areaScale);
        float currentDepthTarget = Mathf.Clamp(
            transform.position.y + Random.Range(-swimSize.y * verticalScale * 0.38f, swimSize.y * verticalScale * 0.38f),
            swimCenter.y - swimSize.y * verticalScale,
            swimCenter.y + swimSize.y * verticalScale
        );

        Vector3 randomPoint = new Vector3(
            Random.Range(swimCenter.x - swimSize.x * horizontalScale, swimCenter.x + swimSize.x * horizontalScale),
            currentDepthTarget,
            Random.Range(swimCenter.z - swimSize.z * horizontalScale, swimCenter.z + swimSize.z * horizontalScale)
        );

        Vector3 roamDirection = PickTargetRoamDirection();
        float reachBase = Mathf.Min(swimSize.x, swimSize.z);
        float roamReach = reachBase * Random.Range(targetDistanceRange.x, targetDistanceRange.y);
        Vector3 sideDirection = Vector3.Cross(Vector3.up, roamDirection);
        if (sideDirection.sqrMagnitude < 0.001f)
        {
            sideDirection = transform.right;
        }

        Vector3 roamPoint = transform.position
            + roamDirection * roamReach
            + sideDirection.normalized * Random.Range(-roamReach * targetSideSlip, roamReach * targetSideSlip);
        roamPoint.y = currentDepthTarget + Random.Range(-swimSize.y * 0.08f, swimSize.y * 0.08f);

        targetPosition = Vector3.Lerp(roamPoint, randomPoint, Mathf.Clamp01(waypointForwardBias));
        targetPosition = BlendTargetTowardSchoolHome(targetPosition, currentDepthTarget);
        targetPosition = ClampToSwimBounds(
            targetPosition,
            Mathf.Max(boundsPadding, boundsAvoidancePadding * 0.42f),
            boundsPadding
        );
        targetPosition = ClampAboveSeabed(targetPosition);
        activeTargetReachDistance = targetReachDistance * Random.Range(0.85f, 1.45f);
    }

    private Vector3 PickTargetRoamDirection()
    {
        Vector3 baseForward = StableHorizontalDirection(transform.forward, schoolGroupForward);
        float yaw = Random.Range(-targetTurnAngle, targetTurnAngle);
        if (Random.value < 0.22f)
        {
            yaw += Random.value < 0.5f ? -Random.Range(70f, 145f) : Random.Range(70f, 145f);
        }

        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * baseForward;
        return StableHorizontalDirection(direction, baseForward);
    }

    private Vector3 BlendTargetTowardSchoolHome(Vector3 target, float depthTarget)
    {
        if (schoolGroupId == UnassignedSchoolGroupId)
        {
            return target;
        }

        Vector3 flatPosition = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatHome = new Vector3(schoolGroupCenter.x, 0f, schoolGroupCenter.z);
        float distanceFromHome = Vector3.Distance(flatPosition, flatHome);
        float homePressure = Mathf.InverseLerp(schoolGroupRadius * 1.15f, schoolGroupRadius * 2.8f, distanceFromHome);
        if (homePressure <= 0.001f)
        {
            return target;
        }

        Vector3 homeTarget = new Vector3(schoolGroupCenter.x, depthTarget, schoolGroupCenter.z);
        return Vector3.Lerp(target, homeTarget, Mathf.Clamp01(homePressure * 0.58f));
    }

    private void PickWanderDirection(bool initial)
    {
        Vector3 baseForward = StableHorizontalDirection(transform.forward, schoolGroupForward);
        float yaw = Random.Range(-wanderTurnAngle, wanderTurnAngle);
        if (!initial && Random.value < 0.18f)
        {
            yaw += Random.value < 0.5f ? -Random.Range(80f, 150f) : Random.Range(80f, 150f);
        }

        Vector3 horizontal = Quaternion.Euler(0f, yaw, 0f) * baseForward;
        wanderDirection = new Vector3(
            horizontal.x,
            Random.Range(-maxVerticalSwimDirection, maxVerticalSwimDirection) * 0.65f,
            horizontal.z
        ).normalized;

        float min = Mathf.Max(0.25f, Mathf.Min(wanderTurnSecondsRange.x, wanderTurnSecondsRange.y));
        float max = Mathf.Max(min, Mathf.Max(wanderTurnSecondsRange.x, wanderTurnSecondsRange.y));
        nextWanderTurnTime = Time.time + (initial ? Random.Range(0f, max) : Random.Range(min, max));
    }

    private Vector3 BlendWanderDirection(Vector3 direction)
    {
        float weight = Mathf.Clamp01(wanderTurnWeight);
        Vector3 blended = direction + wanderDirection * weight;
        return blended.sqrMagnitude > 0.001f ? blended.normalized : direction;
    }

    private static Vector3 StableHorizontalDirection(Vector3 direction, Vector3 fallback)
    {
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude > 0.001f)
        {
            return flat.normalized;
        }

        flat = new Vector3(fallback.x, 0f, fallback.z);
        return flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward;
    }

    private void PickBoundsRecoveryTarget()
    {
        Vector3 inward = swimCenter - transform.position;
        if (inward.sqrMagnitude < 0.001f)
        {
            inward = -transform.forward;
        }

        Vector3 recoveryTarget = transform.position + inward.normalized * Mathf.Max(2f, Mathf.Min(swimSize.x, swimSize.z) * 0.22f);
        recoveryTarget += transform.right * Random.Range(-swimSize.x * 0.05f, swimSize.x * 0.05f);
        recoveryTarget.y = Mathf.Lerp(transform.position.y, swimCenter.y, 0.35f);
        targetPosition = ClampAboveSeabed(ClampToSwimBounds(
            recoveryTarget,
            Mathf.Max(boundsPadding, boundsAvoidancePadding * 0.65f),
            boundsPadding
        ));
        activeTargetReachDistance = targetReachDistance * 0.85f;
    }

    private Vector3 BlendBoundsAvoidance(Vector3 direction)
    {
        Vector3 avoidance = BoundsAvoidanceVector(transform.position);
        if (avoidance.sqrMagnitude < 0.001f)
        {
            return direction;
        }

        Vector3 blended = direction + avoidance;
        return blended.sqrMagnitude > 0.001f ? blended.normalized : direction;
    }

    private Vector3 BoundsAvoidanceVector(Vector3 position)
    {
        Vector3 halfSize = swimSize * 0.5f;
        Vector3 min = swimCenter - halfSize;
        Vector3 max = swimCenter + halfSize;
        float horizontalPadding = Mathf.Clamp(
            Mathf.Max(boundsAvoidancePadding, boundsPadding + 0.1f),
            0.1f,
            Mathf.Max(0.1f, Mathf.Min(halfSize.x, halfSize.z) * 0.95f)
        );
        float verticalPadding = Mathf.Clamp(
            Mathf.Max(boundsPadding + 0.18f, boundsAvoidancePadding * 0.24f),
            0.1f,
            Mathf.Max(0.1f, halfSize.y * 0.58f)
        );

        Vector3 avoidance = Vector3.zero;
        AddAxisBoundsAvoidance(ref avoidance.x, position.x, min.x, max.x, horizontalPadding);
        AddAxisBoundsAvoidance(ref avoidance.y, position.y, min.y, max.y, verticalPadding);
        AddAxisBoundsAvoidance(ref avoidance.z, position.z, min.z, max.z, horizontalPadding);

        if (!IsInsideSwimBounds(position, 0f))
        {
            Vector3 toCenter = swimCenter - position;
            if (toCenter.sqrMagnitude > 0.001f)
            {
                avoidance += toCenter.normalized * boundsReturnWeight;
            }
        }

        avoidance.y *= 0.45f;
        return avoidance * boundsAvoidanceWeight;
    }

    private static void AddAxisBoundsAvoidance(ref float axis, float value, float min, float max, float padding)
    {
        if (value < min + padding)
        {
            axis += Mathf.Clamp01((min + padding - value) / padding);
        }
        else if (value > max - padding)
        {
            axis -= Mathf.Clamp01((value - (max - padding)) / padding);
        }
    }

    private Vector3 BlendSeabedAvoidance(Vector3 direction)
    {
        if (!avoidSeabed || !TryGetSeabedHeight(transform.position, out float floorY, out float waterY))
        {
            return direction;
        }

        float safeClearance = Mathf.Max(0.12f, seabedClearance);
        float currentClearance = transform.position.y - floorY;
        Vector3 forward = StableHorizontalDirection(direction, transform.forward);
        Vector3 aheadPosition = transform.position + forward * Mathf.Max(0.2f, seabedLookAhead);
        float aheadFloorY = floorY;
        TryGetSeabedHeight(aheadPosition, out aheadFloorY, out _);

        float aheadClearance = transform.position.y - aheadFloorY;
        float currentPressure = Mathf.InverseLerp(safeClearance * 2.1f, safeClearance * 0.45f, currentClearance);
        float aheadPressure = Mathf.InverseLerp(safeClearance * 3.1f, safeClearance * 0.65f, aheadClearance);
        if (floorY >= waterY - 0.45f || aheadFloorY >= waterY - 0.45f)
        {
            aheadPressure = Mathf.Max(aheadPressure, 0.95f);
        }

        Vector3 avoidance = Vector3.up * (currentPressure + aheadPressure * 0.74f) * seabedAvoidanceWeight;
        if (aheadPressure > 0.03f)
        {
            avoidance += LowerTerrainEscapeDirection(aheadPosition, forward) * aheadPressure * seabedAvoidanceWeight * 0.7f;
        }

        if (transform.position.y > waterY - 0.42f)
        {
            avoidance += Vector3.down * Mathf.InverseLerp(waterY - 0.42f, waterY + 0.18f, transform.position.y) * 1.2f;
        }

        Vector3 blended = direction + avoidance;
        return blended.sqrMagnitude > 0.001f ? blended.normalized : direction;
    }

    private Vector3 ClampAboveSeabed(Vector3 position)
    {
        if (!avoidSeabed || !TryGetSeabedHeight(position, out float floorY, out float waterY))
        {
            return position;
        }

        float safeClearance = Mathf.Max(0.12f, seabedClearance);
        float minimumY = floorY + safeClearance;
        float maximumY = waterY - 0.38f;
        if (minimumY > maximumY)
        {
            position += LowerTerrainEscapeDirection(position, swimCenter - position) * safeClearance * 0.55f;
            position.y = Mathf.Min(position.y, maximumY);
            return position;
        }

        position.y = Mathf.Clamp(Mathf.Max(position.y, minimumY), swimCenter.y - swimSize.y * 0.5f, maximumY);
        return position;
    }

    private Vector3 LowerTerrainEscapeDirection(Vector3 position, Vector3 fallbackDirection)
    {
        Vector3 forward = StableHorizontalDirection(fallbackDirection, transform.forward);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f)
        {
            right = transform.right;
        }

        right.Normalize();
        float probe = Mathf.Max(0.35f, seabedSideProbeDistance);
        bool hasLeft = TryGetSeabedHeight(position - right * probe, out float leftFloorY, out _);
        bool hasRight = TryGetSeabedHeight(position + right * probe, out float rightFloorY, out _);
        Vector3 side = hasLeft && hasRight && rightFloorY < leftFloorY ? right : -right;
        Vector3 home = StableHorizontalDirection(swimCenter - position, -forward);
        Vector3 escape = side + home * 0.22f;
        return escape.sqrMagnitude > 0.001f ? escape.normalized : side;
    }

    private bool TryGetSeabedHeight(Vector3 worldPosition, out float floorY, out float waterY)
    {
        floorY = 0f;
        waterY = float.PositiveInfinity;
        OceanEnvironment environment = ResolveOceanEnvironment();
        if (environment == null)
        {
            return false;
        }

        Vector3 local = environment.transform.InverseTransformPoint(worldPosition);
        float localFloorY = environment.SampleSeabedHeight(local.x, local.z);
        floorY = environment.transform.TransformPoint(new Vector3(local.x, localFloorY, local.z)).y;
        waterY = environment.transform.TransformPoint(new Vector3(local.x, environment.WaterSurfaceY, local.z)).y;
        return true;
    }

    private OceanEnvironment ResolveOceanEnvironment()
    {
        if (oceanEnvironment != null)
        {
            return oceanEnvironment;
        }

        if (Time.time < nextOceanEnvironmentSearchTime)
        {
            return null;
        }

        nextOceanEnvironmentSearchTime = Time.time + 1f;
        oceanEnvironment = FindAnyObjectByType<OceanEnvironment>();
        return oceanEnvironment;
    }

    private void PickNextSpeedMode(bool initial)
    {
        bool fastSwim = Random.value < fastSwimChance;
        currentSpeedMultiplier = fastSwim
            ? Random.Range(fastSwimMultiplier * 0.86f, fastSwimMultiplier * 1.08f)
            : Random.Range(slowSwimMultiplier * 0.9f, slowSwimMultiplier * 1.12f);

        Vector2 durationRange = fastSwim ? fastSwimSecondsRange : slowSwimSecondsRange;
        nextSpeedModeTime = Time.time + Random.Range(durationRange.x, durationRange.y);

        if (initial)
        {
            currentSpeed = baseSpeed * currentSpeedMultiplier * Random.Range(0.65f, 0.95f);
        }
    }

    private bool IsInsideSwimBounds(Vector3 position, float padding)
    {
        Vector3 halfSize = swimSize * 0.5f;
        return position.x >= swimCenter.x - halfSize.x + padding
            && position.x <= swimCenter.x + halfSize.x - padding
            && position.y >= swimCenter.y - halfSize.y + padding
            && position.y <= swimCenter.y + halfSize.y - padding
            && position.z >= swimCenter.z - halfSize.z + padding
            && position.z <= swimCenter.z + halfSize.z - padding;
    }

    private bool IsNearSwimBoundsEdge(Vector3 position, float padding)
    {
        Vector3 halfSize = swimSize * 0.5f;
        float safePadding = Mathf.Clamp(
            Mathf.Max(0f, padding),
            0f,
            Mathf.Max(0f, Mathf.Min(halfSize.x, halfSize.y, halfSize.z) - 0.1f)
        );

        return position.x <= swimCenter.x - halfSize.x + safePadding
            || position.x >= swimCenter.x + halfSize.x - safePadding
            || position.y <= swimCenter.y - halfSize.y + safePadding
            || position.y >= swimCenter.y + halfSize.y - safePadding
            || position.z <= swimCenter.z - halfSize.z + safePadding
            || position.z >= swimCenter.z + halfSize.z - safePadding;
    }

    private bool IsNearHorizontalSwimBoundsEdge(Vector3 position, float padding)
    {
        Vector3 halfSize = swimSize * 0.5f;
        float safePadding = Mathf.Clamp(
            Mathf.Max(0f, padding),
            0f,
            Mathf.Max(0f, Mathf.Min(halfSize.x, halfSize.z) - 0.1f)
        );

        return position.x <= swimCenter.x - halfSize.x + safePadding
            || position.x >= swimCenter.x + halfSize.x - safePadding
            || position.z <= swimCenter.z - halfSize.z + safePadding
            || position.z >= swimCenter.z + halfSize.z - safePadding;
    }

    private float HorizontalEdgePressure(Vector3 position, float padding)
    {
        Vector3 halfSize = swimSize * 0.5f;
        float safePadding = Mathf.Clamp(
            Mathf.Max(0.1f, padding),
            0.1f,
            Mathf.Max(0.1f, Mathf.Min(halfSize.x, halfSize.z) - 0.1f)
        );
        float minX = swimCenter.x - halfSize.x;
        float maxX = swimCenter.x + halfSize.x;
        float minZ = swimCenter.z - halfSize.z;
        float maxZ = swimCenter.z + halfSize.z;
        float xPressure = Mathf.Max(
            Mathf.InverseLerp(minX + safePadding, minX, position.x),
            Mathf.InverseLerp(maxX - safePadding, maxX, position.x)
        );
        float zPressure = Mathf.Max(
            Mathf.InverseLerp(minZ + safePadding, minZ, position.z),
            Mathf.InverseLerp(maxZ - safePadding, maxZ, position.z)
        );
        return Mathf.Clamp01(Mathf.Max(xPressure, zPressure));
    }

    private Vector3 ClampToSwimBounds(Vector3 position, float inset)
    {
        return ClampToSwimBounds(position, inset, inset);
    }

    private Vector3 ClampToSwimBounds(Vector3 position, float horizontalInset, float verticalInset)
    {
        Vector3 halfSize = swimSize * 0.5f;
        float safeHorizontalInset = Mathf.Clamp(
            Mathf.Max(0f, horizontalInset),
            0f,
            Mathf.Max(0f, Mathf.Min(halfSize.x, halfSize.z) - 0.05f)
        );
        float safeVerticalInset = Mathf.Clamp(
            Mathf.Max(0f, verticalInset),
            0f,
            Mathf.Max(0f, halfSize.y - 0.05f)
        );
        return new Vector3(
            Mathf.Clamp(position.x, swimCenter.x - halfSize.x + safeHorizontalInset, swimCenter.x + halfSize.x - safeHorizontalInset),
            Mathf.Clamp(position.y, swimCenter.y - halfSize.y + safeVerticalInset, swimCenter.y + halfSize.y - safeVerticalInset),
            Mathf.Clamp(position.z, swimCenter.z - halfSize.z + safeHorizontalInset, swimCenter.z + halfSize.z - safeHorizontalInset)
        );
    }

    private void CacheAnimators()
    {
        animators = GetComponentsInChildren<Animator>(true);
        animatorBaseSpeeds = new float[animators.Length];
        animatorHasSwimSpeedParameter = new bool[animators.Length];
        animatorHasTurnParameter = new bool[animators.Length];

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            animatorBaseSpeeds[i] = animator != null
                ? animator.speed * Random.Range(0.9f, 1.12f)
                : 1f;

            if (animator != null)
            {
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animatorHasSwimSpeedParameter[i] = HasAnimatorParameter(animator, SwimSpeedAnimatorParameter);
                animatorHasTurnParameter[i] = HasAnimatorParameter(animator, TurnAnimatorParameter);
                if (animatorHasSwimSpeedParameter[i])
                {
                    animator.SetFloat(SwimSpeedAnimatorParameter, 1f);
                }

                if (animatorHasTurnParameter[i])
                {
                    animator.SetFloat(TurnAnimatorParameter, 0.5f);
                }

                if (animator.runtimeAnimatorController != null && animator.HasState(0, SwimAnimatorState))
                {
                    animator.Play(SwimAnimatorState, 0, Random.value);
                    animator.Update(0f);
                }

                animator.speed = animatorBaseSpeeds[i] * currentAnimationSpeed;
            }
        }
    }

    private float EstimateFocusRadius()
    {
        return TryGetVisualBounds(out Bounds bounds)
            ? Mathf.Clamp(bounds.extents.magnitude, 0.45f, 5f)
            : 1f;
    }

    private Vector3 EstimateVisualCenter()
    {
        return TryGetVisualBounds(out Bounds bounds) ? bounds.center : transform.position;
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
    }

    private static float SizeToScale(string size)
    {
        return size switch
        {
            "small" => 0.82f,
            "large" => 1.18f,
            _ => 1f
        };
    }

    private static float PersonalitySpeedMultiplier(string value)
    {
        return value switch
        {
            "fast" => 1.18f,
            "schooling" => 1.02f,
            _ => 0.95f
        };
    }

    private static float PersonalitySchoolingMultiplier(string value)
    {
        return value switch
        {
            "fast" => 0.78f,
            "schooling" => 1.28f,
            _ => 1f
        };
    }

    private static string SanitizeNickname(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "no name";
        }

        string cleaned = value.Replace("\r", "").Replace("\n", "").Trim();
        return cleaned.Length > 12 ? cleaned.Substring(0, 12) : cleaned;
    }
}
