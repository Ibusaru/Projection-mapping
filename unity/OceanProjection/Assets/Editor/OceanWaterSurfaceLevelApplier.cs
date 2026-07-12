using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class OceanWaterSurfaceLevelApplier
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const float TargetWaterSurfaceY = 4.65f;
    private const float MinimumContentClearance = 0.12f;
    private const float CoordinateTolerance = 0.01f;
    private const string StableWaterShaderName = "OceanProjection/Stable Water";
    private const string VerificationLogName = "OceanWaterSurfaceLevelApplier.log";

    static OceanWaterSurfaceLevelApplier()
    {
        EditorApplication.delayCall += ApplyToActiveSampleScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Ocean/Apply Water Surface Level")]
    private static void ApplyFromMenu()
    {
        ApplyToActiveSampleScene();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += ApplyToActiveSampleScene;
        }
    }

    private static void ApplyToActiveSampleScene()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != SampleScenePath)
        {
            return;
        }

        OceanEnvironment environment = Object.FindAnyObjectByType<OceanEnvironment>();
        if (environment == null)
        {
            Debug.LogError("OceanWaterSurfaceLevelApplier: OceanEnvironment was not found in SampleScene.");
            return;
        }

        SerializedObject serializedEnvironment = new SerializedObject(environment);
        SerializedProperty waterSurfaceProperty = serializedEnvironment.FindProperty("waterSurfaceY");
        if (waterSurfaceProperty == null)
        {
            Debug.LogError("OceanWaterSurfaceLevelApplier: waterSurfaceY was not found.");
            return;
        }

        bool serializedValueChanged = !Mathf.Approximately(waterSurfaceProperty.floatValue, TargetWaterSurfaceY);
        if (serializedValueChanged)
        {
            Undo.RecordObject(environment, "Fix Ocean Water Surface Coordinate Path");
            waterSurfaceProperty.floatValue = TargetWaterSurfaceY;
            serializedEnvironment.ApplyModifiedProperties();
            EditorUtility.SetDirty(environment);
        }

        // Generated ocean objects are DontSave. Rebuild the active editor
        // instance explicitly so an already-open scene cannot keep displaying
        // the old in-memory water mesh after the YAML value changes on disk.
        environment.RebuildEnvironment();
        SceneView.RepaintAll();

        if (serializedValueChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        LogAppliedWaterSurface(environment);
    }

    private static void LogAppliedWaterSurface(OceanEnvironment environment)
    {
        Transform generatedRoot = environment.transform.Find("_GeneratedOceanEnvironment");
        Transform waterSurface = generatedRoot != null ? generatedRoot.Find("Water Surface") : null;
        MeshFilter meshFilter = waterSurface != null ? waterSurface.GetComponent<MeshFilter>() : null;
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;

        if (mesh == null)
        {
            const string error = "OceanWaterSurfaceLevelApplier: generated Water Surface mesh was not found after rebuild.";
            WriteVerificationLog(error);
            Debug.LogError(error);
            return;
        }

        float generatedWorldY = waterSurface.TransformPoint(mesh.bounds.center).y;
        float meshLocalY = mesh.bounds.center.y;
        float transformLocalY = waterSurface.localPosition.y;
        MeshRenderer meshRenderer = waterSurface.GetComponent<MeshRenderer>();
        Shader shader = meshRenderer != null && meshRenderer.sharedMaterial != null
            ? meshRenderer.sharedMaterial.shader
            : null;
        string shaderName = shader != null ? shader.name : "<none>";
        bool coordinatePathValid = Mathf.Abs(meshLocalY) <= CoordinateTolerance
            && Mathf.Abs(transformLocalY - environment.WaterSurfaceY) <= CoordinateTolerance
            && shaderName == StableWaterShaderName;
        float highestContentY = FindHighestUnderwaterContentY(environment, generatedRoot, out string highestContentName);
        float contentClearance = float.IsNegativeInfinity(highestContentY)
            ? float.PositiveInfinity
            : generatedWorldY - highestContentY;
        string report =
            $"OceanWaterSurfaceLevelApplier: applied waterSurfaceY={environment.WaterSurfaceY:0.00}, " +
            $"generatedWaterWorldY={generatedWorldY:0.00}, " +
            $"meshLocalY={meshLocalY:0.000}, transformLocalY={transformLocalY:0.000}, shader={shaderName}, " +
            $"highestUnderwaterContentY={highestContentY:0.00}, highestContent={highestContentName}, " +
            $"contentClearance={contentClearance:0.00}, coordinatePathValid={coordinatePathValid}, scene={SampleScenePath}.";
        WriteVerificationLog(report);
        if (contentClearance < MinimumContentClearance || !coordinatePathValid)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }
    }

    private static float FindHighestUnderwaterContentY(
        OceanEnvironment environment,
        Transform generatedRoot,
        out string highestContentName)
    {
        highestContentName = "<none>";
        float highestContentY = float.NegativeInfinity;
        Transform beachRoot = generatedRoot.Find("Beach Props");

        foreach (Renderer renderer in generatedRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null
                || renderer is LineRenderer
                || renderer is ParticleSystemRenderer
                || renderer.name == "Water Surface"
                || renderer.name == "Visible Shoreline"
                || renderer.name == "Seabed"
                || (beachRoot != null && renderer.transform.IsChildOf(beachRoot)))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (!environment.IsUnderwaterWorldPoint(bounds.center, 0f) || bounds.max.y <= highestContentY)
            {
                continue;
            }

            highestContentY = bounds.max.y;
            highestContentName = renderer.name;
        }

        return highestContentY;
    }

    private static void WriteVerificationLog(string message)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        File.WriteAllText(
            Path.Combine(projectRoot, "Library", VerificationLogName),
            message + System.Environment.NewLine
        );
    }
}
