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
    [SerializeField] private int fetchLimit = 100;
    [SerializeField] private int catchUpPageLimit = 6;

    private readonly Dictionary<string, string> seenFishVersions = new Dictionary<string, string>();

    public event Action<IReadOnlyList<FishData>> OnNewFishes;

    private void Start()
    {
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

        for (int page = 0; page < catchUpPageLimit; page++)
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
                break;
            }

            Debug.Log($"FishApiClient: fetched page {page + 1}/{catchUpPageLimit}, rows={list.items.Length}, seen={seenFishVersions.Count}.");

            for (int index = list.items.Length - 1; index >= 0; index--)
            {
                FishData fish = list.items[index];
                if (fish == null)
                {
                    continue;
                }

                string fishKey = FishKey(fish);
                if (string.IsNullOrWhiteSpace(fishKey))
                {
                    continue;
                }

                string version = !string.IsNullOrWhiteSpace(fish.updated_at) ? fish.updated_at : fish.created_at;
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

            if (list.items.Length < fetchLimit)
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

    private string BuildFetchUrl(string baseUrl, int page)
    {
        int safePage = Mathf.Max(0, page);
        int offset = safePage * fetchLimit;
        return $"{baseUrl}/rest/v1/fishes?select=*&order=updated_at.desc,created_at.desc&limit={fetchLimit}&offset={offset}";
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

        string version = !string.IsNullOrWhiteSpace(fish.updated_at) ? fish.updated_at : fish.created_at;
        if (!string.IsNullOrWhiteSpace(version) || !string.IsNullOrWhiteSpace(fish.nickname))
        {
            return $"{fish.nickname?.Trim()}|{version}";
        }

        return "";
    }
}
