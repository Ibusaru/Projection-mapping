using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class FishActor : MonoBehaviour
{
    private static readonly List<FishActor> ActiveFishes = new List<FishActor>();

    [Header("Visual")]
    [SerializeField] private Renderer[] colorRenderers;
    [SerializeField] private Renderer[] subColorRenderers;
    [SerializeField] private Renderer[] textureRenderers;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private TMP_Text nicknameLabel;
    [SerializeField] private bool createNicknameLabelWhenMissing = true;
    [SerializeField] private bool remapDrawingTextureForModel = true;
    [SerializeField] private int remappedDrawingTextureSize = 512;
    [SerializeField] private float drawingAlphaThreshold = 0.05f;

    [Header("Movement")]
    [SerializeField] private float baseSpeed = 0.9f;
    [SerializeField] private float turnSpeed = 1.55f;
    [SerializeField] private float labelVisibleDistance = 4.2f;
    [SerializeField] private float boundsPadding = 0.7f;
    [SerializeField] private float acceleration = 2.1f;
    [SerializeField] private float turnSlowdown = 0.58f;
    [SerializeField] private bool autoWireRenderers = true;

    [Header("Natural Swim")]
    [SerializeField] private float targetReachDistance = 1.25f;
    [SerializeField] private float slowSwimMultiplier = 0.82f;
    [SerializeField] private float fastSwimMultiplier = 1.55f;
    [SerializeField] private float fastSwimChance = 0.24f;
    [SerializeField] private Vector2 slowSwimSecondsRange = new Vector2(3.5f, 7f);
    [SerializeField] private Vector2 fastSwimSecondsRange = new Vector2(0.9f, 2.2f);
    [SerializeField] private float waypointForwardBias = 0.58f;

    [Header("Animation Sync")]
    [SerializeField] private float animationSpeedAtBaseSwim = 1f;
    [SerializeField] private float animationSpeedMultiplier = 0.92f;
    [SerializeField] private float minAnimationSpeed = 0.72f;
    [SerializeField] private float maxAnimationSpeed = 1.28f;
    [SerializeField] private float animationSmooth = 4.5f;
    [SerializeField] private float slowTailSwayDegrees = 2.4f;
    [SerializeField] private float fastTailSwayDegrees = 8.5f;
    [SerializeField] private float tailSwayFrequency = 4.8f;

    [Header("Schooling")]
    [SerializeField] private bool enableSchooling = true;
    [SerializeField] private float neighborRadius = 7.4f;
    [SerializeField] private float separationRadius = 1.35f;
    [SerializeField] private float alignmentWeight = 0.42f;
    [SerializeField] private float cohesionWeight = 0.28f;
    [SerializeField] private float separationWeight = 0.72f;
    [SerializeField] private float schoolUpdateSeconds = 0.24f;
    [SerializeField] private float schoolModeChance = 0.62f;
    [SerializeField] private Vector2 schoolingSecondsRange = new Vector2(5.5f, 12f);
    [SerializeField] private Vector2 soloSecondsRange = new Vector2(3f, 8f);
    [SerializeField] private float schoolingBlendSpeed = 1.8f;
    [SerializeField] private float soloSeparationWeight = 0.42f;
    [SerializeField] private float verticalSchoolingWeight = 0.08f;
    [SerializeField] private float verticalSeparationRadius = 0.9f;
    [SerializeField] private float verticalSeparationWeight = 0.65f;
    [SerializeField] private float preferredDepthDrift = 0.16f;
    [SerializeField] private float maxVerticalSwimDirection = 0.04f;
    [SerializeField] private float sameColumnAvoidanceRadius = 1.2f;
    [SerializeField] private float sameColumnAvoidanceWeight = 2.4f;
    [SerializeField] private Vector2 schoolSlotSideRange = new Vector2(-3.2f, 3.2f);
    [SerializeField] private Vector2 schoolSlotForwardRange = new Vector2(-2.6f, 2.6f);

    [Header("Awareness")]
    [SerializeField] private float cameraAwarenessDistance = 5.5f;
    [SerializeField] private float cameraLookWeight = 0.32f;
    [SerializeField] private float cameraAvoidanceWeight = 1.15f;
    [SerializeField] private float curiousLookSeconds = 1.15f;

    private Vector3 targetPosition;
    private Vector3 swimCenter;
    private Vector3 swimSize = new Vector3(16f, 7f, 10f);
    private Vector3 initialModelScale = Vector3.one;
    private Vector3 schoolDirection;
    private Vector2 schoolSlotOffset;
    private Quaternion baseModelLocalRotation = Quaternion.identity;
    private Animator[] animators = new Animator[0];
    private float[] animatorBaseSpeeds = new float[0];
    private float currentSpeed;
    private float currentAnimationSpeed = 1f;
    private float currentSwimEffort = 0.5f;
    private float currentSpeedMultiplier = 1f;
    private float schoolingNoiseSeed;
    private float nextSpeedModeTime;
    private float activeTargetReachDistance;
    private float nextSchoolUpdateTime;
    private float nextSchoolModeTime;
    private float currentSchoolStrength;
    private float curiousLookUntil;
    private string species = "original";
    private string personality = "calm";
    private string appliedTextureUrl = "";
    private Camera mainCamera;
    private bool releasedFish;
    private bool isSchoolingMode;
    private float initialBaseSpeed;
    private float initialSchoolModeChance;
    private Coroutine textureCoroutine;
    private static bool warnedMissingTmpResources;

    public float SpawnTime { get; private set; }
    public string Nickname { get; private set; } = "";
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

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
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
        SpawnTime = Time.time;
        initialBaseSpeed = baseSpeed;
        initialSchoolModeChance = schoolModeChance;
        schoolingNoiseSeed = Random.Range(0f, 1000f);
        RemoveLegacyDrawingBillboards();
        AutoWireVisuals();
        PickSchoolSlot();
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
            EnsureNicknameLabel();
            EnsureReleasedFishMaterials();
        }

        if (nicknameLabel != null)
        {
            nicknameLabel.gameObject.SetActive(false);
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

    public void Apply(FishData data)
    {
        if (data == null)
        {
            return;
        }

        species = string.IsNullOrWhiteSpace(data.species) ? "original" : data.species;
        personality = string.IsNullOrWhiteSpace(data.personality) ? "calm" : data.personality;
        Nickname = SanitizeNickname(data.nickname);

        ApplyColor(colorRenderers, ParseColor(data.main_color, Color.cyan));
        ApplyColor(subColorRenderers, ParseColor(data.sub_color, Color.white));
        ApplyRemoteTexture(data.texture_url);

        if (modelRoot != null)
        {
            modelRoot.localScale = initialModelScale * SizeToScale(data.size);
        }

        if (nicknameLabel != null)
        {
            nicknameLabel.text = Nickname;
            nicknameLabel.gameObject.SetActive(false);
        }

        baseSpeed = initialBaseSpeed * PersonalitySpeedMultiplier(personality);
        schoolModeChance = Mathf.Clamp01(initialSchoolModeChance * PersonalitySchoolingMultiplier(personality));
    }

    private void Update()
    {
        Swim();
        UpdateLabel();
    }

    private void Swim()
    {
        if (!IsInsideSwimBounds(transform.position, boundsPadding))
        {
            targetPosition = ClampToSwimBounds(transform.position, boundsPadding * 2f);
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

        UpdateSchoolingMode();

        Vector3 desiredDirection = toTarget.normalized;
        desiredDirection = BlendSchoolingDirection(desiredDirection);
        desiredDirection = BlendCameraAwareness(desiredDirection);
        desiredDirection = LimitVerticalSwim(desiredDirection);
        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            desiredDirection = transform.forward;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            turnSpeed * Time.deltaTime
        );

        float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDirection.normalized));
        float targetSpeed = baseSpeed * currentSpeedMultiplier * Mathf.Lerp(turnSlowdown, 1f, alignment);
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
        transform.position = ClampToSwimBounds(transform.position, 0f);
        UpdateSwimAnimation();

        if (modelRoot != null)
        {
            float swimAnimationRate = Mathf.Lerp(0.86f, 1.16f, currentSwimEffort);
            float tailAmplitude = Mathf.Lerp(slowTailSwayDegrees, fastTailSwayDegrees, currentSwimEffort);
            float sway = Mathf.Sin(Time.time * tailSwayFrequency * swimAnimationRate) * tailAmplitude;
            float curiousYaw = Time.time < curiousLookUntil ? Mathf.Sin(Time.time * 5.2f) * 7f : 0f;
            modelRoot.localRotation = baseModelLocalRotation * Quaternion.Euler(0f, sway + curiousYaw, 0f);
        }
    }

    private void PickNextTarget()
    {
        float currentDepthTarget = Mathf.Clamp(
            transform.position.y + Random.Range(-swimSize.y * 0.16f, swimSize.y * 0.16f),
            swimCenter.y - swimSize.y * 0.42f,
            swimCenter.y + swimSize.y * 0.42f
        );

        Vector3 randomPoint = new Vector3(
            Random.Range(swimCenter.x - swimSize.x * 0.46f, swimCenter.x + swimSize.x * 0.46f),
            currentDepthTarget,
            Random.Range(swimCenter.z - swimSize.z * 0.46f, swimCenter.z + swimSize.z * 0.46f)
        );

        Vector3 forwardPoint = transform.position + transform.forward * Random.Range(swimSize.z * 0.08f, swimSize.z * 0.22f);
        forwardPoint += transform.right * Random.Range(-swimSize.x * 0.08f, swimSize.x * 0.08f);
        forwardPoint.y = currentDepthTarget + Random.Range(-swimSize.y * 0.08f, swimSize.y * 0.08f);

        targetPosition = Vector3.Lerp(randomPoint, forwardPoint, waypointForwardBias);
        targetPosition = ClampToSwimBounds(targetPosition, boundsPadding);
        activeTargetReachDistance = targetReachDistance * Random.Range(0.85f, 1.45f);
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

    private Vector3 ClampToSwimBounds(Vector3 position, float inset)
    {
        Vector3 halfSize = swimSize * 0.5f;
        float safeInset = Mathf.Max(0f, inset);
        return new Vector3(
            Mathf.Clamp(position.x, swimCenter.x - halfSize.x + safeInset, swimCenter.x + halfSize.x - safeInset),
            Mathf.Clamp(position.y, swimCenter.y - halfSize.y + safeInset, swimCenter.y + halfSize.y - safeInset),
            Mathf.Clamp(position.z, swimCenter.z - halfSize.z + safeInset, swimCenter.z + halfSize.z - safeInset)
        );
    }

    private void CacheAnimators()
    {
        animators = GetComponentsInChildren<Animator>(true);
        animatorBaseSpeeds = new float[animators.Length];

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            animatorBaseSpeeds[i] = animator != null
                ? animator.speed * Random.Range(0.9f, 1.12f)
                : 1f;

            if (animator != null)
            {
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
