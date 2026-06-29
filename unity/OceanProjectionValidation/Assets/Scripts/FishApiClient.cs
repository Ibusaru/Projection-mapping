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

    private readonly Dictionary<string, string> seenFishVersions = new Dictionary<string, string>();

    public event Action<IReadOnlyList<FishData>> OnNewFishes;

    private void Start()
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseAnonKey))
        {
            Debug.LogWarning("FishApiClient: Supabase URL or anon key is empty.");
            return;
        }

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
        string url =
            $"{baseUrl}/rest/v1/fishes?select=*&order=updated_at.desc&limit={fetchLimit}";

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
            yield break;
        }

        List<FishData> newFishes = new List<FishData>();

        for (int index = list.items.Length - 1; index >= 0; index--)
        {
            FishData fish = list.items[index];
            if (fish == null || string.IsNullOrWhiteSpace(fish.id))
            {
                continue;
            }

            string fishKey = !string.IsNullOrWhiteSpace(fish.nickname) ? fish.nickname : fish.id;
            string version = !string.IsNullOrWhiteSpace(fish.updated_at) ? fish.updated_at : fish.created_at;
            if (!seenFishVersions.TryGetValue(fishKey, out string seenVersion) || seenVersion != version)
            {
                seenFishVersions[fishKey] = version;
                newFishes.Add(fish);
            }
        }

        if (newFishes.Count > 0)
        {
            Debug.Log($"FishApiClient: received {newFishes.Count} new fish.");
            OnNewFishes?.Invoke(newFishes);
        }
    }
}
