using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class OceanValidationCapture
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const string OutputFolder = "Assets/ValidationScreenshots";

    [MenuItem("Tools/Ocean/Reload Active Scene From Disk")]
    public static void ReloadActiveSceneFromDisk()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            Debug.LogError("OceanValidationCapture: active scene has no asset path.");
            return;
        }

        EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
        Debug.Log($"OceanValidationCapture: reloaded {activeScene.path} from disk.");
    }

    [MenuItem("Tools/Ocean/Capture Validation Views")]
    public static void CaptureAll()
    {
        if (Application.isBatchMode && SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        OceanCameraRig rig = Object.FindAnyObjectByType<OceanCameraRig>();
        OceanEnvironment environment = Object.FindAnyObjectByType<OceanEnvironment>();
        if (camera == null || rig == null || environment == null)
        {
            Debug.LogError("OceanValidationCapture: Main Camera, OceanCameraRig, or OceanEnvironment is missing.");
            return;
        }

        Directory.CreateDirectory(OutputFolder);
        Pose originalPose = new Pose(camera.transform.position, camera.transform.rotation);
        float originalFov = camera.fieldOfView;
        RenderTexture originalTarget = camera.targetTexture;

        try
        {
            DumpBeachDiagnostics(environment);
            CaptureCinematic(camera, rig, OceanCinematicShotKind.DroneOverview, 0f, 52f, "01_drone_start.png");
            CaptureCinematic(camera, rig, OceanCinematicShotKind.DroneOverview, 0.5f, 52f, "02_drone_mid.png");
            CaptureCinematic(camera, rig, OceanCinematicShotKind.DroneOverview, 1f, 52f, "03_drone_end_before_cut.png");
            CaptureLowBeach(camera, environment, "04_low_beach.png");
            // The opening of ReefDive intentionally looks up through the
            // surface.  Use the following exploration frame for a stable
            // editor capture that still shows the reef below that entry.
            CaptureCinematic(camera, rig, OceanCinematicShotKind.UnderwaterExplore, 0.5f, 48f, "05_underwater_after_entry.png");
        }
        finally
        {
            camera.transform.SetPositionAndRotation(originalPose.position, originalPose.rotation);
            camera.fieldOfView = originalFov;
            camera.targetTexture = originalTarget;
            AssetDatabase.Refresh();
        }

        Debug.Log($"OceanValidationCapture: wrote five views to {OutputFolder}.");
    }

    private static void DumpBeachDiagnostics(OceanEnvironment environment)
    {
        Transform generated = environment.transform.Find("_GeneratedOceanEnvironment");
        Transform beach = generated != null ? generated.Find("Beach Props") : null;
        StringBuilder report = new StringBuilder();
        report.AppendLine($"hasAllBeachPrefabs={environment.HasAllBeachPrefabs()}");
        report.AppendLine($"build={environment.DescribeBeachDecorationBuild()}");
        report.AppendLine($"generatedRoot={(generated != null ? generated.name : "<missing>")}");
        report.AppendLine($"beachRoot={(beach != null ? beach.name : "<missing>")}");
        FishActor[] activeFish = Object.FindObjectsByType<FishActor>(FindObjectsSortMode.None);
        int emperorAngelfish = 0;
        for (int i = 0; i < activeFish.Length; i++)
        {
            FishActor fish = activeFish[i];
            if (fish != null && fish.name.IndexOf("EmperorAngelfish", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                emperorAngelfish++;
            }
        }
        report.AppendLine($"fish active={activeFish.Length}, emperorAngelfish={emperorAngelfish}");
        if (beach != null)
        {
            int palm = 0;
            int parasol = 0;
            int sunbed = 0;
            int desk = 0;
            int rendererCount = 0;
            foreach (Transform item in beach.GetComponentsInChildren<Transform>(true))
            {
                if (item == beach)
                {
                    continue;
                }

                if (item.name.StartsWith("Beach Palm", System.StringComparison.Ordinal)) palm++;
                if (item.name.StartsWith("Beach Parasol", System.StringComparison.Ordinal)) parasol++;
                if (item.name.StartsWith("Beach Sunbed", System.StringComparison.Ordinal)) sunbed++;
                if (item.name.StartsWith("Beach Desk", System.StringComparison.Ordinal)) desk++;
                rendererCount += item.GetComponents<Renderer>().Length;
                report.AppendLine($"{item.name}: position={item.position}, active={item.gameObject.activeSelf}, renderers={item.GetComponents<Renderer>().Length}");
            }

            report.AppendLine($"counts palms={palm}, parasols={parasol}, sunbeds={sunbed}, desks={desk}, renderers={rendererCount}");
            report.AppendLine($"directChildren={beach.childCount}");
            for (int i = 0; i < beach.childCount; i++)
            {
                Transform direct = beach.GetChild(i);
                Renderer[] directRenderers = direct.GetComponentsInChildren<Renderer>(true);
                Bounds directBounds = default;
                bool hasBounds = false;
                for (int j = 0; j < directRenderers.Length; j++)
                {
                    if (directRenderers[j] == null) continue;
                    if (!hasBounds) { directBounds = directRenderers[j].bounds; hasBounds = true; }
                    else directBounds.Encapsulate(directRenderers[j].bounds);
                }
                report.AppendLine($"DIRECT {i}: name={direct.name}, position={direct.position}, childCount={direct.childCount}, bounds={(hasBounds ? directBounds.ToString() : "<none>")}");
            }
        }

        string path = Path.Combine(Application.dataPath, "ValidationScreenshots", "beach-diagnostics.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log($"OceanValidationCapture: beach diagnostics written to {path}.\n{report}");
    }

    private static void CaptureCinematic(
        Camera camera,
        OceanCameraRig rig,
        OceanCinematicShotKind shot,
        float normalizedTime,
        float fieldOfView,
        string fileName)
    {
        if (!rig.TryEvaluateCinematicShot(shot, normalizedTime, out Vector3 position, out Vector3 lookTarget))
        {
            Debug.LogError($"OceanValidationCapture: could not evaluate {shot} at t={normalizedTime:0.00}.");
            return;
        }

        Capture(camera, position, lookTarget, fieldOfView, fileName);
    }

    private static void CaptureLowBeach(Camera camera, OceanEnvironment environment, string fileName)
    {
        environment.GetDroneInterestPoints(out Vector3 beachCenter, out Vector3 waterInterest);
        Vector3 shoreward = beachCenter - waterInterest;
        shoreward.y = 0f;
        shoreward = shoreward.sqrMagnitude > 0.001f ? shoreward.normalized : Vector3.right;
        Vector3 lateral = Vector3.Cross(Vector3.up, shoreward).normalized;
        Vector3 position = beachCenter - shoreward * 34f - lateral * 18f + Vector3.up * 11f;
        Vector3 lookTarget = beachCenter + lateral * 2f + Vector3.up * 2.8f;
        Capture(camera, position, lookTarget, 52f, fileName);
    }

    private static void Capture(
        Camera camera,
        Vector3 position,
        Vector3 lookTarget,
        float fieldOfView,
        string fileName)
    {
        Vector3 direction = lookTarget - position;
        if (direction.sqrMagnitude < 0.001f)
        {
            Debug.LogError($"OceanValidationCapture: invalid view direction for {fileName}.");
            return;
        }

        camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction.normalized, Vector3.up));
        camera.fieldOfView = fieldOfView;

        RenderTexture renderTexture = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            image.Apply(false, false);
            File.WriteAllBytes(Path.Combine(OutputFolder, fileName), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }
}
