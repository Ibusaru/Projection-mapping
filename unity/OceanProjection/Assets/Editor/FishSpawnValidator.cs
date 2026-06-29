using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FishSpawnValidator
{
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scenePath);

        FishSpawner spawner = Object.FindFirstObjectByType<FishSpawner>();
        if (spawner == null)
        {
            Debug.LogError("FishSpawnValidator: FishSpawner not found.");
            EditorApplication.Exit(2);
            return;
        }

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        float releasedFishTargetLength = serializedSpawner.FindProperty("releasedFishTargetLength").floatValue;
        bool allValid = true;
        allValid &= ValidateSpawnerSettings(serializedSpawner);
        allValid &= ValidatePrefab(serializedSpawner, "clownfishPrefab", releasedFishTargetLength);
        allValid &= ValidatePrefab(serializedSpawner, "jellyfishPrefab", releasedFishTargetLength);
        allValid &= ValidatePrefab(serializedSpawner, "tunaPrefab", releasedFishTargetLength);
        allValid &= ValidatePrefab(serializedSpawner, "originalPrefab", releasedFishTargetLength);

        Debug.Log($"FishSpawnValidator: releasedFishTargetLength={releasedFishTargetLength}");
        Debug.Log($"FishSpawnValidator: releasedFishSpawnSpread={serializedSpawner.FindProperty("releasedFishSpawnSpread").vector3Value}");
        EditorApplication.Exit(allValid ? 0 : 2);
    }

    private static bool ValidateSpawnerSettings(SerializedObject serializedSpawner)
    {
        bool valid = true;
        int defaultFishCount = serializedSpawner.FindProperty("defaultFishCount").intValue;
        int minimumFishCount = serializedSpawner.FindProperty("minimumFishCount").intValue;
        int maxFishCount = serializedSpawner.FindProperty("maxFishCount").intValue;
        SerializedProperty defaultPrefabs = serializedSpawner.FindProperty("defaultFishAlivePrefabs");

        Debug.Log(
            $"FishSpawnValidator: defaultFishCount={defaultFishCount}, minimumFishCount={minimumFishCount}, maxFishCount={maxFishCount}, defaultPrefabCount={defaultPrefabs.arraySize}");

        if (defaultFishCount < 150)
        {
            Debug.LogError("FishSpawnValidator: defaultFishCount should be restored to at least 150.");
            valid = false;
        }

        if (minimumFishCount < 125)
        {
            Debug.LogError("FishSpawnValidator: minimumFishCount should keep the tank populated.");
            valid = false;
        }

        if (maxFishCount < defaultFishCount)
        {
            Debug.LogError("FishSpawnValidator: maxFishCount is lower than defaultFishCount.");
            valid = false;
        }

        if (defaultPrefabs.arraySize == 0)
        {
            Debug.LogError("FishSpawnValidator: defaultFishAlivePrefabs is empty.");
            valid = false;
        }

        return valid;
    }

    private static bool ValidatePrefab(SerializedObject serializedSpawner, string propertyName, float releasedFishTargetLength)
    {
        SerializedProperty property = serializedSpawner.FindProperty(propertyName);
        Object prefabReference = property.objectReferenceValue;
        GameObject prefab = prefabReference as GameObject;
        if (prefab == null && prefabReference is Component component)
        {
            prefab = component.gameObject;
        }

        if (prefab == null)
        {
            string typeName = prefabReference == null ? "null" : prefabReference.GetType().Name;
            Debug.LogError($"FishSpawnValidator: {propertyName} is null, missing, or unsupported. type={typeName}");
            return false;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append($"FishSpawnValidator: {propertyName}='{prefab.name}'");

        Renderer[] prefabRenderers = prefab.GetComponentsInChildren<Renderer>(true);
        builder.Append($", prefabRendererCount={prefabRenderers.Length}");

        GameObject instance = Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;

        Renderer[] instanceRenderers = instance.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer renderer in instanceRenderers)
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

        builder.Append($", instanceRendererCount={instanceRenderers.Length}");
        builder.Append($", hasBounds={hasBounds}");
        int enabledCameraCount = CountEnabled(instance.GetComponentsInChildren<Camera>(true));
        int enabledAudioListenerCount = CountEnabled(instance.GetComponentsInChildren<AudioListener>(true));
        int enabledLightCount = CountEnabled(instance.GetComponentsInChildren<Light>(true));
        builder.Append($", enabledCameras={enabledCameraCount}");
        builder.Append($", enabledAudioListeners={enabledAudioListenerCount}");
        builder.Append($", enabledLights={enabledLightCount}");
        if (hasBounds)
        {
            builder.Append($", boundsCenter={bounds.center}, boundsSize={bounds.size}");
            float currentLength = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (currentLength > 0.001f)
            {
                Vector3 normalizedSize = bounds.size * (Mathf.Max(0.5f, releasedFishTargetLength) / currentLength);
                builder.Append($", mediumNormalizedBoundsSize={normalizedSize}");
            }
        }

        FishActor actor = instance.GetComponent<FishActor>();
        builder.Append($", hasFishActor={actor != null}");
        Debug.Log(builder.ToString());

        Object.DestroyImmediate(instance);
        if (enabledCameraCount > 0 || enabledAudioListenerCount > 0 || enabledLightCount > 0)
        {
            Debug.LogError($"FishSpawnValidator: {propertyName} contains enabled imported view components.");
            return false;
        }

        return hasBounds;
    }

    private static int CountEnabled(Behaviour[] behaviours)
    {
        int count = 0;
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour != null && behaviour.enabled)
            {
                count++;
            }
        }

        return count;
    }
}
