using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FishSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FishApiClient apiClient;
    [SerializeField] private GameObject clownfishPrefab;
    [SerializeField] private GameObject jellyfishPrefab;
    [SerializeField] private GameObject tunaPrefab;
    [SerializeField] private GameObject originalPrefab;
    [SerializeField] private GameObject[] defaultFishAlivePrefabs;

    [Header("Spawn Area")]
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = new Vector3(16f, 7f, 10f);
    [SerializeField] private float releasedFishScaleMultiplier = 40f;
    [SerializeField] private Vector3 releasedFishSpawnSpread = new Vector3(6f, 2f, 4f);

    [Header("Default School")]
    [SerializeField] private bool spawnDefaultFishOnStart = true;
    [SerializeField] private int defaultFishCount = 54;
    [SerializeField] private Vector2 defaultFishScaleRange = new Vector2(1.45f, 2.2f);
    [SerializeField] private bool autoFindFishAlivePrefabs = true;
    [SerializeField] private bool disableImportedFishMotion = true;

    [Header("Lifetime")]
    [SerializeField] private int maxFishCount = 120;
    [SerializeField] private int minimumFishCount = 20;
    [SerializeField] private float lifetimeSeconds = 600f;

    private readonly Queue<FishActor> fishQueue = new Queue<FishActor>();
    private readonly Dictionary<string, FishActor> releasedFishByNickname = new Dictionary<string, FishActor>();

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
            SpawnFish(fish);
        }
    }

    private void SpawnFish(FishData fish)
    {
        string nicknameKey = NormalizeNicknameKey(fish.nickname);
        if (!string.IsNullOrWhiteSpace(nicknameKey)
            && releasedFishByNickname.TryGetValue(nicknameKey, out FishActor existingActor)
            && existingActor != null)
        {
            existingActor.Apply(fish);
            Debug.Log($"FishSpawner: updated '{fish.nickname}' with the latest drawing.");
            return;
        }

        GameObject prefab = GetPrefab(fish.species);
        if (prefab == null)
        {
            Debug.LogWarning($"FishSpawner: prefab is missing for species '{fish.species}'.");
            return;
        }

        Vector3 position = ReleasedFishPointInSpawnArea();
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        instance.transform.localScale *= Mathf.Max(0.1f, releasedFishScaleMultiplier);
        DisableImportedMotionComponents(instance);

        FishActor actor = instance.GetComponent<FishActor>();
        if (actor == null)
        {
            actor = instance.AddComponent<FishActor>();
        }

        actor.SetReleasedFish(true);
        actor.SetSwimBounds(center, size);
        actor.Apply(fish);
        fishQueue.Enqueue(actor);
        if (!string.IsNullOrWhiteSpace(nicknameKey))
        {
            releasedFishByNickname[nicknameKey] = actor;
        }

        Debug.Log($"FishSpawner: spawned '{fish.nickname}' ({fish.species}) at {position}.");

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
            GameObject instance = Instantiate(prefab, position, rotation);
            instance.name = $"Default Fish {prefab.name}";
            instance.transform.localScale *= Random.Range(defaultFishScaleRange.x, defaultFishScaleRange.y);
            DisableImportedMotionComponents(instance);

            FishActor actor = instance.GetComponent<FishActor>();
            if (actor == null)
            {
                actor = instance.AddComponent<FishActor>();
            }

            actor.SetReleasedFish(false);
            actor.SetSwimBounds(center, size);
            actor.Apply(CreateDefaultFishData(prefab.name, i));
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
        Vector3 halfSpread = releasedFishSpawnSpread * 0.5f;
        Vector3 position = center + new Vector3(
            Random.Range(-halfSpread.x, halfSpread.x),
            Random.Range(-halfSpread.y, halfSpread.y),
            Random.Range(-halfSpread.z, halfSpread.z)
        );
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
        GameObject speciesPrefab = species switch
        {
            "clownfish" => clownfishPrefab != null ? clownfishPrefab : originalPrefab,
            "jellyfish" => jellyfishPrefab != null ? jellyfishPrefab : originalPrefab,
            "tuna" => tunaPrefab != null ? tunaPrefab : originalPrefab,
            _ => originalPrefab
        };

        if (speciesPrefab != null)
        {
            return speciesPrefab;
        }

        GameObject[] prefabs = GetDefaultFishPrefabs();
        return prefabs != null && prefabs.Length > 0 ? prefabs[0] : null;
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
            string nicknameKey = NormalizeNicknameKey(actor.Nickname);
            if (!string.IsNullOrWhiteSpace(nicknameKey)
                && releasedFishByNickname.TryGetValue(nicknameKey, out FishActor trackedActor)
                && trackedActor == actor)
            {
                releasedFishByNickname.Remove(nicknameKey);
            }

            Destroy(actor.gameObject);
        }
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
