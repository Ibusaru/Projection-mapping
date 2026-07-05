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
        valid &= ValidateFeatureSpacing(environment);
        valid &= ValidateGeneratedRendering(environment);
        valid &= ValidatePandazoleMaterial();

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

        bool hasBeachOrShallows = max >= environment.WaterSurfaceY - 0.45f;
        bool noTallIsland = max <= environment.WaterSurfaceY + 2.05f;
        bool hasTrench = min <= -15f;
        bool hasSubmergedRockyField = environment.TryGetFeaturePoint(OceanFeatureKind.RockMountain, out Vector3 rockyPoint)
            && rockyPoint.y < environment.WaterSurfaceY - 0.65f
            && rockyPoint.y > environment.WaterSurfaceY - 10.5f;
        Debug.Log(
            $"OceanEnvironmentValidator: heightRange min={min:0.00}, max={max:0.00}, " +
            $"water={environment.WaterSurfaceY:0.00}, hasBeachOrShallows={hasBeachOrShallows}, " +
            $"noTallIsland={noTallIsland}, hasTrench={hasTrench}, hasSubmergedRockyField={hasSubmergedRockyField}"
        );

        if (!hasBeachOrShallows)
        {
            Debug.LogError("OceanEnvironmentValidator: terrain maximum does not reach a shallow beach shelf.");
        }

        if (!noTallIsland)
        {
            Debug.LogError("OceanEnvironmentValidator: terrain creates a tall island above the waterline.");
        }

        if (!hasTrench)
        {
            Debug.LogError("OceanEnvironmentValidator: terrain minimum is not deep enough for a trench.");
        }

        if (!hasSubmergedRockyField)
        {
            Debug.LogError("OceanEnvironmentValidator: rocky field feature should stay below the water surface.");
        }

        return hasBeachOrShallows && noTallIsland && hasTrench && hasSubmergedRockyField;
    }

    private static bool ValidateFeatureSpacing(OceanEnvironment environment)
    {
        bool hasBeach = environment.TryGetFeaturePoint(OceanFeatureKind.Beach, out Vector3 beach);
        bool hasTrench = environment.TryGetFeaturePoint(OceanFeatureKind.Trench, out Vector3 trench);
        bool hasRocky = environment.TryGetFeaturePoint(OceanFeatureKind.RockMountain, out Vector3 rocky);
        if (!hasBeach || !hasTrench || !hasRocky)
        {
            Debug.LogError("OceanEnvironmentValidator: cannot validate feature spacing because a feature point is missing.");
            return false;
        }

        float beachTrenchDistance = HorizontalDistance(beach, trench);
        float beachRockyDistance = HorizontalDistance(beach, rocky);
        float minimumPrimaryGap = Mathf.Max(environment.OceanSize.x, environment.OceanSize.y) * 0.46f;
        bool beachAwayFromTrench = beachTrenchDistance >= minimumPrimaryGap;
        bool beachAwayFromRocky = beachRockyDistance >= minimumPrimaryGap * 0.78f;
        Debug.Log(
            $"OceanEnvironmentValidator: featureSpacing beachTrench={beachTrenchDistance:0.00}, " +
            $"beachRocky={beachRockyDistance:0.00}, beachAwayFromTrench={beachAwayFromTrench}, " +
            $"beachAwayFromRocky={beachAwayFromRocky}"
        );

        if (!beachAwayFromTrench)
        {
            Debug.LogError("OceanEnvironmentValidator: beach and trench are too close.");
        }

        if (!beachAwayFromRocky)
        {
            Debug.LogError("OceanEnvironmentValidator: beach and rocky field are too close.");
        }

        return beachAwayFromTrench && beachAwayFromRocky;
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

    private static bool ValidateGeneratedRendering(OceanEnvironment environment)
    {
        Transform root = environment.transform.Find("_GeneratedOceanEnvironment");
        if (root == null)
        {
            Debug.LogError("OceanEnvironmentValidator: generated environment root was not created.");
            return false;
        }

        bool valid = true;
        bool dontSave = (root.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        MeshCollider seabedCollider = root.GetComponentInChildren<MeshCollider>(true);
        bool hasSuimonoComponent = HasGeneratedSuimonoComponent(root);
        Debug.Log(
            $"OceanEnvironmentValidator: generatedRootDontSave={dontSave}, rendererCount={renderers.Length}, " +
            $"hasSeabedCollider={seabedCollider != null}, hasSuimonoComponent={hasSuimonoComponent}"
        );

        if (!dontSave)
        {
            Debug.LogError("OceanEnvironmentValidator: generated environment root can be saved into the scene.");
            valid = false;
        }

        if (renderers.Length == 0)
        {
            Debug.LogError("OceanEnvironmentValidator: generated environment has no renderers.");
            return false;
        }

        if (seabedCollider == null || seabedCollider.sharedMesh == null)
        {
            Debug.LogError("OceanEnvironmentValidator: generated seabed is missing a MeshCollider.");
            valid = false;
        }

        if (hasSuimonoComponent)
        {
            Debug.LogError("OceanEnvironmentValidator: generated environment should use the stable procedural water, not Suimono components.");
            valid = false;
        }

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                Debug.LogError($"OceanEnvironmentValidator: renderer has no material. renderer={renderer.name}");
                valid = false;
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                Shader shader = material != null ? material.shader : null;
                if (shader != null && shader.isSupported && shader.name != "Hidden/InternalErrorShader")
                {
                    continue;
                }

                Debug.LogError(
                    $"OceanEnvironmentValidator: unsupported generated material. " +
                    $"renderer={renderer.name}, material={(material != null ? material.name : "null")}, " +
                    $"shader={(shader != null ? shader.name : "null")}"
                );
                valid = false;
            }
        }

        return valid;
    }

    private static bool HasGeneratedSuimonoComponent(Transform root)
    {
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().FullName;
            if (!string.IsNullOrEmpty(typeName)
                && typeName.ToLowerInvariant().Contains("suimono"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValidatePandazoleMaterial()
    {
        const string materialPath = "Assets/Pandazole_Ultimate_Pack/Pandazole Nature Environment Pack/Materials/PandaMat.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Debug.Log("OceanEnvironmentValidator: Pandazole material not found; skipping optional asset check.");
            return true;
        }

        Shader shader = material.shader;
        bool valid = shader != null
            && shader.isSupported
            && shader.name != "Standard"
            && shader.name != "Hidden/InternalErrorShader";

        Debug.Log(
            $"OceanEnvironmentValidator: pandazoleMaterial={material.name}, " +
            $"shader={(shader != null ? shader.name : "null")}, valid={valid}"
        );

        if (!valid)
        {
            Debug.LogError("OceanEnvironmentValidator: Pandazole material is not URP-safe.");
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

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
