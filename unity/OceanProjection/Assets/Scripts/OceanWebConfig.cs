using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Both clients wait for one shared snapshot so they cannot select different databases.
public static class OceanWebConfig
{
    [Serializable] private sealed class Config { public string supabaseUrl; public string supabaseAnonKey; }
    private static bool started;
    private static bool finished;
    private static Config config;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { started = false; finished = false; config = null; }

    public static IEnumerator Load(Action<string, string> completed)
    {
        if (!started)
        {
            started = true;
            string baseUrl = FindWebUrl();
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                string url = baseUrl.TrimEnd('/') + "/ocean-config.json?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 15;
                    request.SetRequestHeader("Cache-Control", "no-cache");
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try { config = JsonUtility.FromJson<Config>(request.downloadHandler.text); }
                        catch (Exception) { config = null; }
                    }
                }
            }
            if (config == null || !Uri.TryCreate(config.supabaseUrl, UriKind.Absolute, out Uri target) ||
                (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp) ||
                string.IsNullOrWhiteSpace(config.supabaseAnonKey))
            {
                config = null;
                Debug.LogError("Ocean: Web connection settings could not be loaded. Check ocean-web-url.txt and the deployed website, then restart.");
            }
            finished = true;
        }
        else
        {
            while (!finished) yield return null;
        }
        completed(config?.supabaseUrl, config?.supabaseAnonKey);
    }

    public static string FindWebUrl()
    {
        string value = Environment.GetEnvironmentVariable("OCEAN_WEB_URL");
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        string path = Path.Combine(Application.dataPath, "..", "ocean-web-url.txt");
        try { if (File.Exists(path)) return File.ReadAllText(path).Trim(); }
        catch (IOException) { }
#if UNITY_EDITOR
        string localEnv = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "web", ".env.local"));
        try
        {
            if (File.Exists(localEnv))
                foreach (string line in File.ReadLines(localEnv))
                    if (line.StartsWith("VITE_PUBLIC_APP_URL=", StringComparison.Ordinal))
                        return line.Substring("VITE_PUBLIC_APP_URL=".Length).Trim().Trim('"', '\'');
        }
        catch (IOException) { }
#endif
        return "";
    }
}
