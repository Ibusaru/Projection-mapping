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
    [SerializeField] private Transform fishParent;

    [Header("Spawn Area")]
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = new Vector3(16f, 7f, 10f);
    [FormerlySerializedAs("releasedFishScaleMultiplier")]
    [SerializeField] private float releasedFishTargetLength = 1.8f;
    [SerializeField] private Vector3 releasedFishSpawnSpread = new Vector3(6f, 2f, 4f);
    [SerializeField] private float minimumReleasedFishSpawnDistanceFromCamera = 7f;
    [SerializeField] private int spawnPositionAttempts = 18;

    [Header("Default School")]
    [SerializeField] private bool spawnDefaultFishOnStart = true;
    [SerializeField] private int defaultFishCount = 150;
    [FormerlySerializedAs("defaultFishScaleRange")]
    [SerializeField] private Vector2 defaultFishTargetLengthRange = new Vector2(0.45f, 0.85f);
    [SerializeField] private bool autoFindFishAlivePrefabs = true;
    [SerializeField] private bool disableImportedFishMotion = true;

    [Header("Lifetime")]
    [SerializeField] private int maxFishCount = 340;
    [SerializeField] private int minimumFishCount = 125;
    [SerializeField] private float lifetimeSeconds = 600f;

    private readonly Queue<FishActor> fishQueue = new Queue<FishActor>();
    private readonly Dictionary<string, FishActor> releasedFishByKey = new Dictionary<string, FishActor>();
    private Transform runtimeFishParent;

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

        Debug.Log($"FishSpawner: spawned '{fish.nickname}' ({fish.species}) at {position}.");
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
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = prefabs[i % prefabs.Length];
            if (prefab == null)
            {
                continue;
            }

            Vector3 position = DistributedPointInSpawnArea(i, spawnCount);
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
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
            actor.Apply(CreateDefaultFishData(prefab.name, i));
            NormalizeFishScaleToLength(instance, Random.Range(defaultFishTargetLengthRange.x, defaultFishTargetLengthRange.y));
            fishQueue.Enqueue(actor);
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
            if (mainCamera == null || IsFarEnoughFromCamera(position, mainCamera.transform))
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
        return new Vector3(
            Mathf.Clamp(position.x, center.x - size.x * 0.5f, center.x + size.x * 0.5f),
            Mathf.Clamp(position.y, center.y - size.y * 0.5f, center.y + size.y * 0.5f),
            Mathf.Clamp(position.z, center.z - size.z * 0.5f, center.z + size.z * 0.5f)
        );
    }

    private Vector3 DistributedPointInSpawnArea(int index, int totalCount)
    {
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(totalCount)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(totalCount / (float)columns));
        int row = index / columns;
        int column = index % columns;
        float normalizedX = (column + Random.Range(0.18f, 0.82f)) / columns - 0.5f;
        float normalizedZ = (row + Random.Range(0.18f, 0.82f)) / rows - 0.5f;

        return center + new Vector3(
            normalizedX * size.x,
            Random.Range(-size.y * 0.34f, size.y * 0.34f),
            normalizedZ * size.z
        );
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
        NormalizeFishScaleToLength(instance, Mathf.Max(0.5f, releasedFishTargetLength) * ReleasedFishSizeMultiplier(sizeName));
    }

    private void NormalizeFishScaleToLength(GameObject instance, float targetLength)
    {
        Bounds bounds = CalculateRendererBounds(instance);
        float currentLength = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (currentLength <= 0.001f)
        {
            return;
        }

        float scaleFactor = Mathf.Max(0.15f, targetLength) / currentLength;
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

        return !string.IsNullOrWhiteSpace(fish.id)
            ? fish.id.Trim().ToLowerInvariant()
            : NormalizeNicknameKey(fish.nickname);
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
