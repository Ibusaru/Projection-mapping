using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class OceanAdminCommandClient : MonoBehaviour
{
    [Header("Supabase")]
    [SerializeField] private string supabaseUrl = "";
    [SerializeField] private string supabaseAnonKey = "";

    [Header("References")]
    [SerializeField] private FishSpawner fishSpawner;
    [SerializeField] private OceanAdminCameraController cameraController;

    [Header("Polling")]
    [SerializeField] private float pollingSeconds = 1f;

    private readonly HashSet<string> handledCommandIds = new HashSet<string>();
    private string listenFromUtc;

    [Serializable]
    private class AdminCommandPayload
    {
        public string fish_id;
        public string nickname;
    }

    [Serializable]
    private class AdminCommand
    {
        public string id;
        public string action;
        public AdminCommandPayload payload;
        public string created_at;
        public string expires_at;
    }

    [Serializable]
    private class AdminCommandList
    {
        public AdminCommand[] items;
    }

    private void Awake()
    {
        if (fishSpawner == null)
        {
            fishSpawner = FindAnyObjectByType<FishSpawner>();
        }

        if (cameraController == null)
        {
            cameraController = FindAnyObjectByType<OceanAdminCameraController>();
        }

        if (cameraController == null)
        {
            OceanCameraRig cameraRig = FindAnyObjectByType<OceanCameraRig>();
            if (cameraRig != null)
            {
                cameraController = cameraRig.gameObject.AddComponent<OceanAdminCameraController>();
            }
        }
    }

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
            Debug.LogWarning("OceanAdminCommandClient: Supabase URL or anon key is empty.");
            return;
        }

        pollingSeconds = Mathf.Max(0.5f, pollingSeconds);
        listenFromUtc = DateTime.UtcNow.AddSeconds(-2).ToString("o");
        StartCoroutine(PollLoop());
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return FetchCommands();
            yield return new WaitForSecondsRealtime(pollingSeconds);
        }
    }

    private IEnumerator FetchCommands()
    {
        string encodedStart = UnityWebRequest.EscapeURL(listenFromUtc);
        string url = $"{supabaseUrl.TrimEnd('/')}/rest/v1/admin_commands"
            + $"?select=id,action,payload,created_at,expires_at&created_at=gte.{encodedStart}&order=created_at.asc";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("apikey", supabaseAnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"OceanAdminCommandClient: fetch failed: {request.error} ({request.responseCode})");
            yield break;
        }

        string wrappedJson = $"{{\"items\":{request.downloadHandler.text}}}";
        AdminCommandList list = JsonUtility.FromJson<AdminCommandList>(wrappedJson);
        if (list?.items == null)
        {
            yield break;
        }

        foreach (AdminCommand command in list.items)
        {
            if (command == null
                || string.IsNullOrWhiteSpace(command.id)
                || !handledCommandIds.Add(command.id))
            {
                continue;
            }

            ExecuteCommand(command);
        }
    }

    private void ExecuteCommand(AdminCommand command)
    {
        string fishId = command.payload?.fish_id ?? "";
        switch (command.action)
        {
            case "camera_aerial":
                cameraController?.ShowAerialView();
                break;
            case "camera_roam":
                cameraController?.ResumeRoam();
                break;
            case "camera_focus":
                cameraController?.FocusFish(fishId);
                break;
            case "delete_fish":
                fishSpawner?.DeleteReleasedFish(fishId);
                break;
            default:
                Debug.LogWarning($"OceanAdminCommandClient: unknown action '{command.action}'.");
                break;
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
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
        Dictionary<string, string> values = new Dictionary<string, string>();
        string path = FindLocalEnvPath();
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
            Debug.LogWarning($"OceanAdminCommandClient: failed to read web/.env.local: {exception.Message}");
        }
        return values;
    }

    private static bool TryParseEnvLine(string line, out string key, out string value)
    {
        key = "";
        value = "";
        if (string.IsNullOrWhiteSpace(line)) return false;

        string trimmed = line.Trim();
        if (trimmed.StartsWith("#")) return false;
        if (trimmed.StartsWith("export ", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(7).TrimStart();
        }

        int separator = trimmed.IndexOf('=');
        if (separator <= 0) return false;
        key = trimmed.Substring(0, separator).Trim();
        value = NormalizeEnvValue(trimmed.Substring(separator + 1).Trim());
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string NormalizeEnvValue(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[value.Length - 1] == '"')
                || (value[0] == '\'' && value[value.Length - 1] == '\'')))
        {
            return value.Substring(1, value.Length - 2);
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
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "web", ".env.local"))
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }
        return "";
    }
}
