using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FishSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FishApiClient apiClient;
    [SerializeField] private Object clownfishPrefab;
    [SerializeField] private Object jellyfishPrefab;
    [SerializeField] private Object tunaPrefab;
    [SerializeField] private Object originalPrefab;
    [SerializeField] private GameObject[] defaultFishAlivePrefabs;
    [SerializeField] private GameObject emperorAngelfishPrefab;
    [SerializeField, Range(0f, 0.4f)] private float emperorAngelfishSchoolShare = 0.09f;
    [SerializeField] private Vector2 emperorAngelfishTargetLengthRange = new Vector2(0.18f, 0.38f);
    [SerializeField] private Transform fishParent;

    [Header("Spawn Area")]
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = new Vector3(16f, 7f, 10f);
    [FormerlySerializedAs("releasedFishScaleMultiplier")]
    [SerializeField] private float releasedFishTargetLength = 0.55f;
    [SerializeField] private Vector3 releasedFishSpawnSpread = new Vector3(11f, 4f, 7f);
    [SerializeField] private float minimumReleasedFishSpawnDistanceFromCamera = 7f;
    [SerializeField] private int spawnPositionAttempts = 18;
    [SerializeField] private OceanEnvironment oceanEnvironment;
    [SerializeField] private float spawnSeabedClearance = 1.1f;

    [Header("Default School")]
    [SerializeField] private bool spawnDefaultFishOnStart = true;
    [SerializeField] private int defaultFishCount = 230;
    [FormerlySerializedAs("defaultFishScaleRange")]
    [SerializeField] private Vector2 defaultFishTargetLengthRange = new Vector2(0.18f, 0.38f);
    [SerializeField] private Vector3 defaultSchoolSpread = new Vector3(44f, 4.4f, 28f);
    [SerializeField] private float defaultSchoolYawJitter = 62f;
    [SerializeField] private int defaultSchoolClusterCount = 22;
    [SerializeField] private Vector2 defaultSchoolClusterRadiusRange = new Vector2(2.0f, 4.2f);
    [SerializeField] private float defaultSchoolEdgeMargin = 4.8f;
    [SerializeField] private float mediumSchoolChance = 0.28f;
    [SerializeField] private bool autoFindFishAlivePrefabs = true;
    [SerializeField] private bool disableImportedFishMotion = true;

    [Header("Lifetime")]
    [SerializeField] private int maxFishCount = 520;
    [SerializeField] private int minimumFishCount = 185;
    [SerializeField] private float lifetimeSeconds = 600f;

    private readonly Queue<FishActor> fishQueue = new Queue<FishActor>();
    private readonly Dictionary<string, FishActor> releasedFishByKey = new Dictionary<string, FishActor>();
    private Transform runtimeFishParent;

    private struct DefaultSchoolCluster
    {
        public int id;
        public int count;
        public Vector3 center;
        public Vector3 forward;
        public Quaternion orientation;
        public float radius;
    }

    private void Awake()
    {
        if (apiClient == null)
        {
            apiClient = FindAnyObjectByType<FishApiClient>();
        }
    }

    private void Start()
    {
        if (spawnDefaultFishOnStart)
        {
            SpawnDefaultFish();
        }
    }

    private void OnEnable()
    {
        if (apiClient != null)
        {
            apiClient.OnNewFishes += SpawnFishes;
        }
    }

    private void OnDisable()
    {
        if (apiClient != null)
        {
            apiClient.OnNewFishes -= SpawnFishes;
        }
    }

    private void Update()
    {
        TrimOldFishes();
    }

    public bool DeleteReleasedFish(string fishId)
    {
        string key = string.IsNullOrWhiteSpace(fishId) ? "" : fishId.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key)
            || !releasedFishByKey.TryGetValue(key, out FishActor actor)
            || actor == null)
        {
            return false;
        }

        releasedFishByKey.Remove(key);
        int queuedCount = fishQueue.Count;
        for (int index = 0; index < queuedCount; index++)
        {
            FishActor queuedActor = fishQueue.Dequeue();
            if (queuedActor != null && queuedActor != actor)
            {
                fishQueue.Enqueue(queuedActor);
            }
        }

        Destroy(actor.gameObject);
        Debug.Log($"FishSpawner: deleted released fish id='{fishId}'.");
        return true;
    }

    private void SpawnFishes(IReadOnlyList<FishData> fishes)
    {
        foreach (FishData fish in fishes)
        {
            if (fish == null)
            {
                continue;
            }

            SpawnFish(fish);
        }
    }

    private void SpawnFish(FishData fish)
    {
        if (fish == null)
        {
            return;
        }

        string fishKey = NormalizeFishKey(fish);
        if (!string.IsNullOrWhiteSpace(fishKey)
            && releasedFishByKey.TryGetValue(fishKey, out FishActor existingActor)
            && existingActor != null)
        {
            existingActor.Apply(fish);
            NormalizeReleasedFishScale(existingActor.gameObject, fish.size);
            Debug.Log($"FishSpawner: updated '{fish.nickname}' ({fish.id}) with the latest drawing.");
            return;
        }

        GameObject prefab = GetPrefab(fish.species);
        if (prefab == null)
        {
            Debug.LogWarning($"FishSpawner: prefab is missing for species '{fish.species}'.");
            return;
        }

        Vector3 position = ReleasedFishPointInSpawnArea();
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, GetFishParent());
        PrepareSpawnedInstance(instance, true);

        FishActor actor = instance.GetComponent<FishActor>();
        if (actor == null)
        {
            actor = instance.AddComponent<FishActor>();
        }

        actor.SetReleasedFish(true);
        actor.SetSwimBounds(center, size);
        actor.Apply(fish);
        NormalizeReleasedFishScale(instance, fish.size);
        fishQueue.Enqueue(actor);
        if (!string.IsNullOrWhiteSpace(fishKey))
        {
            releasedFishByKey[fishKey] = actor;
        }

        Debug.Log($"FishSpawner: spawned '{fish.nickname}' ({fish.species}) key='{fishKey}' at {position}. trackedReleased={releasedFishByKey.Count}, totalQueue={fishQueue.Count}");
        Debug.Log($"FishSpawner: visual state for '{fish.nickname}' -> {actor.DescribeVisualState()}");

        TrimOverflow();
    }

    private void SpawnDefaultFish()
    {
        GameObject[] prefabs = GetDefaultFishPrefabs();
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("FishSpawner: no Fish Alive prefabs found for the default school.");
            return;
        }

        int spawnCount = Mathf.Clamp(defaultFishCount, 0, maxFishCount);
        DefaultSchoolCluster[] clusters = CreateDefaultSchoolClusters(spawnCount);
        int fishIndex = 0;
        for (int clusterIndex = 0; clusterIndex < clusters.Length; clusterIndex++)
        {
            DefaultSchoolCluster cluster = clusters[clusterIndex];
            for (int localIndex = 0; localIndex < cluster.count; localIndex++)
            {
                bool useEmperorAngelfish = emperorAngelfishPrefab != null
                    && Random.value < Mathf.Clamp01(emperorAngelfishSchoolShare);
                GameObject prefab = useEmperorAngelfish
                    ? emperorAngelfishPrefab
                    : prefabs[fishIndex % prefabs.Length];
                if (prefab == null)
                {
                    fishIndex++;
                    continue;
                }

                Vector3 position = DistributedPointInSchoolCluster(localIndex, cluster);
                Quaternion rotation = Quaternion.LookRotation(cluster.forward, Vector3.up)
                    * Quaternion.Euler(0f, Random.Range(-defaultSchoolYawJitter, defaultSchoolYawJitter), 0f);
                GameObject instance = Instantiate(prefab, position, rotation, GetFishParent());
                instance.name = $"Default Fish {prefab.name}";
                PrepareSpawnedInstance(instance, false);

                FishActor actor = instance.GetComponent<FishActor>();
                if (actor == null)
                {
                    actor = instance.AddComponent<FishActor>();
                }

                actor.SetReleasedFish(false);
                actor.SetSwimBounds(center, size);
                actor.ConfigureSchoolGroup(cluster.id, cluster.center, cluster.forward, cluster.radius);
                actor.Apply(CreateDefaultFishData(prefab.name, fishIndex));
                Vector2 targetLengthRange = useEmperorAngelfish
                    ? emperorAngelfishTargetLengthRange
                    : defaultFishTargetLengthRange;
                NormalizeFishScaleToLength(instance, Random.Range(targetLengthRange.x, targetLengthRange.y));
                fishQueue.Enqueue(actor);
                fishIndex++;
            }
        }

        TrimOverflow();
    }

    private Vector3 RandomPointInSpawnArea()
    {
        return center + new Vector3(
            Random.Range(-size.x * 0.5f, size.x * 0.5f),
            Random.Range(-size.y * 0.5f, size.y * 0.5f),
            Random.Range(-size.z * 0.5f, size.z * 0.5f)
        );
    }

    private Vector3 ReleasedFishPointInSpawnArea()
    {
        Camera mainCamera = Camera.main;
        Vector3 position = center;
        int attempts = Mathf.Max(1, spawnPositionAttempts);
        for (int i = 0; i < attempts; i++)
        {
            position = RandomReleasedFishPoint();
            if ((mainCamera == null || IsFarEnoughFromCamera(position, mainCamera.transform))
                && IsSpawnPointAboveSeabed(position))
            {
                break;
            }
        }

        return ClampToSpawnArea(position);
    }

    private Vector3 RandomReleasedFishPoint()
    {
        Vector3 safeSpread = new Vector3(
            Mathf.Abs(releasedFishSpawnSpread.x),
            Mathf.Abs(releasedFishSpawnSpread.y),
            Mathf.Abs(releasedFishSpawnSpread.z)
        );
        Vector3 safeSize = new Vector3(
            Mathf.Max(0.1f, Mathf.Abs(size.x)),
            Mathf.Max(0.1f, Mathf.Abs(size.y)),
            Mathf.Max(0.1f, Mathf.Abs(size.z))
        );
        Vector3 halfSpread = Vector3.Min(safeSpread * 0.5f, safeSize * 0.48f);
        return center + new Vector3(
            Random.Range(-halfSpread.x, halfSpread.x),
            Random.Range(-halfSpread.y, halfSpread.y),
            Random.Range(-halfSpread.z, halfSpread.z)
        );
    }

    private bool IsFarEnoughFromCamera(Vector3 position, Transform cameraTransform)
    {
        float minimumDistance = Mathf.Max(0f, minimumReleasedFishSpawnDistanceFromCamera);
        Vector3 toSpawn = position - cameraTransform.position;
        if (toSpawn.magnitude < minimumDistance)
        {
            return false;
        }

        Vector3 horizontalToSpawn = new Vector3(toSpawn.x, 0f, toSpawn.z);
        Vector3 horizontalForward = new Vector3(cameraTransform.forward.x, 0f, cameraTransform.forward.z);
        return horizontalToSpawn.sqrMagnitude < 0.001f
            || horizontalForward.sqrMagnitude < 0.001f
            || Vector3.Dot(horizontalForward.normalized, horizontalToSpawn.normalized) < 0.82f;
    }

    private Vector3 ClampToSpawnArea(Vector3 position)
    {
        return ClampToSpawnArea(position, 0f);
    }

    private Vector3 ClampToSpawnArea(Vector3 position, float inset)
    {
        Vector3 halfSize = new Vector3(
            Mathf.Abs(size.x) * 0.5f,
            Mathf.Abs(size.y) * 0.5f,
            Mathf.Abs(size.z) * 0.5f
        );
        float horizontalInset = Mathf.Max(0f, inset);
        float verticalInset = Mathf.Max(0f, inset * 0.35f);
        float xInset = Mathf.Clamp(horizontalInset, 0f, Mathf.Max(0f, halfSize.x - 0.05f));
        float yInset = Mathf.Clamp(verticalInset, 0f, Mathf.Max(0f, halfSize.y - 0.05f));
        float zInset = Mathf.Clamp(horizontalInset, 0f, Mathf.Max(0f, halfSize.z - 0.05f));

        Vector3 clamped = new Vector3(
            Mathf.Clamp(position.x, center.x - halfSize.x + xInset, center.x + halfSize.x - xInset),
            Mathf.Clamp(position.y, center.y - halfSize.y + yInset, center.y + halfSize.y - yInset),
            Mathf.Clamp(position.z, center.z - halfSize.z + zInset, center.z + halfSize.z - zInset)
        );
        return ClampSpawnAboveSeabed(clamped, inset);
    }

    private Vector3 ClampToSpawnBoundsOnly(Vector3 position, float inset)
    {
        Vector3 halfSize = new Vector3(
            Mathf.Abs(size.x) * 0.5f,
            Mathf.Abs(size.y) * 0.5f,
            Mathf.Abs(size.z) * 0.5f
        );
        float horizontalInset = Mathf.Max(0f, inset);
        float verticalInset = Mathf.Max(0f, inset * 0.35f);
        float xInset = Mathf.Clamp(horizontalInset, 0f, Mathf.Max(0f, halfSize.x - 0.05f));
        float yInset = Mathf.Clamp(verticalInset, 0f, Mathf.Max(0f, halfSize.y - 0.05f));
        float zInset = Mathf.Clamp(horizontalInset, 0f, Mathf.Max(0f, halfSize.z - 0.05f));

        return new Vector3(
            Mathf.Clamp(position.x, center.x - halfSize.x + xInset, center.x + halfSize.x - xInset),
            Mathf.Clamp(position.y, center.y - halfSize.y + yInset, center.y + halfSize.y - yInset),
            Mathf.Clamp(position.z, center.z - halfSize.z + zInset, center.z + halfSize.z - zInset)
        );
    }

    private Vector3 ClampSpawnAboveSeabed(Vector3 position, float inset)
    {
        OceanEnvironment environment = ResolveOceanEnvironment();
        if (environment == null)
        {
            return position;
        }

        Vector3 safePosition = FindNearbySeabedSafePosition(position, environment, inset);
        float floorY = SampleSeabedWorldY(environment, safePosition);
        float waterY = environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY, 0f)).y;
        Vector3 halfSize = new Vector3(
            Mathf.Abs(size.x) * 0.5f,
            Mathf.Abs(size.y) * 0.5f,
            Mathf.Abs(size.z) * 0.5f
        );
        float maxSwimY = Mathf.Min(center.y + halfSize.y - 0.2f, waterY - 0.48f);
        float minSwimY = center.y - halfSize.y + Mathf.Max(0f, inset * 0.35f);
        float desiredY = floorY + Mathf.Max(0.2f, spawnSeabedClearance);
        if (desiredY <= maxSwimY)
        {
            safePosition.y = Mathf.Clamp(Mathf.Max(safePosition.y, desiredY), minSwimY, maxSwimY);
        }
        else
        {
            safePosition.y = Mathf.Clamp(safePosition.y, minSwimY, maxSwimY);
        }

        return safePosition;
    }

    private Vector3 FindNearbySeabedSafePosition(Vector3 position, OceanEnvironment environment, float inset)
    {
        float waterY = environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY, 0f)).y;
        Vector3 halfSize = new Vector3(
            Mathf.Abs(size.x) * 0.5f,
            Mathf.Abs(size.y) * 0.5f,
            Mathf.Abs(size.z) * 0.5f
        );
        float maxSwimY = Mathf.Min(center.y + halfSize.y - 0.2f, waterY - 0.48f);
        float clearance = Mathf.Max(0.2f, spawnSeabedClearance);
        Vector3 best = position;
        float bestFloorY = SampleSeabedWorldY(environment, position);
        if (bestFloorY + clearance <= maxSwimY)
        {
            return best;
        }

        for (int radiusIndex = 0; radiusIndex < 3; radiusIndex++)
        {
            float radius = 3.5f + radiusIndex * 3.5f;
            for (int i = 0; i < 10; i++)
            {
                float angle = (i / 10f) * Mathf.PI * 2f + radiusIndex * 0.37f;
                Vector3 candidate = position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                candidate = ClampToSpawnBoundsOnly(candidate, inset);
                float floorY = SampleSeabedWorldY(environment, candidate);
                if (floorY < bestFloorY)
                {
                    best = candidate;
                    bestFloorY = floorY;
                }

                if (floorY + clearance <= maxSwimY)
                {
                    return candidate;
                }
            }
        }

        return best;
    }

    private bool IsSpawnPointAboveSeabed(Vector3 position)
    {
        OceanEnvironment environment = ResolveOceanEnvironment();
        if (environment == null)
        {
            return true;
        }

        float floorY = SampleSeabedWorldY(environment, position);
        float waterY = environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY, 0f)).y;
        return position.y >= floorY + Mathf.Max(0.2f, spawnSeabedClearance)
            && floorY <= waterY - 0.6f;
    }

    private float SampleSeabedWorldY(OceanEnvironment environment, Vector3 worldPosition)
    {
        Vector3 local = environment.transform.InverseTransformPoint(worldPosition);
        float localY = environment.SampleSeabedHeight(local.x, local.z);
        return environment.transform.TransformPoint(new Vector3(local.x, localY, local.z)).y;
    }

    private OceanEnvironment ResolveOceanEnvironment()
    {
        if (oceanEnvironment == null)
        {
            oceanEnvironment = FindAnyObjectByType<OceanEnvironment>();
        }

        return oceanEnvironment;
    }

    private Vector3 SafeSpawnHalfSize(float inset)
    {
        Vector3 halfSize = new Vector3(
            Mathf.Abs(size.x) * 0.5f,
            Mathf.Abs(size.y) * 0.5f,
            Mathf.Abs(size.z) * 0.5f
        );
        return new Vector3(
            Mathf.Max(0.1f, halfSize.x - Mathf.Max(0f, inset)),
            Mathf.Max(0.1f, halfSize.y - Mathf.Max(0f, inset * 0.35f)),
            Mathf.Max(0.1f, halfSize.z - Mathf.Max(0f, inset))
        );
    }

    private Vector3 DistributedPointInSpawnArea(int index, int totalCount, Quaternion schoolOrientation)
    {
        float safeTotal = Mathf.Max(1, totalCount);
        float goldenAngle = 137.508f * Mathf.Deg2Rad;
        float radius = Mathf.Sqrt((index + 0.5f) / safeTotal);
        float angle = index * goldenAngle + Random.Range(-0.18f, 0.18f);
        Vector3 safeSpread = new Vector3(
            Mathf.Min(Mathf.Abs(defaultSchoolSpread.x), Mathf.Abs(size.x) * 0.78f),
            Mathf.Min(Mathf.Abs(defaultSchoolSpread.y), Mathf.Abs(size.y) * 0.72f),
            Mathf.Min(Mathf.Abs(defaultSchoolSpread.z), Mathf.Abs(size.z) * 0.78f)
        );

        Vector3 localOffset = new Vector3(
            Mathf.Cos(angle) * radius * safeSpread.x * 0.5f,
            Random.Range(-safeSpread.y * 0.5f, safeSpread.y * 0.5f),
            Mathf.Sin(angle) * radius * safeSpread.z * 0.5f
        );
        localOffset += new Vector3(
            Random.Range(-safeSpread.x * 0.035f, safeSpread.x * 0.035f),
            Random.Range(-safeSpread.y * 0.08f, safeSpread.y * 0.08f),
            Random.Range(-safeSpread.z * 0.035f, safeSpread.z * 0.035f)
        );

        return ClampToSpawnArea(center + schoolOrientation * localOffset);
    }

    private DefaultSchoolCluster[] CreateDefaultSchoolClusters(int totalCount)
    {
        if (totalCount <= 0)
        {
            return new DefaultSchoolCluster[0];
        }

        int clusterCount = Mathf.Clamp(
            defaultSchoolClusterCount > 0 ? defaultSchoolClusterCount : Mathf.RoundToInt(Mathf.Sqrt(totalCount)),
            1,
            totalCount
        );
        DefaultSchoolCluster[] clusters = new DefaultSchoolCluster[clusterCount];
        int remaining = totalCount;
        Quaternion spreadOrientation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        for (int i = 0; i < clusterCount; i++)
        {
            int remainingClusters = clusterCount - i;
            int count = i == clusterCount - 1
                ? remaining
                : PickClusterSize(totalCount, clusterCount, remaining, remainingClusters);
            remaining -= count;

            float yaw = Random.Range(0f, 360f);
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float radius = Random.Range(defaultSchoolClusterRadiusRange.x, defaultSchoolClusterRadiusRange.y);
            if (Random.value < mediumSchoolChance)
            {
                radius *= Random.Range(1.18f, 1.45f);
            }

            clusters[i] = new DefaultSchoolCluster
            {
                id = i,
                count = count,
                center = DistributedClusterCenter(i, clusterCount, spreadOrientation, radius),
                forward = forward.normalized,
                orientation = Quaternion.LookRotation(forward, Vector3.up),
                radius = Mathf.Max(1.5f, radius)
            };

            remaining = Mathf.Max(0, totalCount - SumClusterCounts(clusters, i + 1));
        }

        if (SumClusterCounts(clusters, clusters.Length) != totalCount && clusters.Length > 0)
        {
            clusters[clusters.Length - 1].count += totalCount - SumClusterCounts(clusters, clusters.Length);
        }

        return clusters;
    }

    private int PickClusterSize(int totalCount, int clusterCount, int remaining, int remainingClusters)
    {
        int average = Mathf.Max(1, Mathf.RoundToInt(totalCount / (float)clusterCount));
        int minCount = Mathf.Max(3, Mathf.RoundToInt(average * 0.55f));
        int maxCount = Mathf.Max(minCount, Mathf.RoundToInt(average * 1.55f));
        int reserve = Mathf.Max(0, remainingClusters - 1) * minCount;
        int upper = Mathf.Clamp(remaining - reserve, minCount, maxCount);
        return Mathf.Clamp(Random.Range(minCount, upper + 1), 1, remaining);
    }

    private int SumClusterCounts(DefaultSchoolCluster[] clusters, int count)
    {
        int total = 0;
        for (int i = 0; i < count && i < clusters.Length; i++)
        {
            total += clusters[i].count;
        }

        return total;
    }

    private Vector3 DistributedClusterCenter(int index, int totalCount, Quaternion spreadOrientation, float clusterRadius)
    {
        float safeTotal = Mathf.Max(1, totalCount);
        float goldenAngle = 137.508f * Mathf.Deg2Rad;
        float radius = Mathf.Sqrt((index + 0.5f) / safeTotal);
        float angle = index * goldenAngle + Random.Range(-0.32f, 0.32f);
        float edgeMargin = Mathf.Max(defaultSchoolEdgeMargin, clusterRadius + 1.4f);
        Vector3 safeHalfSize = SafeSpawnHalfSize(edgeMargin);
        Vector3 safeSpread = new Vector3(
            Mathf.Min(Mathf.Abs(defaultSchoolSpread.x), safeHalfSize.x * 2f),
            Mathf.Min(Mathf.Abs(defaultSchoolSpread.y), safeHalfSize.y * 2f),
            Mathf.Min(Mathf.Abs(defaultSchoolSpread.z), safeHalfSize.z * 2f)
        );

        Vector3 localOffset = new Vector3(
            Mathf.Cos(angle) * radius * safeSpread.x * 0.5f,
            Random.Range(-safeSpread.y * 0.5f, safeSpread.y * 0.5f),
            Mathf.Sin(angle) * radius * safeSpread.z * 0.5f
        );
        return ClampToSpawnArea(center + spreadOrientation * localOffset, edgeMargin);
    }

    private Vector3 DistributedPointInSchoolCluster(int index, DefaultSchoolCluster cluster)
    {
        float safeCount = Mathf.Max(1, cluster.count);
        float goldenAngle = 137.508f * Mathf.Deg2Rad;
        float radius = Mathf.Sqrt((index + 0.5f) / safeCount) * cluster.radius;
        float angle = index * goldenAngle + Random.Range(-0.22f, 0.22f);
        Vector3 localOffset = new Vector3(
            Mathf.Cos(angle) * radius,
            Random.Range(-cluster.radius * 0.22f, cluster.radius * 0.22f),
            Mathf.Sin(angle) * radius * 0.68f
        );
        localOffset += new Vector3(
            Random.Range(-cluster.radius * 0.14f, cluster.radius * 0.14f),
            Random.Range(-cluster.radius * 0.08f, cluster.radius * 0.08f),
            Random.Range(-cluster.radius * 0.14f, cluster.radius * 0.14f)
        );

        return ClampToSpawnArea(cluster.center + cluster.orientation * localOffset, Mathf.Max(1.2f, defaultSchoolEdgeMargin * 0.35f));
    }

    private GameObject[] GetDefaultFishPrefabs()
    {
        if (defaultFishAlivePrefabs != null && defaultFishAlivePrefabs.Length > 0)
        {
            return defaultFishAlivePrefabs;
        }

#if UNITY_EDITOR
        if (autoFindFishAlivePrefabs)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DenysAlmaral/FishAlive/Prefabs" });
            List<GameObject> foundPrefabs = new List<GameObject>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    foundPrefabs.Add(prefab);
                }
            }

            if (foundPrefabs.Count > 0)
            {
                return foundPrefabs.ToArray();
            }
        }
