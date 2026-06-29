using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class FishActor : MonoBehaviour
{
    private static readonly List<FishActor> ActiveFishes = new List<FishActor>();

    [Header("Visual")]
    [SerializeField] private Renderer[] colorRenderers;
    [SerializeField] private Renderer[] subColorRenderers;
    [SerializeField] private Renderer[] textureRenderers;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private TMP_Text nicknameLabel;
    [SerializeField] private bool createNicknameLabelWhenMissing = true;

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

    public float SpawnTime { get; private set; }
    public string Nickname { get; private set; } = "";
    public bool IsReleasedFish => releasedFish;
    public float CameraFocusRadius => EstimateFocusRadius();
    public static IReadOnlyList<FishActor> AllActiveFishes => ActiveFishes;

    private void Awake()
    {
        mainCamera = Camera.main;
        SpawnTime = Time.time;
        initialBaseSpeed = baseSpeed;
        initialSchoolModeChance = schoolModeChance;
        schoolingNoiseSeed = Random.Range(0f, 1000f);
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
        EnsureNicknameLabel();

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

    private void UpdateSwimAnimation()
    {
        float baseReferenceSpeed = Mathf.Max(0.1f, baseSpeed * animationSpeedAtBaseSwim);
        float speedRatio = Mathf.Clamp(currentSpeed / baseReferenceSpeed, 0.35f, 2.25f);
        currentSwimEffort = Mathf.Lerp(
            currentSwimEffort,
            Mathf.InverseLerp(0.45f, 1.65f, speedRatio),
            1f - Mathf.Exp(-animationSmooth * Time.deltaTime)
        );

        float targetAnimationSpeed = Mathf.Clamp(
            speedRatio * animationSpeedMultiplier,
            minAnimationSpeed,
            maxAnimationSpeed
        );
        currentAnimationSpeed = Mathf.Lerp(
            currentAnimationSpeed,
            targetAnimationSpeed,
            1f - Mathf.Exp(-animationSmooth * Time.deltaTime)
        );

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator != null)
            {
                animator.speed = animatorBaseSpeeds[i] * currentAnimationSpeed;
            }
        }
    }

    private Vector3 BlendSchoolingDirection(Vector3 direction)
    {
        if (!enableSchooling)
        {
            schoolDirection = Vector3.Lerp(schoolDirection, Vector3.zero, 0.3f);
            return direction;
        }

        if (Time.time < nextSchoolUpdateTime)
        {
            return (direction + schoolDirection * currentSchoolStrength).normalized;
        }

        nextSchoolUpdateTime = Time.time + schoolUpdateSeconds + Random.Range(0f, 0.12f);
        IReadOnlyList<FishActor> fishes = AllActiveFishes;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 separation = Vector3.zero;
        Vector3 averageForward = Vector3.zero;
        float averageNeighborY = 0f;
        int neighborCount = 0;

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor neighbor = fishes[i];
            if (neighbor == null || neighbor == this)
            {
                continue;
            }

            Vector3 offset = neighbor.transform.position - transform.position;
            Vector3 horizontalOffset = new Vector3(offset.x, 0f, offset.z);
            float horizontalDistance = horizontalOffset.magnitude;
            if (horizontalDistance > neighborRadius)
            {
                continue;
            }

            neighborCount++;
            Vector3 neighborForward = neighbor.transform.forward;
            Vector3 horizontalForward = new Vector3(neighborForward.x, 0f, neighborForward.z);
            alignment += horizontalForward;
            averageForward += horizontalForward;
            cohesion += new Vector3(neighbor.transform.position.x, 0f, neighbor.transform.position.z);
            averageNeighborY += neighbor.transform.position.y;

            if (horizontalDistance < sameColumnAvoidanceRadius)
            {
                Vector3 escape = horizontalDistance > 0.001f
                    ? -horizontalOffset.normalized
                    : RandomEscapeDirection(neighbor);
                separation += escape * sameColumnAvoidanceWeight * (1f - horizontalDistance / sameColumnAvoidanceRadius);
            }
            else if (horizontalDistance < separationRadius)
            {
                separation -= horizontalOffset.normalized * (1f - horizontalDistance / separationRadius);
            }

            float verticalDistance = Mathf.Abs(offset.y);
            if (verticalDistance < verticalSeparationRadius)
            {
                float pushDirection = offset.y >= 0f ? -1f : 1f;
                separation += Vector3.up * pushDirection * (1f - verticalDistance / verticalSeparationRadius) * verticalSeparationWeight;
            }
        }

        if (neighborCount == 0)
        {
            schoolDirection = Vector3.Lerp(schoolDirection, Vector3.zero, 0.45f);
            return (direction + schoolDirection * currentSchoolStrength).normalized;
        }

        float schoolingInfluence = currentSchoolStrength;
        alignment = alignment.normalized * alignmentWeight * schoolingInfluence;
        Vector3 schoolForward = averageForward.sqrMagnitude > 0.001f
            ? averageForward.normalized
            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        if (schoolForward.sqrMagnitude < 0.001f)
        {
            schoolForward = Vector3.forward;
        }

        Vector3 schoolRight = new Vector3(schoolForward.z, 0f, -schoolForward.x);
        Vector3 slotOffset = schoolRight * schoolSlotOffset.x + schoolForward * schoolSlotOffset.y;
        Vector3 cohesionTarget = cohesion / neighborCount + slotOffset;
        Vector3 horizontalCohesion = cohesionTarget - new Vector3(transform.position.x, 0f, transform.position.z);
        cohesion = horizontalCohesion.normalized * cohesionWeight * schoolingInfluence;
        float depthOffset = (averageNeighborY / neighborCount) - transform.position.y;
        cohesion += Vector3.up * Mathf.Clamp(depthOffset, -1f, 1f) * verticalSchoolingWeight * schoolingInfluence;
        cohesion += Vector3.up * Mathf.Sin(Time.time * 0.37f + SpawnTime) * preferredDepthDrift * (1f - schoolingInfluence * 0.35f);
        separation = separation.normalized * Mathf.Lerp(soloSeparationWeight, separationWeight, schoolingInfluence);
        schoolDirection = Vector3.Lerp(schoolDirection, alignment + cohesion + separation, 0.5f);
        return (direction + schoolDirection).normalized;
    }

    private Vector3 LimitVerticalSwim(Vector3 direction)
    {
        Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
        if (horizontal.sqrMagnitude < 0.001f)
        {
            horizontal = new Vector3(transform.forward.x, 0f, transform.forward.z);
        }

        if (horizontal.sqrMagnitude < 0.001f)
        {
            horizontal = Vector3.forward;
        }

        float vertical = Mathf.Clamp(direction.y, -maxVerticalSwimDirection, maxVerticalSwimDirection);
        return (horizontal.normalized + Vector3.up * vertical).normalized;
    }

    private void UpdateSchoolingMode()
    {
        if (Time.time >= nextSchoolModeTime)
        {
            PickNextSchoolingMode(false);
        }

        float targetStrength = isSchoolingMode ? 1f : 0f;
        currentSchoolStrength = Mathf.MoveTowards(
            currentSchoolStrength,
            targetStrength,
            schoolingBlendSpeed * Time.deltaTime
        );
    }

    private void PickNextSchoolingMode(bool initial)
    {
        isSchoolingMode = Random.value < schoolModeChance;
        Vector2 durationRange = isSchoolingMode ? schoolingSecondsRange : soloSecondsRange;
        nextSchoolModeTime = Time.time + Random.Range(durationRange.x, durationRange.y);

        if (isSchoolingMode)
        {
            PickSchoolSlot();
        }

        if (initial)
        {
            currentSchoolStrength = isSchoolingMode ? Random.Range(0.65f, 1f) : Random.Range(0f, 0.25f);
        }
    }

    private void PickSchoolSlot()
    {
        schoolSlotOffset = new Vector2(
            Random.Range(schoolSlotSideRange.x, schoolSlotSideRange.y),
            Random.Range(schoolSlotForwardRange.x, schoolSlotForwardRange.y)
        );
    }

    private Vector3 RandomEscapeDirection(FishActor neighbor)
    {
        float seed = schoolingNoiseSeed * 0.73f + neighbor.schoolingNoiseSeed * 1.37f;
        return new Vector3(Mathf.Cos(seed), 0f, Mathf.Sin(seed)).normalized;
    }

    private Vector3 BlendCameraAwareness(Vector3 direction)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return direction;
            }
        }

        Vector3 toCamera = mainCamera.transform.position - transform.position;
        float distance = toCamera.magnitude;
        if (distance > cameraAwarenessDistance || distance < 0.001f)
        {
            return direction;
        }

        float awareness = 1f - distance / cameraAwarenessDistance;
        curiousLookUntil = Time.time + curiousLookSeconds * awareness;
        Vector3 curiousDirection = Vector3.Lerp(direction, toCamera.normalized, cameraLookWeight * awareness);
        return curiousDirection.normalized;
    }

    private void UpdateLabel()
    {
        if (!releasedFish)
        {
            if (nicknameLabel != null)
            {
                nicknameLabel.gameObject.SetActive(false);
            }

            return;
        }

        if (nicknameLabel == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
        bool visible = distance <= labelVisibleDistance;
        nicknameLabel.gameObject.SetActive(visible);

        if (visible)
        {
            nicknameLabel.transform.LookAt(mainCamera.transform);
            nicknameLabel.transform.Rotate(0f, 180f, 0f);
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

    private void AutoWireVisuals()
    {
        if (!autoWireRenderers)
        {
            return;
        }

        if (modelRoot == null)
        {
            modelRoot = transform;
        }

        initialModelScale = modelRoot.localScale;
        baseModelLocalRotation = modelRoot.localRotation;

        if (colorRenderers == null || colorRenderers.Length == 0)
        {
            colorRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (textureRenderers == null || textureRenderers.Length == 0)
        {
            textureRenderers = colorRenderers;
        }
    }

    private void EnsureNicknameLabel()
    {
        if (!releasedFish || nicknameLabel != null || !createNicknameLabelWhenMissing)
        {
            return;
        }

        GameObject labelObject = new GameObject("Nickname Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = Vector3.up * 0.9f;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 0.42f;
        label.color = new Color(0.9f, 1f, 1f, 0.92f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = string.Empty;
        nicknameLabel = label;
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
        Renderer[] renderers = colorRenderers != null && colorRenderers.Length > 0
            ? colorRenderers
            : GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(transform.position, Vector3.one * 0.5f);
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

        return hasBounds ? Mathf.Clamp(bounds.extents.magnitude, 0.45f, 5f) : 1f;
    }

    private static void ApplyColor(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer item in renderers)
        {
            if (item != null)
            {
                item.material.color = color;
            }
        }
    }

    private void ApplyRemoteTexture(string textureUrl)
    {
        if (string.IsNullOrWhiteSpace(textureUrl) || textureUrl == appliedTextureUrl)
        {
            return;
        }

        if (textureCoroutine != null)
        {
            StopCoroutine(textureCoroutine);
        }

        textureCoroutine = StartCoroutine(DownloadAndApplyTexture(textureUrl));
    }

    private IEnumerator DownloadAndApplyTexture(string textureUrl)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(textureUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"FishActor: texture download failed for '{Nickname}': {request.error}");
            textureCoroutine = null;
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);
        ApplyTexture(textureRenderers, texture);
        appliedTextureUrl = textureUrl;
        textureCoroutine = null;
    }

    private static void ApplyTexture(Renderer[] renderers, Texture2D texture)
    {
        if (renderers == null || texture == null)
        {
            return;
        }

        foreach (Renderer item in renderers)
        {
            if (item == null)
            {
                continue;
            }

            item.material.mainTexture = texture;
        }
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
