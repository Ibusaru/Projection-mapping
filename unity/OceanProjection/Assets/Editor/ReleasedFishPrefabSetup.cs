using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReleasedFishPrefabSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PrefabFolder = "Assets/Prefabs/ReleasedFish";
    private const string KakukumaModelPath = "Assets/Models/\u30AB\u30AF\u30AF\u30DE\u5B8C.fbx";
    private const string KurageModelPath = "Assets/Models/\u30AF\u30E9\u30B2\uFF12.fbx";
    private const string KakukumaPrefabPath = PrefabFolder + "/ReleasedKakukuma.prefab";
    private const string KuragePrefabPath = PrefabFolder + "/ReleasedKurage.prefab";
    private const string CenteredVisualPivotName = "Centered Visual Pivot";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += Run;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            return;
        }

        EnsureFolder("Assets/Prefabs", "ReleasedFish");

        GameObject kakukumaPrefab = CreateOrUpdatePrefab(KakukumaModelPath, KakukumaPrefabPath, "ReleasedKakukuma");
        GameObject kuragePrefab = CreateOrUpdatePrefab(KurageModelPath, KuragePrefabPath, "ReleasedKurage");
        if (kakukumaPrefab == null || kuragePrefab == null)
        {
            Debug.LogWarning("ReleasedFishPrefabSetup: model-based prefabs could not be prepared.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        FishSpawner spawner = Object.FindAnyObjectByType<FishSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("ReleasedFishPrefabSetup: FishSpawner not found in SampleScene.");
            return;
        }

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        bool changed = false;
        changed |= SetReference(serializedSpawner, "clownfishPrefab", kakukumaPrefab);
        changed |= SetReference(serializedSpawner, "tunaPrefab", kakukumaPrefab);
        changed |= SetReference(serializedSpawner, "originalPrefab", kakukumaPrefab);
        changed |= SetReference(serializedSpawner, "jellyfishPrefab", kuragePrefab);

        if (changed)
        {
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ReleasedFishPrefabSetup: updated FishSpawner to use model-based released fish prefabs.");
        }
    }

    private static GameObject CreateOrUpdatePrefab(string modelPath, string prefabPath, string rootName)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null)
        {
            Debug.LogWarning($"ReleasedFishPrefabSetup: model not found at '{modelPath}'.");
            return null;
        }

        GameObject root = new GameObject(rootName);
        GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
        if (instance == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogWarning($"ReleasedFishPrefabSetup: failed to instantiate model '{modelPath}'.");
            return null;
        }

        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        DisableImportedViewComponents(root);
        FishRendererUtility.GetVisualRenderers(root, true);
        CenterTopLevelVisualsOnRoot(root);
        EnsureProceduralMeshAnimation(root);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static bool SetReference(SerializedObject serializedObject, string propertyName, GameObject value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static void EnsureFolder(string parentFolder, string childFolder)
    {
        string combinedPath = Path.Combine(parentFolder, childFolder).Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(combinedPath))
        {
            return;
        }

        AssetDatabase.CreateFolder(parentFolder, childFolder);
    }

    private static void DisableImportedViewComponents(GameObject root)
    {
        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera != null)
            {
                camera.enabled = false;
            }
        }

        AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
        foreach (AudioListener listener in listeners)
        {
            if (listener != null)
            {
                listener.enabled = false;
            }
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        foreach (Light light in lights)
        {
            if (light != null)
            {
                light.enabled = false;
            }
        }
    }

    private static void CenterTopLevelVisualsOnRoot(GameObject root)
    {
        Renderer[] renderers = FishRendererUtility.GetVisualRenderers(root, false);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        if (localCenter.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Transform pivot = CreateCenteredVisualPivot(root.transform);
        MoveTopLevelChildrenUnderPivot(root.transform, pivot);

        for (int i = 0; i < pivot.childCount; i++)
        {
            Transform child = pivot.GetChild(i);
            child.localPosition -= localCenter;
        }
    }

    private static Transform CreateCenteredVisualPivot(Transform root)
    {
        GameObject pivotObject = new GameObject(CenteredVisualPivotName);
        Transform pivot = pivotObject.transform;
        pivot.SetParent(root, false);
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        return pivot;
    }

    private static void MoveTopLevelChildrenUnderPivot(Transform root, Transform pivot)
    {
        while (root.childCount > 1)
        {
            Transform child = root.GetChild(0);
            if (child == pivot)
            {
                child.SetSiblingIndex(root.childCount - 1);
                continue;
            }

            child.SetParent(pivot, true);
        }
    }

    private static void EnsureProceduralMeshAnimation(GameObject root)
    {
        if (root == null || root.GetComponent<ProceduralFishMeshDeformer>() != null)
        {
            return;
        }

        if (root.GetComponentsInChildren<MeshFilter>(true).Length == 0)
        {
            return;
        }

        root.AddComponent<ProceduralFishMeshDeformer>();
    }
}
