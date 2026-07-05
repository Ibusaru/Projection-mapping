using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OceanEnvironmentSceneCleaner
{
    public static void CleanGeneratedEnvironment()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath);
        int removed = 0;

        OceanEnvironment[] environments = Object.FindObjectsByType<OceanEnvironment>(FindObjectsInactive.Include);

        foreach (OceanEnvironment environment in environments)
        {
            if (environment == null)
            {
                continue;
            }

            for (int i = environment.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = environment.transform.GetChild(i);
                if (child == null || child.name != "_GeneratedOceanEnvironment")
                {
                    continue;
                }

                Object.DestroyImmediate(child.gameObject);
                removed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"OceanEnvironmentSceneCleaner: removed {removed} generated root object(s).");
        EditorApplication.Exit(0);
    }
}
