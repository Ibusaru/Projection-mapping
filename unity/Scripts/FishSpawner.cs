using System.Collections.Generic;
using UnityEngine;

public class FishSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FishApiClient apiClient;
    [SerializeField] private FishActor clownfishPrefab;
    [SerializeField] private FishActor jellyfishPrefab;
    [SerializeField] private FishActor tunaPrefab;
    [SerializeField] private FishActor originalPrefab;

    [Header("Spawn Area")]
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = new Vector3(16f, 7f, 10f);

    [Header("Lifetime")]
    [SerializeField] private int maxFishCount = 120;
    [SerializeField] private int minimumFishCount = 20;
    [SerializeField] private float lifetimeSeconds = 600f;

    private readonly Queue<FishActor> fishQueue = new Queue<FishActor>();

    private void Awake()
    {
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<FishApiClient>();
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
        FishActor prefab = GetPrefab(fish.species);
        if (prefab == null)
        {
            Debug.LogWarning($"FishSpawner: prefab is missing for species '{fish.species}'.");
            return;
        }

        Vector3 position = center + new Vector3(
            Random.Range(-size.x * 0.5f, size.x * 0.5f),
            Random.Range(-size.y * 0.5f, size.y * 0.5f),
            Random.Range(-size.z * 0.5f, size.z * 0.5f)
        );

        FishActor actor = Instantiate(prefab, position, Quaternion.identity);
        actor.Apply(fish);
        fishQueue.Enqueue(actor);

        TrimOverflow();
    }

    private FishActor GetPrefab(string species)
    {
        return species switch
        {
            "clownfish" => clownfishPrefab != null ? clownfishPrefab : originalPrefab,
            "jellyfish" => jellyfishPrefab != null ? jellyfishPrefab : originalPrefab,
            "tuna" => tunaPrefab != null ? tunaPrefab : originalPrefab,
            _ => originalPrefab
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
            Destroy(actor.gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.28f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
        Gizmos.DrawWireCube(center, size);
    }
}
