using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public class FishApiClient : MonoBehaviour
{
    [Header("Supabase")]
    [SerializeField] private string supabaseUrl = "";
    [SerializeField] private string supabaseAnonKey = "";

    [Header("Realtime")]
    [SerializeField] private bool useRealtime = true;
    [SerializeField] private float realtimeSafetyRefreshSeconds = 60f;

    [Header("Polling fallback")]
    [SerializeField] private float pollingSeconds = 8f;
    [SerializeField] private int fetchLimit = 100;
    [SerializeField] private int catchUpPageLimit = 6;

    private readonly Dictionary<string, string> seenFishVersions = new Dictionary<string, string>();
    private readonly ConcurrentQueue<string> realtimeChanges = new ConcurrentQueue<string>();
    private SupabaseRealtimeListener realtimeListener;
    private bool isFetching;
    private bool lastRealtimeSubscribed;
    private int realtimeStatusRevision;
    private int snapshotRequested;

    public event Action<IReadOnlyList<FishData>> OnNewFishes;
    public event Action<IReadOnlyList<string>> OnRemovedFishKeys;

    private void Start()
    {
        Dictionary<string, string> localEnv = ReadLocalEnvFile();
        supabaseUrl = FirstNonEmpty(
            supabaseUrl,
            Environment.GetEnvironmentVariable("OCEAN_SUPABASE_URL"),
            Environment.GetEnvironmentVariable("SUPABASE_URL"),
            Environment.GetEnvironmentVariable("VITE_SUPABASE_URL"),
            GetLocalEnvValue(localEnv, "VITE_SUPABASE_URL")
        );
        supabaseAnonKey = FirstNonEmpty(
            supabaseAnonKey,
            Environment.GetEnvironmentVariable("OCEAN_SUPABASE_ANON_KEY"),
            Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY"),
            Environment.GetEnvironmentVariable("VITE_SUPABASE_ANON_KEY"),
            GetLocalEnvValue(localEnv, "VITE_SUPABASE_ANON_KEY")
        );

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseAnonKey))
        {
            Debug.LogWarning("FishApiClient: Supabase URL or anon key is empty. Set OCEAN_SUPABASE_URL/OCEAN_SUPABASE_ANON_KEY or web/.env.local.");
            return;
        }

        fetchLimit = Mathf.Max(1, fetchLimit);
        catchUpPageLimit = Mathf.Max(1, catchUpPageLimit);
        pollingSeconds = Mathf.Max(1f, pollingSeconds);
        realtimeSafetyRefreshSeconds = Mathf.Max(pollingSeconds, realtimeSafetyRefreshSeconds);

        if (useRealtime)
        {
            try
            {
                realtimeListener = new SupabaseRealtimeListener(
                    supabaseUrl,
                    supabaseAnonKey,
                    realtimeChanges.Enqueue
                );
                realtimeListener.Start();
                Debug.Log(
                    $"FishApiClient: Realtime starting. REST fallback={pollingSeconds:0.#}s, "
                    + $"safety refresh={realtimeSafetyRefreshSeconds:0.#}s."
                );
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"FishApiClient: Realtime could not start ({exception.Message}). "
                    + $"Using REST polling every {pollingSeconds:0.#} seconds."
                );
            }
        }
        else
        {
            Debug.Log($"FishApiClient: Realtime disabled. Polling every {pollingSeconds:0.#} seconds.");
        }

        StartCoroutine(SyncLoop());
    }

    private void Update()
    {
        UpdateRealtimeStatus();

        if (isFetching)
        {
            return;
        }

        int processedChanges = 0;
        while (processedChanges < 100 && realtimeChanges.TryDequeue(out string messageJson))
        {
            try
            {
                ApplyRealtimeChange(messageJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"FishApiClient: invalid Realtime change ({exception.Message}); refreshing snapshot.");
                Interlocked.Exchange(ref snapshotRequested, 1);
            }

            processedChanges++;
        }
    }

    private void OnDestroy()
    {
        realtimeListener?.Dispose();
        realtimeListener = null;
    }

    private IEnumerator SyncLoop()
    {
        while (true)
        {
            Interlocked.Exchange(ref snapshotRequested, 0);
            yield return FetchLatestFishes();

            float waitSeconds = realtimeListener != null && realtimeListener.IsSubscribed
                ? realtimeSafetyRefreshSeconds
                : pollingSeconds;
            float refreshAt = Time.realtimeSinceStartup + waitSeconds;

            while (Time.realtimeSinceStartup < refreshAt
                   && Volatile.Read(ref snapshotRequested) == 0)
            {
                yield return null;
            }
        }
    }

    private IEnumerator FetchLatestFishes()
    {
        isFetching = true;
        try
        {
            yield return FetchLatestFishSnapshot();
        }
        finally
        {
            isFetching = false;
        }
    }

    private IEnumerator FetchLatestFishSnapshot()
    {
        string baseUrl = supabaseUrl.TrimEnd('/');
        List<FishData> newFishes = new List<FishData>();
        HashSet<string> currentFishKeys = new HashSet<string>();
        int pageLimit = Mathf.Max(1, catchUpPageLimit);
        bool snapshotComplete = false;

        for (int page = 0; page < pageLimit; page++)
        {
            string url = BuildFetchUrl(baseUrl, page);
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"FishApiClient: fetch failed: {request.error} ({request.responseCode})");
                yield break;
            }

            string wrappedJson = $"{{\"items\":{request.downloadHandler.text}}}";
            FishDataList list = JsonUtility.FromJson<FishDataList>(wrappedJson);

            if (list?.items == null || list.items.Length == 0)
            {
                snapshotComplete = true;
                break;
            }

            Debug.Log($"FishApiClient: fetched page {page + 1}/{pageLimit}, rows={list.items.Length}, seen={seenFishVersions.Count}.");

            for (int index = list.items.Length - 1; index >= 0; index--)
            {
                string fishKey = FishKey(list.items[index]);
                if (!string.IsNullOrWhiteSpace(fishKey))
                {
                    currentFishKeys.Add(fishKey);
                }

                CollectNewFish(list.items[index], newFishes);
            }

            if (list.items.Length < fetchLimit)
            {
                snapshotComplete = true;
                break;
            }
        }

        if (snapshotComplete)
        {
            List<string> removedFishKeys = CollectRemovedFishKeys(currentFishKeys);
            if (removedFishKeys.Count > 0)
            {
                Debug.Log($"FishApiClient: detected {removedFishKeys.Count} fish removed from the database.");
                OnRemovedFishKeys?.Invoke(removedFishKeys);
            }
        }

        if (newFishes.Count > 0)
        {
            Debug.Log($"FishApiClient: received {newFishes.Count} new fish.");
            OnNewFishes?.Invoke(newFishes);
        }
    }

    private void UpdateRealtimeStatus()
    {
        if (realtimeListener == null)
        {
            return;
        }

        bool isSubscribed = realtimeListener.IsSubscribed;
        if (isSubscribed != lastRealtimeSubscribed)
        {
            lastRealtimeSubscribed = isSubscribed;
            Interlocked.Exchange(ref snapshotRequested, 1);
        }

        int revision = realtimeListener.StatusRevision;
        if (revision == realtimeStatusRevision)
        {
            return;
        }

        realtimeStatusRevision = revision;
        if (isSubscribed)
        {
            Debug.Log($"FishApiClient: {realtimeListener.StatusMessage}");
        }
        else
        {
            Debug.LogWarning(
                $"FishApiClient: {realtimeListener.StatusMessage} "
                + $"REST fallback remains active every {pollingSeconds:0.#} seconds."
            );
        }
    }

    private void ApplyRealtimeChange(string messageJson)
    {
        RealtimeChangeEnvelope envelope = JsonUtility.FromJson<RealtimeChangeEnvelope>(messageJson);
        RealtimeChangeData change = envelope?.payload?.data;
        if (change == null
            || !string.Equals(change.schema, "public", StringComparison.Ordinal)
            || !string.Equals(change.table, "fishes", StringComparison.Ordinal))
        {
            Interlocked.Exchange(ref snapshotRequested, 1);
            return;
        }

        switch (change.type)
        {
            case "INSERT":
            case "UPDATE":
                List<FishData> changedFishes = new List<FishData>();
                CollectNewFish(change.record, changedFishes);
                if (changedFishes.Count > 0)
                {
                    Debug.Log($"FishApiClient: received {change.type} through Realtime.");
                    OnNewFishes?.Invoke(changedFishes);
                }
                break;

            case "DELETE":
                string removedFishKey = FishKey(change.old_record);
                if (string.IsNullOrWhiteSpace(removedFishKey))
                {
                    Interlocked.Exchange(ref snapshotRequested, 1);
                    return;
                }

                seenFishVersions.Remove(removedFishKey);
                Debug.Log($"FishApiClient: received DELETE through Realtime for '{removedFishKey}'.");
                OnRemovedFishKeys?.Invoke(new[] { removedFishKey });
                break;

            default:
                Interlocked.Exchange(ref snapshotRequested, 1);
                break;
        }
    }

    private List<string> CollectRemovedFishKeys(HashSet<string> currentFishKeys)
    {
        List<string> removedFishKeys = new List<string>();
        List<string> staleFishKeys = new List<string>();

        foreach (string fishKey in seenFishVersions.Keys)
        {
            if (!currentFishKeys.Contains(fishKey))
            {
                staleFishKeys.Add(fishKey);
            }
        }

        foreach (string fishKey in staleFishKeys)
        {
            if (seenFishVersions.Remove(fishKey))
            {
                removedFishKeys.Add(fishKey);
            }
        }

        return removedFishKeys;
    }

    private string BuildFetchUrl(string baseUrl, int page)
    {
        int safePage = Mathf.Max(0, page);
        int offset = safePage * fetchLimit;
        return $"{baseUrl}/rest/v1/fishes?select=*&order=updated_at.desc,created_at.desc&limit={fetchLimit}&offset={offset}";
    }

    private void CollectNewFish(FishData fish, List<FishData> newFishes)
    {
        if (fish == null)
        {
            return;
        }

        string fishKey = FishKey(fish);
        if (string.IsNullOrWhiteSpace(fishKey))
        {
            return;
        }

        string version = FishVersion(fish);
        if (string.IsNullOrWhiteSpace(version))
        {
            version = fishKey;
        }

        if (!seenFishVersions.TryGetValue(fishKey, out string seenVersion) || seenVersion != version)
        {
            seenFishVersions[fishKey] = version;
            newFishes.Add(fish);
            Debug.Log($"FishApiClient: queued fish key='{fishKey}', nickname='{fish.nickname}', version='{version}'.");
        }
    }

    private static string FishVersion(FishData fish)
    {
        if (fish == null)
        {
            return "";
        }

        return !string.IsNullOrWhiteSpace(fish.updated_at) ? fish.updated_at : fish.created_at;
    }

    private static string FishKey(FishData fish)
    {
        if (fish == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(fish.id))
        {
            return fish.id.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fish.texture_path))
        {
            return fish.texture_path.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fish.texture_url))
        {
            return fish.texture_url.Trim();
        }

        string version = FishVersion(fish);
        if (!string.IsNullOrWhiteSpace(version) || !string.IsNullOrWhiteSpace(fish.nickname))
        {
            return $"{fish.nickname?.Trim()}|{version}";
        }

        return "";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]))
            {
                return values[index].Trim();
            }
        }

        return "";
    }

    private static string GetLocalEnvValue(Dictionary<string, string> values, string key)
    {
        return values != null && values.TryGetValue(key, out string value) ? value : "";
    }

    private static Dictionary<string, string> ReadLocalEnvFile()
    {
        string path = FindLocalEnvPath();
        Dictionary<string, string> values = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(path))
        {
            return values;
        }

        try
        {
            foreach (string line in File.ReadLines(path))
            {
                if (TryParseEnvLine(line, out string key, out string value))
                {
                    values[key] = value;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"FishApiClient: failed to read web/.env.local at '{path}': {exception.Message}");
        }

        return values;
    }

    private static bool TryParseEnvLine(string line, out string key, out string value)
    {
        key = "";
        value = "";

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string trimmed = line.Trim();
        if (trimmed.StartsWith("#"))
        {
            return false;
        }

        const string exportPrefix = "export ";
        if (trimmed.StartsWith(exportPrefix, StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(exportPrefix.Length).TrimStart();
        }

        int separator = trimmed.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        key = trimmed.Substring(0, separator).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        value = NormalizeEnvValue(trimmed.Substring(separator + 1).Trim());
        return true;
    }

    private static string NormalizeEnvValue(string value)
    {
        if (value.Length >= 2)
        {
            char first = value[0];
            char last = value[value.Length - 1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value.Substring(1, value.Length - 2);
            }
        }

        return value;
    }

    private static string FindLocalEnvPath()
    {
        string[] candidates =
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "web", ".env.local")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "web", ".env.local")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "web", ".env.local")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "web", ".env.local")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "web", ".env.local"))
        };

        for (int index = 0; index < candidates.Length; index++)
        {
            if (File.Exists(candidates[index]))
            {
                return candidates[index];
            }
        }

        return "";
    }

    [Serializable]
    private sealed class RealtimeChangeEnvelope
    {
        public RealtimeChangePayload payload;
    }

    [Serializable]
    private sealed class RealtimeChangePayload
    {
        public RealtimeChangeData data;
    }

    [Serializable]
    private sealed class RealtimeChangeData
    {
        public string schema;
        public string table;
        public string type;
        public FishData record;
        public FishData old_record;
    }
}
