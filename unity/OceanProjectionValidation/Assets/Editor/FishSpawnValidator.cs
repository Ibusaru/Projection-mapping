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
        ValidatePrefab(serializedSpawner, "clownfishPrefab");
        ValidatePrefab(serializedSpawner, "jellyfishPrefab");
        ValidatePrefab(serializedSpawner, "tunaPrefab");
        ValidatePrefab(serializedSpawner, "originalPrefab");

        Debug.Log($"FishSpawnValidator: releasedFishScaleMultiplier={serializedSpawner.FindProperty("releasedFishScaleMultiplier").floatValue}");
        Debug.Log($"FishSpawnValidator: releasedFishSpawnSpread={serializedSpawner.FindProperty("releasedFishSpawnSpread").vector3Value}");
        EditorApplication.Exit(0);
    }

    private static void ValidatePrefab(SerializedObject serializedSpawner, string propertyName)
    {
        SerializedProperty property = serializedSpawner.FindProperty(propertyName);
        GameObject prefab = property.objectReferenceValue as GameObject;
        if (prefab == null)
        {
            Debug.LogError($"FishSpawnValidator: {propertyName} is null or missing.");
            return;
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
        if (hasBounds)
        {
            builder.Append($", boundsCenter={bounds.center}, boundsSize={bounds.size}");
        }

        FishActor actor = instance.GetComponent<FishActor>();
        builder.Append($", hasFishActor={actor != null}");
        Debug.Log(builder.ToString());

        Object.DestroyImmediate(instance);
    }
}
