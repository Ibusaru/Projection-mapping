using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OceanEnvironmentValidator
{
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scenePath);

        OceanEnvironment environment = Object.FindAnyObjectByType<OceanEnvironment>();
        OceanCameraRig cameraRig = Object.FindAnyObjectByType<OceanCameraRig>();
        bool valid = true;

        if (environment == null)
        {
            Debug.LogError("OceanEnvironmentValidator: OceanEnvironment not found.");
            EditorApplication.Exit(2);
            return;
        }

        valid &= ValidateFeaturePoints(environment);
        valid &= ValidateHeightRange(environment);

        if (cameraRig == null)
        {
            Debug.LogError("OceanEnvironmentValidator: OceanCameraRig not found.");
            valid = false;
        }
        else
        {
            valid &= ValidateCameraShots(environment, cameraRig);
        }

        EditorApplication.Exit(valid ? 0 : 2);
    }

    private static bool ValidateFeaturePoints(OceanEnvironment environment)
    {
        bool valid = true;
        OceanFeatureKind[] features =
        {
            OceanFeatureKind.Overview,
            OceanFeatureKind.Beach,
            OceanFeatureKind.Reef,
            OceanFeatureKind.Trench,
            OceanFeatureKind.RockMountain
        };

        foreach (OceanFeatureKind feature in features)
        {
            bool found = environment.TryGetFeaturePoint(feature, out Vector3 point);
            Debug.Log($"OceanEnvironmentValidator: feature={feature}, found={found}, point={point}");
            if (!found || !IsFinite(point))
            {
                Debug.LogError($"OceanEnvironmentValidator: invalid feature point for {feature}.");
                valid = false;
            }
        }

        return valid;
    }

    private static bool ValidateHeightRange(OceanEnvironment environment)
    {
        Vector2 size = environment.OceanSize;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        const int samples = 41;

        for (int z = 0; z < samples; z++)
        {
            float tz = z / (float)(samples - 1);
            float localZ = Mathf.Lerp(-size.y * 0.5f, size.y * 0.5f, tz);
            for (int x = 0; x < samples; x++)
            {
                float tx = x / (float)(samples - 1);
                float localX = Mathf.Lerp(-size.x * 0.5f, size.x * 0.5f, tx);
                float height = environment.SampleSeabedHeight(localX, localZ);
                min = Mathf.Min(min, height);
                max = Mathf.Max(max, height);
            }
        }

        bool hasMountain = max >= environment.WaterSurfaceY + 5.5f;
        bool hasTrench = min <= -15f;
        Debug.Log(
            $"OceanEnvironmentValidator: heightRange min={min:0.00}, max={max:0.00}, " +
            $"water={environment.WaterSurfaceY:0.00}, hasMountain={hasMountain}, hasTrench={hasTrench}"
        );

        if (!hasMountain)
        {
            Debug.LogError("OceanEnvironmentValidator: terrain maximum is not high enough above the water.");
        }

        if (!hasTrench)
        {
            Debug.LogError("OceanEnvironmentValidator: terrain minimum is not deep enough for a trench.");
        }

        return hasMountain && hasTrench;
    }

    private static bool ValidateCameraShots(OceanEnvironment environment, OceanCameraRig cameraRig)
    {
        bool valid = true;
        OceanCinematicShotKind[] shots =
        {
            OceanCinematicShotKind.DroneOverview,
            OceanCinematicShotKind.SurfaceSkim,
            OceanCinematicShotKind.ReefDive,
            OceanCinematicShotKind.TrenchRun,
            OceanCinematicShotKind.RockMountainReveal
        };

        foreach (OceanCinematicShotKind shot in shots)
        {
            valid &= ValidateCameraShot(environment, cameraRig, shot, 0f);
            valid &= ValidateCameraShot(environment, cameraRig, shot, 0.5f);
            valid &= ValidateCameraShot(environment, cameraRig, shot, 1f);
        }

        return valid;
    }

    private static bool ValidateCameraShot(OceanEnvironment environment, OceanCameraRig cameraRig, OceanCinematicShotKind shot, float t)
    {
        bool evaluated = cameraRig.TryEvaluateCinematicShot(shot, t, out Vector3 position, out Vector3 lookTarget);
        Vector3 localPosition = environment.transform.InverseTransformPoint(position);
        Vector2 size = environment.OceanSize;
        bool finite = IsFinite(position) && IsFinite(lookTarget);
        bool inRange = Mathf.Abs(localPosition.x) <= size.x * 0.72f
            && Mathf.Abs(localPosition.z) <= size.y * 0.72f
            && localPosition.y >= -26f
            && localPosition.y <= environment.WaterSurfaceY + Mathf.Max(size.x, size.y) * 0.45f;

        Debug.Log(
            $"OceanEnvironmentValidator: shot={shot}, t={t:0.00}, evaluated={evaluated}, " +
            $"position={position}, lookTarget={lookTarget}, inRange={inRange}"
        );

        if (!evaluated || !finite || !inRange)
        {
            Debug.LogError($"OceanEnvironmentValidator: invalid camera shot target. shot={shot}, t={t:0.00}");
            return false;
        }

        return true;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