#endif

        return new GameObject[0];
    }

    private Transform GetFishParent()
    {
        if (fishParent != null)
        {
            return fishParent;
        }

        if (runtimeFishParent == null)
        {
            GameObject parentObject = new GameObject("Runtime Fish");
            runtimeFishParent = parentObject.transform;
        }

        return runtimeFishParent;
    }

    private void PrepareSpawnedInstance(GameObject instance, bool centerVisuals)
    {
        if (instance == null)
        {
            return;
        }

        DisableImportedViewComponents(instance);
        DisableImportedMotionComponents(instance);
        FishRendererUtility.GetVisualRenderers(instance, true);

        if (centerVisuals)
        {
            CenterTopLevelVisualsOnRoot(instance);
        }
    }

    private static void DisableImportedViewComponents(GameObject instance)
    {
        Camera[] cameras = instance.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera != null)
            {
                camera.enabled = false;
            }
        }

        AudioListener[] listeners = instance.GetComponentsInChildren<AudioListener>(true);
        foreach (AudioListener listener in listeners)
        {
            if (listener != null)
            {
                listener.enabled = false;
            }
        }

        Light[] lights = instance.GetComponentsInChildren<Light>(true);
        foreach (Light light in lights)
        {
            if (light != null)
            {
                light.enabled = false;
            }
        }
    }

    private void DisableImportedMotionComponents(GameObject instance)
    {
        if (!disableImportedFishMotion)
        {
            return;
        }

        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour is FishActor)
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private static void CenterTopLevelVisualsOnRoot(GameObject instance)
    {
        Renderer[] renderers = FishRendererUtility.GetVisualRenderers(instance, false);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = CalculateRendererBounds(instance);
        Vector3 localCenter = instance.transform.InverseTransformPoint(bounds.center);
        if (localCenter.sqrMagnitude < 0.0001f)
        {
            return;
        }

        for (int i = 0; i < instance.transform.childCount; i++)
        {
            Transform child = instance.transform.GetChild(i);
            child.localPosition -= localCenter;
        }
    }

    private static FishData CreateDefaultFishData(string prefabName, int index)
    {
        string lowerName = prefabName.ToLowerInvariant();
        string species = lowerName.Contains("clown") ? "clownfish" : "original";

        return new FishData
        {
            nickname = $"{prefabName}-{index + 1:00}",
            species = species,
            personality = index % 4 == 0 ? "fast" : "schooling",
            main_color = "#FFFFFF",
            sub_color = "#FFFFFF",
            size = index % 5 == 0 ? "large" : "medium"
        };
    }

    private GameObject GetPrefab(string species)
    {
        Object prefabReference = species switch
        {
            "clownfish" => clownfishPrefab != null ? clownfishPrefab : originalPrefab,
            "jellyfish" => jellyfishPrefab != null ? jellyfishPrefab : originalPrefab,
            "tuna" => tunaPrefab != null ? tunaPrefab : originalPrefab,
            _ => originalPrefab
        };

        GameObject speciesPrefab = ResolvePrefabAsset(prefabReference);
        if (speciesPrefab != null)
        {
            return speciesPrefab;
        }

        return null;
    }

    private static GameObject ResolvePrefabAsset(Object prefabReference)
    {
        if (prefabReference == null)
        {
            return null;
        }

        if (prefabReference is GameObject gameObject)
        {
            return gameObject;
        }

        if (prefabReference is Component component)
        {
            return component.gameObject;
        }

        Debug.LogWarning($"FishSpawner: unsupported prefab reference type '{prefabReference.GetType().Name}'.");
        return null;
    }

    private void NormalizeReleasedFishScale(GameObject instance, string sizeName)
    {
        NormalizeFishScaleToLength(instance, Mathf.Max(0.05f, releasedFishTargetLength) * ReleasedFishSizeMultiplier(sizeName));
    }

    private void NormalizeFishScaleToLength(GameObject instance, float targetLength)
    {
        Bounds bounds = CalculateRendererBounds(instance);
        float currentLength = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (currentLength <= 0.001f)
        {
            return;
        }

        float scaleFactor = Mathf.Max(0.05f, targetLength) / currentLength;
        instance.transform.localScale *= scaleFactor;
    }

    private static Bounds CalculateRendererBounds(GameObject instance)
    {
        Renderer[] renderers = FishRendererUtility.GetVisualRenderers(instance, false);
        Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
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

        return hasBounds ? bounds : new Bounds(instance.transform.position, Vector3.one);
    }

    private static float ReleasedFishSizeMultiplier(string sizeName)
    {
        return sizeName switch
        {
            "small" => 0.78f,
            "large" => 1.18f,
            _ => 1f
        };
    }

    private void TrimOverflow()
    {
        while (fishQueue.Count > maxFishCount)
        {
            DestroyOldest();
        }
    }

    private void TrimOldFishes()
    {
        if (fishQueue.Count <= minimumFishCount)
        {
            return;
        }

        int checks = fishQueue.Count;
        for (int i = 0; i < checks; i++)
        {
            if (fishQueue.Count == 0)
            {
                return;
            }

            FishActor actor = fishQueue.Peek();
            if (actor == null)
            {
                fishQueue.Dequeue();
                continue;
            }

            if (Time.time - actor.SpawnTime < lifetimeSeconds)
            {
                break;
            }

            DestroyOldest();

            if (fishQueue.Count <= minimumFishCount)
            {
                break;
            }
        }
    }

    private void DestroyOldest()
    {
        if (fishQueue.Count == 0)
        {
            return;
        }

        FishActor actor = fishQueue.Dequeue();
        if (actor != null)
        {
            RemoveTrackedActor(actor);

            Destroy(actor.gameObject);
        }
    }

    private void RemoveTrackedActor(FishActor actor)
    {
        string keyToRemove = "";
        foreach (KeyValuePair<string, FishActor> pair in releasedFishByKey)
        {
            if (pair.Value == actor)
            {
                keyToRemove = pair.Key;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(keyToRemove))
        {
            releasedFishByKey.Remove(keyToRemove);
        }
    }

    private static string NormalizeFishKey(FishData fish)
    {
        if (fish == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(fish.id))
        {
            return fish.id.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(fish.texture_path))
        {
            return fish.texture_path.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(fish.texture_url))
        {
            return fish.texture_url.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(fish.created_at) || !string.IsNullOrWhiteSpace(fish.updated_at))
        {
            return $"{NormalizeNicknameKey(fish.nickname)}|{fish.created_at}|{fish.updated_at}";
        }

        return NormalizeNicknameKey(fish.nickname);
    }

    private static string NormalizeNicknameKey(string nickname)
    {
        return string.IsNullOrWhiteSpace(nickname)
            ? ""
            : nickname.Trim().ToLowerInvariant();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.28f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
        Gizmos.DrawWireCube(center, size);
    }
}
