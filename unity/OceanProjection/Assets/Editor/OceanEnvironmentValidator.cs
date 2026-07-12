using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

public static class OceanEnvironmentValidator
{
    [MenuItem("Tools/Ocean/Run Environment Validator")]
    public static void RunFromMenu()
    {
        RunInternal(false);
    }

    public static void Run()
    {
        RunInternal(true);
    }

    private static void RunInternal(bool exitEditor)
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scenePath);

        OceanEnvironment environment = Object.FindAnyObjectByType<OceanEnvironment>();
        OceanCameraRig cameraRig = Object.FindAnyObjectByType<OceanCameraRig>();
        bool valid = true;

        if (environment == null)
        {
            Debug.LogError("OceanEnvironmentValidator: OceanEnvironment not found.");
            if (exitEditor)
            {
                EditorApplication.Exit(2);
            }
            return;
        }

        valid &= ValidateFeaturePoints(environment);
        valid &= ValidateHeightRange(environment);
        valid &= ValidateFeatureSpacing(environment);
        valid &= ValidateGeneratedRendering(environment);
        valid &= ValidateSingleShoreline(environment);
        valid &= ValidateBeachDecorations(environment);
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

        Debug.Log($"OceanEnvironmentValidator: completed valid={valid}");
        if (exitEditor)
        {
            EditorApplication.Exit(valid ? 0 : 2);
        }
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
        float transitionMin = float.PositiveInfinity;
        float transitionMax = float.NegativeInfinity;
        const int samples = 41;

        for (int z = 0; z < samples; z++)
        {
            float tz = z / (float)(samples - 1);
            float localZ = Mathf.Lerp(-size.y * 0.5f, size.y * 0.5f, tz);
            float transitionHeight = environment.SampleSandMaterialTransitionHeight(localZ);
            transitionMin = Mathf.Min(transitionMin, transitionHeight);
            transitionMax = Mathf.Max(transitionMax, transitionHeight);
            for (int x = 0; x < samples; x++)
            {
                float tx = x / (float)(samples - 1);
                float localX = Mathf.Lerp(-size.x * 0.5f, size.x * 0.5f, tx);
                float height = environment.SampleSeabedHeight(localX, localZ);
                min = Mathf.Min(min, height);
                max = Mathf.Max(max, height);
            }
        }

        float transitionClearance = environment.WaterSurfaceY - transitionMax;
        bool waterReachesSandTransition = transitionClearance >= 0f;
        bool dryBeachNotFlooded = transitionClearance <= 0.25f;
        bool noTallIsland = max <= environment.WaterSurfaceY + 2.05f;
        bool hasTrench = min <= -15f;
        bool hasSubmergedRockyField = environment.TryGetFeaturePoint(OceanFeatureKind.RockMountain, out Vector3 rockyPoint)
            && rockyPoint.y < environment.WaterSurfaceY - 0.65f
            && rockyPoint.y > environment.WaterSurfaceY - 12.5f;
        Debug.Log(
            $"OceanEnvironmentValidator: heightRange min={min:0.00}, max={max:0.00}, " +
            $"transitionRange={transitionMin:0.00}..{transitionMax:0.00}, water={environment.WaterSurfaceY:0.00}, " +
            $"transitionClearance={transitionClearance:0.00}, waterReachesSandTransition={waterReachesSandTransition}, " +
            $"dryBeachNotFlooded={dryBeachNotFlooded}, " +
            $"noTallIsland={noTallIsland}, hasTrench={hasTrench}, hasSubmergedRockyField={hasSubmergedRockyField}"
        );

        if (!waterReachesSandTransition)
        {
            Debug.LogError("OceanEnvironmentValidator: water surface is below the sand texture transition height.");
        }

        if (!dryBeachNotFlooded)
        {
            Debug.LogError("OceanEnvironmentValidator: water surface is too far above the sand texture transition and floods the dry beach.");
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

        return waterReachesSandTransition && dryBeachNotFlooded && noTallIsland && hasTrench && hasSubmergedRockyField;
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

        valid &= ValidateDroneOverview(environment, cameraRig);
        valid &= ValidateDroneCut(cameraRig);

        return valid;
    }

    private static bool ValidateDroneOverview(OceanEnvironment environment, OceanCameraRig cameraRig)
    {
        Camera camera = cameraRig.GetComponent<Camera>();
        if (camera == null)
        {
            Debug.LogError("OceanEnvironmentValidator: DroneOverview camera is missing.");
            return false;
        }

        environment.GetDroneInterestPoints(out Vector3 showcaseCenter, out Vector3 waterInterest);
        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        float originalFov = camera.fieldOfView;
        bool valid = true;
        float minimumHeight = environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY + 25f, 0f)).y;
        // The overview now starts higher so the complete water/beach story is
        // readable in one frame instead of presenting a close beach flyover.
        float maximumHeight = environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY + 150f, 0f)).y;
        for (int i = 0; i <= 4; i++)
        {
            float t = i / 4f;
            bool evaluated = cameraRig.TryEvaluateCinematicShot(OceanCinematicShotKind.DroneOverview, t, out Vector3 position, out Vector3 lookTarget);
            Vector3 forward = (lookTarget - position).normalized;
            float downwardAngle = Mathf.Asin(Mathf.Clamp(-forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            Vector3 beachViewport = camera.WorldToViewportPoint(showcaseCenter);
            Vector3 waterViewport = camera.WorldToViewportPoint(waterInterest);
            bool beachVisible = IsViewportPointVisible(beachViewport);
            bool waterVisible = IsViewportPointVisible(waterViewport);
            bool heightValid = position.y >= minimumHeight && position.y <= maximumHeight;
            bool angleValid = downwardAngle >= 35f && downwardAngle <= 55f;
            valid &= evaluated && heightValid && angleValid && beachVisible && waterVisible;
            Debug.Log(
                $"OceanEnvironmentValidator: drone t={t:0.00}, position={position}, downAngle={downwardAngle:0.0}, " +
                $"beachViewport={beachViewport}, waterViewport={waterViewport}, heightValid={heightValid}, angleValid={angleValid}"
            );
        }

        camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
        camera.fieldOfView = originalFov;
        bool fovValid = originalFov >= 48f && originalFov <= 56.5f;
        bool farClipValid = camera.farClipPlane >= 500f && camera.farClipPlane <= 1200f;
        valid &= fovValid && farClipValid;
        Debug.Log(
            $"OceanEnvironmentValidator: droneOverview fov={originalFov:0.0}, farClip={camera.farClipPlane:0.0}, " +
            $"fovValid={fovValid}, farClipValid={farClipValid}, valid={valid}"
        );

        if (!valid)
        {
            Debug.LogError("OceanEnvironmentValidator: DroneOverview ROI, height, pitch, FOV, or interest-point framing is invalid.");
        }

        return valid;
    }

    private static bool ValidateDroneCut(OceanCameraRig cameraRig)
    {
        bool valid = cameraRig.UsesContinuousDroneWaterEntry && !cameraRig.UsesDroneToUnderwaterCut;
        Debug.Log($"OceanEnvironmentValidator: droneToUnderwaterUsesContinuousEntry={valid}");
        if (!valid)
        {
            Debug.LogError("OceanEnvironmentValidator: DroneOverview must enter underwater continuously without a fade cut.");
        }

        return valid;
    }

    private static bool IsViewportPointVisible(Vector3 point)
    {
        return point.z > 0f
            && point.x >= 0.03f && point.x <= 0.97f
            && point.y >= 0.03f && point.y <= 0.97f;
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

        valid &= ValidateWaterSurfaceCoordinatePath(environment, root);

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

    private static bool ValidateWaterSurfaceCoordinatePath(OceanEnvironment environment, Transform root)
    {
        const string expectedShaderName = "OceanProjection/Stable Water";
        const float coordinateTolerance = 0.01f;

        Transform waterSurface = root.Find("Water Surface");
        MeshFilter meshFilter = waterSurface != null ? waterSurface.GetComponent<MeshFilter>() : null;
        MeshRenderer meshRenderer = waterSurface != null ? waterSurface.GetComponent<MeshRenderer>() : null;
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        Material material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
        Shader shader = material != null ? material.shader : null;
        if (waterSurface == null || mesh == null || shader == null)
        {
            Debug.LogError("OceanEnvironmentValidator: Water Surface transform, mesh, material, or shader is missing.");
            return false;
        }

        float meshLocalY = mesh.bounds.center.y;
        Vector3 surfaceLocalPosition = waterSurface.localPosition;
        Vector3 expectedWorldPosition = environment.transform.TransformPoint(new Vector3(0f, environment.WaterSurfaceY, 0f));
        Vector3 generatedWorldPosition = waterSurface.TransformPoint(new Vector3(0f, meshLocalY, 0f));
        bool meshUsesLocalZero = Mathf.Abs(meshLocalY) <= coordinateTolerance;
        bool transformOwnsWaterLevel = Mathf.Abs(surfaceLocalPosition.y - environment.WaterSurfaceY) <= coordinateTolerance
            && Mathf.Abs(surfaceLocalPosition.x) <= coordinateTolerance
            && Mathf.Abs(surfaceLocalPosition.z) <= coordinateTolerance;
        bool worldLevelMatches = Vector3.Distance(generatedWorldPosition, expectedWorldPosition) <= coordinateTolerance;
        bool usesStableShader = shader.name == expectedShaderName;

        Debug.Log(
            $"OceanEnvironmentValidator: waterCoordinatePath meshLocalY={meshLocalY:0.000}, " +
            $"transformLocalPosition={surfaceLocalPosition}, generatedWorldY={generatedWorldPosition.y:0.000}, " +
            $"expectedWorldY={expectedWorldPosition.y:0.000}, shader={shader.name}, " +
            $"meshUsesLocalZero={meshUsesLocalZero}, transformOwnsWaterLevel={transformOwnsWaterLevel}, " +
            $"worldLevelMatches={worldLevelMatches}, usesStableShader={usesStableShader}"
        );

        if (!meshUsesLocalZero || !transformOwnsWaterLevel || !worldLevelMatches || !usesStableShader)
        {
            Debug.LogError(
                "OceanEnvironmentValidator: Water Surface must use a local-zero mesh, put waterSurfaceY on its Transform, " +
                $"and render with {expectedShaderName}."
            );
            return false;
        }

        return true;
    }

    private static bool ValidateSingleShoreline(OceanEnvironment environment)
    {
        Transform root = environment.transform.Find("_GeneratedOceanEnvironment");
        if (root == null)
        {
            Debug.LogError("OceanEnvironmentValidator: generated environment root was not created.");
            return false;
        }

        int visibleShorelines = CountNamedChildren(root, "Visible Shoreline");
        int generatedRootCount = CountDirectChildren(environment.transform, "_GeneratedOceanEnvironment");
        bool hasHorizontalBeach = FindNamedChild(root, "Horizontal Beach") != null;
        bool hasDistantSand = FindNamedChild(root, "Distant Beach Sand Continuation") != null;
        bool hasSplitBackdrop = HasChildNameFragment(root, "Open Ocean Horizon Water")
            || FindNamedChild(root, "Open Ocean Deep Floor") != null;
        bool hasFixedFoam = HasNamedLineRenderer(root, "Foam") || HasNamedLineRenderer(root, "Surface Sparkle");
        bool hasSunbeamLines = HasNamedLineRenderer(root, "Sunbeam");
        bool complementaryGeometry = environment.HasComplementaryShorelineGeometry();
        bool valid = generatedRootCount == 1
            && visibleShorelines == 1
            && !hasHorizontalBeach
            && !hasDistantSand
            && !hasSplitBackdrop
            && !hasFixedFoam
            && !hasSunbeamLines
            && complementaryGeometry;

        Debug.Log(
            $"OceanEnvironmentValidator: generatedRoots={generatedRootCount}, visibleShorelines={visibleShorelines}, " +
            $"hasHorizontalBeach={hasHorizontalBeach}, hasDistantSand={hasDistantSand}, " +
            $"hasSplitBackdrop={hasSplitBackdrop}, hasFixedFoam={hasFixedFoam}, " +
            $"hasSunbeamLines={hasSunbeamLines}, complementaryGeometry={complementaryGeometry}"
        );

        if (!valid)
        {
            Debug.LogError("OceanEnvironmentValidator: beach must be one complementary Visible Shoreline with no horizontal sandbar or fixed foam.");
        }

        return valid;
    }

    private static int CountDirectChildren(Transform root, string objectName)
    {
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).name == objectName)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasChildNameFragment(Transform root, string fragment)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item != null && item.name.Contains(fragment))
            {
                return true;
            }
        }

        return false;
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

    private static bool ValidateBeachDecorations(OceanEnvironment environment)
    {
        Transform root = environment.transform.Find("_GeneratedOceanEnvironment");
        Transform beachRoot = root != null ? root.Find("Beach Props") : null;
        if (beachRoot == null)
        {
            Debug.LogError("OceanEnvironmentValidator: generated beach props root was not created.");
            return false;
        }

        if (!environment.TryGetGeneratedBounds("Visible Shoreline", out Bounds beachBounds))
        {
            Debug.LogError("OceanEnvironmentValidator: visible shoreline bounds were not created.");
            return false;
        }

        int palmCount = CountNamedChildren(beachRoot, "Beach Palm");
        int parasolCount = CountNamedChildren(beachRoot, "Beach Parasol");
        int sunbedCount = CountNamedChildren(beachRoot, "Beach Sunbed");
        int deskCount = CountNamedChildren(beachRoot, "Beach Desk");
        int showcasePalms = CountNamedChildrenInShowcase(environment, beachRoot, "Beach Palm");
        int showcaseParasols = CountNamedChildrenInShowcase(environment, beachRoot, "Beach Parasol");
        int showcaseSunbeds = CountNamedChildrenInShowcase(environment, beachRoot, "Beach Sunbed");
        int showcaseDesks = CountNamedChildrenInShowcase(environment, beachRoot, "Beach Desk");
        bool hasFallbackPrimitive = FindBeachFallbackPrimitive(beachRoot) != null;
        bool propsAreDry = AllBeachPropsAreDry(environment, beachRoot);
        bool parasolsValid = ValidateAllSceneParasols(environment, beachRoot);
        bool underwaterDecorationsValid = ValidateUnderwaterDecorations(environment, root);
        bool valid = environment.HasAllBeachPrefabs()
            && palmCount >= 4
            && parasolCount >= 4
            && sunbedCount >= 8
            && deskCount >= 2
            && showcasePalms >= 4
            && showcaseParasols >= 4
            && showcaseSunbeds >= 8
            && showcaseDesks >= 2
            && beachBounds.size.x >= environment.OceanSize.x * 0.18f
            && beachBounds.size.z >= environment.OceanSize.y * 0.9f
            && !hasFallbackPrimitive
            && propsAreDry
            && parasolsValid
            && underwaterDecorationsValid;

        Debug.Log(
            $"OceanEnvironmentValidator: beachProps palms={palmCount}, parasols={parasolCount}, sunbeds={sunbedCount}, desks={deskCount}, " +
            $"showcase=({showcasePalms} palms, {showcaseParasols} parasols, {showcaseSunbeds} sunbeds, {showcaseDesks} desks), " +
            $"beachSpan=({beachBounds.size.x:0.00}, {beachBounds.size.z:0.00}), " +
            $"allPrefabs={environment.HasAllBeachPrefabs()}, hasFallbackPrimitive={hasFallbackPrimitive}, " +
            $"propsAreDry={propsAreDry}, parasolsValid={parasolsValid}, underwaterDecorationsValid={underwaterDecorationsValid}, valid={valid}"
        );

        if (!valid)
        {
            Debug.LogError("OceanEnvironmentValidator: Beach Showcase density, prefab grounding, dry placement, or underwater decoration separation is invalid.");
        }

        return valid;
    }

    private static int CountNamedChildrenInShowcase(OceanEnvironment environment, Transform root, string prefix)
    {
        int count = 0;
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item != null
                && item.name.StartsWith(prefix, System.StringComparison.Ordinal)
                && environment.IsInsideBeachShowcaseWorldPoint(item.position))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountNamedChildren(Transform root, string prefix)
    {
        int count = 0;
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item != null && item.name.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static Transform FindNamedChild(Transform root, string objectName)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item != null && item.name == objectName)
            {
                return item;
            }
        }

        return null;
    }

    private static bool HasNamedLineRenderer(Transform root, string nameFragment)
    {
        foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
        {
            if (line != null && line.name.Contains(nameFragment))
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindBeachFallbackPrimitive(Transform beachRoot)
    {
        string[] fallbackNames =
        {
            "Palm Trunk",
            "Palm Frond",
            "Parasol Pole",
            "Parasol Canopy",
            "Sunbed Base",
            "Sunbed Back",
            "Lifebuoy Ring",
            "Lifebuoy Stripe"
        };

        foreach (Transform item in beachRoot.GetComponentsInChildren<Transform>(true))
        {
            if (item == null)
            {
                continue;
            }

            for (int i = 0; i < fallbackNames.Length; i++)
            {
                if (item.name.StartsWith(fallbackNames[i], System.StringComparison.Ordinal))
                {
                    return item;
                }
            }
        }

        return null;
    }

    private static bool AllBeachPropsAreDry(OceanEnvironment environment, Transform beachRoot)
    {
        string[] majorPropPrefixes = { "Beach Palm", "Beach Parasol", "Beach Sunbed", "Beach Desk" };
        bool valid = true;
        foreach (Transform item in beachRoot.GetComponentsInChildren<Transform>(true))
        {
            if (item == null || !StartsWithAny(item.name, majorPropPrefixes))
            {
                continue;
            }

            Bounds bounds = RendererBounds(item);
            Vector3 groundContact = new Vector3(item.position.x, bounds.min.y, item.position.z);
            environment.EvaluateShorelineWorldPoint(groundContact, out Vector3 local, out _, out float sandSurfaceY, out bool dry);
            float sandWorldY = environment.transform.TransformPoint(new Vector3(local.x, sandSurfaceY, local.z)).y;
            bool grounded = Mathf.Abs(bounds.min.y - sandWorldY) <= 0.06f;
            if (!dry || !grounded)
            {
                Debug.LogError(
                    $"OceanEnvironmentValidator: beach prop placement invalid. prop={item.name}, root={item.position}, " +
                    $"boundsMinY={bounds.min.y:0.000}, sandY={sandWorldY:0.000}, dry={dry}, grounded={grounded}"
                );
                valid = false;
            }
        }

        return valid;
    }

    private static bool ValidateAllSceneParasols(OceanEnvironment environment, Transform beachRoot)
    {
        HashSet<int> visited = new HashSet<int>();
        int count = 0;
        int invalid = 0;
        foreach (Transform item in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (item == null || !item.gameObject.scene.IsValid() || item.name.IndexOf("Parasol", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Transform root = item;
            while (root.parent != null && root.parent.name.IndexOf("Parasol", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                root = root.parent;
            }

            if (!visited.Add(root.GetInstanceID()))
            {
                continue;
            }

            count++;
            Bounds bounds = RendererBounds(root);
            Vector3 contact = new Vector3(root.position.x, bounds.min.y, root.position.z);
            environment.EvaluateShorelineWorldPoint(contact, out Vector3 local, out float shorelineX, out float sandY, out bool dry);
            bool canonical = root.IsChildOf(beachRoot);
            bool valid = canonical && dry && local.x >= shorelineX + 5f && contact.y > environment.ShorelineSurfaceY;
            Debug.Log(
                $"OceanEnvironmentValidator: parasol={root.name}, root={root.position}, contact={contact}, local={local}, " +
                $"shorelineX={shorelineX:0.00}, sandY={sandY:0.00}, waterY={environment.WaterSurfaceY:0.00}, dry={dry}, canonical={canonical}, valid={valid}"
            );
            if (!valid)
            {
                invalid++;
            }
        }

        Debug.Log($"OceanEnvironmentValidator: sceneParasols={count}, invalidOrWetParasols={invalid}");
        return count >= 4 && invalid == 0;
    }

    private static bool ValidateUnderwaterDecorations(OceanEnvironment environment, Transform generatedRoot)
    {
        string[] underwaterNames =
        {
            "Seabed Rock",
            "Reef Coral",
            "Coral Branch",
            "Outer Simple Coral",
            "Bubble Column",
            "Thin Reef Caustic",
            "Clear Sunbeam",
            "Pandazole Outer",
            "Pandazole Rocky Field"
        };
        int invalid = 0;
        Transform beachRoot = generatedRoot.Find("Beach Props");
        foreach (Transform item in generatedRoot.GetComponentsInChildren<Transform>(true))
        {
            if (item == null || (beachRoot != null && item.IsChildOf(beachRoot)) || !ContainsAny(item.name, underwaterNames))
            {
                continue;
            }

            Bounds bounds = RendererBounds(item);
            Vector3 sample = bounds.size.sqrMagnitude > 0.0001f ? bounds.center : item.position;
            if (!environment.IsUnderwaterWorldPoint(sample, 2f))
            {
                Debug.LogError($"OceanEnvironmentValidator: underwater decoration is on dry sand. object={item.name}, sample={sample}");
                invalid++;
            }
        }

        Debug.Log($"OceanEnvironmentValidator: dryUnderwaterDecorations={invalid}");
        return invalid == 0;
    }

    private static bool ContainsAny(string value, string[] fragments)
    {
        for (int i = 0; i < fragments.Length; i++)
        {
            if (value.IndexOf(fragments[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithAny(string value, string[] prefixes)
    {
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (value.StartsWith(prefixes[i], System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Bounds RendererBounds(Transform root)
    {
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
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
