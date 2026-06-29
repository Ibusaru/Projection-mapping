using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FishApiClient : MonoBehaviour
{
    [Header("Supabase")]
    [SerializeField] private string supabaseUrl = "";
    [SerializeField] private string supabaseAnonKey = "";

    [Header("Polling")]
    [SerializeField] private float pollingSeconds = 8f;
    [SerializeField] private int fetchLimit = 30;
    [SerializeField] private int catchUpPageLimit = 6;

    private readonly Dictionary<string, string> seenFishVersions = new Dictionary<string, string>();
    private string latestSeenUpdatedAt = "";

    public event Action<IReadOnlyList<FishData>> OnNewFishes;

    private void Start()
    {
        supabaseUrl = FirstNonEmpty(
            supabaseUrl,
            Environment.GetEnvironmentVariable("OCEAN_SUPABASE_URL"),
            Environment.GetEnvironmentVariable("SUPABASE_URL"),
            Environment.GetEnvironmentVariable("VITE_SUPABASE_URL")
        );
        supabaseAnonKey = FirstNonEmpty(
            supabaseAnonKey,
            Environment.GetEnvironmentVariable("OCEAN_SUPABASE_ANON_KEY"),
            Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY"),
            Environment.GetEnvironmentVariable("VITE_SUPABASE_ANON_KEY")
        );

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseAnonKey))
        {
            Debug.LogWarning("FishApiClient: Supabase URL or anon key is empty.");
            return;
        }

        fetchLimit = Mathf.Max(1, fetchLimit);
        catchUpPageLimit = Mathf.Max(1, catchUpPageLimit);
        pollingSeconds = Mathf.Max(1f, pollingSeconds);
        StartCoroutine(PollLoop());
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return FetchLatestFishes();
            yield return new WaitForSeconds(pollingSeconds);
        }
    }

    private IEnumerator FetchLatestFishes()
    {
        string baseUrl = supabaseUrl.TrimEnd('/');
        List<FishData> newFishes = new List<FishData>();
        bool catchUpMode = !string.IsNullOrWhiteSpace(latestSeenUpdatedAt);
        int pageLimit = catchUpMode ? catchUpPageLimit : 1;

        for (int page = 0; page < pageLimit; page++)
        {
            string url = BuildFetchUrl(baseUrl, catchUpMode ? latestSeenUpdatedAt : "");
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
                break;
            }

            if (catchUpMode)
            {
                for (int index = 0; index < list.items.Length; index++)
                {
                    CollectNewFish(list.items[index], newFishes);
                }
            }
            else
            {
                for (int index = list.items.Length - 1; index >= 0; index--)
                {
                    CollectNewFish(list.items[index], newFishes);
                }
            }

            string pageLatestVersion = LatestVersionInPage(list.items);
            if (IsLaterVersion(pageLatestVersion, latestSeenUpdatedAt))
            {
                latestSeenUpdatedAt = pageLatestVersion;
            }

            if (!catchUpMode || list.items.Length < fetchLimit)
            {
                break;
            }
        }

        if (newFishes.Count > 0)
        {
            Debug.Log($"FishApiClient: received {newFishes.Count} new fish.");
            OnNewFishes?.Invoke(newFishes);
        }
    }

    private string BuildFetchUrl(string baseUrl, string updatedAfter)
    {
        if (string.IsNullOrWhiteSpace(updatedAfter))
        {
            return $"{baseUrl}/rest/v1/fishes?select=*&order=updated_at.desc&limit={fetchLimit}";
        }

        string escapedCursor = Uri.EscapeDataString(updatedAfter);
        return $"{baseUrl}/rest/v1/fishes?select=*&updated_at=gt.{escapedCursor}&order=updated_at.asc&limit={fetchLimit}";
    }

    private void CollectNewFish(FishData fish, List<FishData> newFishes)
    {
        if (fish == null)
        {
            return;
        }

        string fishKey = !string.IsNullOrWhiteSpace(fish.id) ? fish.id : fish.nickname;
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
        }
    }

    private static string LatestVersionInPage(FishData[] fishes)
    {
        string latest = "";
        if (fishes == null)
        {
            return latest;
        }

        for (int index = 0; index < fishes.Length; index++)
        {
            string version = FishVersion(fishes[index]);
            if (IsLaterVersion(version, latest))
            {
                latest = version;
            }
        }

        return latest;
    }

    private static string FishVersion(FishData fish)
    {
        if (fish == null)
        {
            return "";
        }

        return !string.IsNullOrWhiteSpace(fish.updated_at) ? fish.updated_at : fish.created_at;
    }

    private static bool IsLaterVersion(string candidate, string current)
    {
        return !string.IsNullOrWhiteSpace(candidate)
            && (string.IsNullOrWhiteSpace(current) || string.CompareOrdinal(candidate, current) > 0);
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
}
