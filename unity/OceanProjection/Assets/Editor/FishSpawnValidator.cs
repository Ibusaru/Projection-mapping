using System.Collections.Generic;
using System.Reflection;
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

        FishSpawner spawner = Object.FindAnyObjectByType<FishSpawner>();
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
        allValid &= ValidateFishActorDefaults();
        allValid &= ValidateModelRootDoesNotOverwriteActorRotation();
        allValid &= ValidateNicknameTagVisibility(spawner, serializedSpawner);
        allValid &= ValidateDefaultNicknameTagsHidden(serializedSpawner);
        allValid &= ValidateDefaultSchoolSpawnDistribution(spawner, serializedSpawner);
        allValid &= ValidateBoundsRecoveryTarget(serializedSpawner);

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
        Renderer[] visualRenderers = FishRendererUtility.GetVisualRenderers(instance, true);
        DrawingFishVisual[] flatDrawingVisuals = instance.GetComponentsInChildren<DrawingFishVisual>(true);
        Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer renderer in visualRenderers)
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
        builder.Append($", visualRendererCount={visualRenderers.Length}");
        builder.Append($", flatDrawingVisualCount={flatDrawingVisuals.Length}");
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
                Vector3 normalizedSize = bounds.size * (Mathf.Max(0.05f, releasedFishTargetLength) / currentLength);
                builder.Append($", mediumNormalizedBoundsSize={normalizedSize}");
            }
        }

        FishActor actor = instance.GetComponent<FishActor>();
        builder.Append($", hasFishActor={actor != null}");
        Debug.Log(builder.ToString());

        Object.DestroyImmediate(instance);
        if (visualRenderers.Length == 0)
        {
            Debug.LogError($"FishSpawnValidator: {propertyName} has no usable 3D fish renderers.");
            return false;
        }

        if (flatDrawingVisuals.Length > 0)
        {
            Debug.LogError($"FishSpawnValidator: {propertyName} contains a flat DrawingFishVisual fallback.");
            return false;
        }

        if (enabledCameraCount > 0 || enabledAudioListenerCount > 0 || enabledLightCount > 0)
        {
            Debug.LogError($"FishSpawnValidator: {propertyName} contains enabled imported view components.");
            return false;
        }

        return hasBounds;
    }

    private static bool ValidateFishActorDefaults()
    {
        GameObject instance = new GameObject("FishActor Default Validation");
        instance.hideFlags = HideFlags.HideAndDontSave;
        FishActor actor = instance.AddComponent<FishActor>();
        SerializedObject serializedActor = new SerializedObject(actor);
        SerializedProperty projectedDrawing = serializedActor.FindProperty("useProjectedDrawingTextureForReleasedFish");
        bool valid = projectedDrawing != null && projectedDrawing.boolValue;
        if (!valid)
        {
            Debug.LogError("FishSpawnValidator: released fish drawing projection must be enabled by default.");
        }

        Object.DestroyImmediate(instance);
        return valid;
    }

    private static bool ValidateModelRootDoesNotOverwriteActorRotation()
    {
        GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.name = "FishSpawnValidator Root Rotation Fish";
        FishActor actor = instance.AddComponent<FishActor>();
        InvokePrivate(actor, "Awake");

        Transform modelRoot = GetPrivateField<Transform>(actor, "modelRoot");
        bool valid = modelRoot != instance.transform;
        Debug.Log(
            $"FishSpawnValidator: modelRootSafety modelRoot={(modelRoot == null ? "null" : modelRoot.name)}, " +
            $"usesActorRoot={modelRoot == instance.transform}"
        );
        if (!valid)
        {
            Debug.LogError("FishSpawnValidator: FishActor modelRoot is the actor root, so swim rotation can be overwritten by visual sway.");
        }

        Object.DestroyImmediate(instance);
        return valid;
    }

    private static bool ValidateNicknameTagVisibility(FishSpawner spawner, SerializedObject serializedSpawner)
    {
        Camera camera = Camera.main;
        GameObject cameraObject = null;
        if (camera == null)
        {
            cameraObject = new GameObject("FishSpawnValidator Main Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 1.2f, -18f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        Transform cameraTransform = camera.transform;
        Vector3 cameraForward = cameraTransform.forward.sqrMagnitude > 0.001f
            ? cameraTransform.forward.normalized
            : Vector3.forward;

        GameObject fishObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        fishObject.name = "FishSpawnValidator Nickname Fish";
        fishObject.transform.position = cameraTransform.position + cameraForward * 88f;
        fishObject.transform.localScale = Vector3.one * 0.5f;

        FishActor actor = fishObject.AddComponent<FishActor>();
        InvokePrivate(actor, "Awake");
        actor.SetSwimBounds(
            serializedSpawner.FindProperty("center").vector3Value,
            serializedSpawner.FindProperty("size").vector3Value
        );
        actor.SetReleasedFish(true);
        actor.Apply(new FishData
        {
            id = "validator-tag",
            nickname = "タグ検証",
            species = "original",
            main_color = "#36D7FF",
            sub_color = "#FFFFFF",
            size = "medium",
            personality = "calm"
        });

        InvokePrivate(actor, "UpdateLabel");

        bool screenLabelVisible = false;
        int uiTextCount = 0;
        int activeUiTextCount = 0;
        foreach (UnityEngine.UI.Text text in fishObject.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            if (text == null)
            {
                continue;
            }

            uiTextCount++;
            Canvas canvas = text.GetComponentInParent<Canvas>();
            bool textVisible = text.gameObject.activeInHierarchy
                && text.enabled
                && canvas != null
                && canvas.gameObject.activeInHierarchy
                && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                && text.text == "タグ検証";
            if (textVisible)
            {
                activeUiTextCount++;
                screenLabelVisible = true;
            }
        }

        bool valid = screenLabelVisible;
        Debug.Log(
            $"FishSpawnValidator: nicknameTagVisibility distance=88, uiTexts={uiTextCount}, " +
            $"activeUiTexts={activeUiTextCount}, screenLabelVisible={screenLabelVisible}"
        );
        if (!valid)
        {
            Debug.LogError("FishSpawnValidator: screen-space nickname tag did not become visible for a released Japanese-name fish in front of the camera.");
        }

        Object.DestroyImmediate(fishObject);
        if (cameraObject != null)
        {
            Object.DestroyImmediate(cameraObject);
        }

        return valid;
    }

    private static bool ValidateFocusedDefaultNicknameTag(SerializedObject serializedSpawner)
    {
        Camera camera = Camera.main;
        GameObject cameraObject = null;
        if (camera == null)
        {
            cameraObject = new GameObject("FishSpawnValidator Focus Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 1.2f, -12f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        Transform cameraTransform = camera.transform;
        Vector3 cameraForward = cameraTransform.forward.sqrMagnitude > 0.001f
            ? cameraTransform.forward.normalized
            : Vector3.forward;

        GameObject fishObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        fishObject.name = "FishSpawnValidator Focused Default Fish";
        fishObject.transform.position = cameraTransform.position + cameraForward * 12f;
        fishObject.transform.localScale = Vector3.one * 0.4f;

        FishActor actor = fishObject.AddComponent<FishActor>();
        InvokePrivate(actor, "Awake");
        actor.SetSwimBounds(
            serializedSpawner.FindProperty("center").vector3Value,
            serializedSpawner.FindProperty("size").vector3Value
        );
        actor.SetReleasedFish(false);
        actor.Apply(new FishData
        {
            id = "validator-focused-default-tag",
            nickname = "FocusTag",
            species = "original",
            main_color = "#36D7FF",
            sub_color = "#FFFFFF",
            size = "medium",
            personality = "calm"
        });
        actor.SetCameraFocused(true);

        InvokePrivate(actor, "UpdateLabel");

        bool textVisible = false;
        foreach (TextMesh textMesh in fishObject.GetComponentsInChildren<TextMesh>(true))
        {
            if (textMesh == null)
            {
                continue;
            }

            Renderer textRenderer = textMesh.GetComponent<Renderer>();
            textVisible |= textMesh.gameObject.activeSelf
                && textRenderer != null
                && textRenderer.enabled
                && textMesh.text == "FocusTag";
        }

        bool lineVisible = false;
        foreach (LineRenderer line in fishObject.GetComponentsInChildren<LineRenderer>(true))
        {
            if (line != null && line.enabled)
            {
                lineVisible = true;
                break;
            }
        }

        bool valid = textVisible && lineVisible;
        Debug.Log(
            $"FishSpawnValidator: focusedDefaultNicknameTag textVisible={textVisible}, lineVisible={lineVisible}"
        );
        if (!valid)
        {
            Debug.LogError("FishSpawnValidator: focused default fish nickname tag did not become visible.");
        }

        Object.DestroyImmediate(fishObject);
        if (cameraObject != null)
        {
            Object.DestroyImmediate(cameraObject);
        }

        return valid;
    }

    private static bool ValidateDefaultNicknameTagsHidden(SerializedObject serializedSpawner)
    {
        bool focusedHidden = ValidateDefaultNicknameTagHidden(serializedSpawner, true, "focused");
        bool nearbyHidden = ValidateDefaultNicknameTagHidden(serializedSpawner, false, "nearby");
        return focusedHidden && nearbyHidden;
    }

    private static bool ValidateDefaultNicknameTagHidden(SerializedObject serializedSpawner, bool focused, string scenario)
    {
        Camera camera = Camera.main;
        GameObject cameraObject = null;
        if (camera == null)
        {
            cameraObject = new GameObject($"FishSpawnValidator {scenario} Hidden Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 1.2f, -12f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        Transform cameraTransform = camera.transform;
        Vector3 cameraForward = cameraTransform.forward.sqrMagnitude > 0.001f
            ? cameraTransform.forward.normalized
            : Vector3.forward;

        GameObject fishObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        fishObject.name = $"FishSpawnValidator {scenario} Default Hidden Fish";
        fishObject.transform.position = cameraTransform.position + cameraForward * 8f;
        fishObject.transform.localScale = Vector3.one * 0.4f;

        FishActor actor = fishObject.AddComponent<FishActor>();
        InvokePrivate(actor, "Awake");
        actor.SetSwimBounds(
            serializedSpawner.FindProperty("center").vector3Value,
            serializedSpawner.FindProperty("size").vector3Value
        );
        actor.SetReleasedFish(false);
        actor.Apply(new FishData
        {
            id = $"validator-{scenario}-default-hidden-tag",
            nickname = "HiddenTag",
            species = "original",
            main_color = "#36D7FF",
            sub_color = "#FFFFFF",
            size = "medium",
            personality = "calm"
        });
        actor.SetCameraFocused(focused);

        InvokePrivate(actor, "UpdateLabel");

        bool textVisible = false;
        foreach (TextMesh textMesh in fishObject.GetComponentsInChildren<TextMesh>(true))
        {
            if (textMesh == null)
            {
                continue;
            }

            Renderer textRenderer = textMesh.GetComponent<Renderer>();
            textVisible |= textMesh.gameObject.activeSelf
                && textRenderer != null
                && textRenderer.enabled;
        }

        foreach (UnityEngine.UI.Text text in fishObject.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            if (text == null)
            {
                continue;
            }

            textVisible |= text.gameObject.activeInHierarchy && text.enabled;
        }

        bool lineVisible = false;
        foreach (LineRenderer line in fishObject.GetComponentsInChildren<LineRenderer>(true))
        {
            if (line != null && line.enabled)
            {
                lineVisible = true;
                break;
            }
        }

        bool valid = !textVisible && !lineVisible;
        Debug.Log(
            $"FishSpawnValidator: defaultNicknameTagHidden scenario={scenario}, " +
            $"textVisible={textVisible}, lineVisible={lineVisible}"
        );
        if (!valid)
        {
            Debug.LogError($"FishSpawnValidator: default fish nickname tag became visible in {scenario} scenario.");
        }

        Object.DestroyImmediate(fishObject);
        if (cameraObject != null)
        {
            Object.DestroyImmediate(cameraObject);
        }

        return valid;
    }

    private static bool ValidateNearbyDefaultNicknameTag(SerializedObject serializedSpawner)
    {
        Camera camera = Camera.main;
        GameObject cameraObject = null;
        if (camera == null)
        {
            cameraObject = new GameObject("FishSpawnValidator Nearby Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 1.2f, -12f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        Transform cameraTransform = camera.transform;
        Vector3 cameraForward = cameraTransform.forward.sqrMagnitude > 0.001f
            ? cameraTransform.forward.normalized
            : Vector3.forward;

        GameObject fishObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        fishObject.name = "FishSpawnValidator Nearby Default Fish";
        fishObject.transform.position = cameraTransform.position + cameraForward * 9f;
        fishObject.transform.localScale = Vector3.one * 0.4f;

        FishActor actor = fishObject.AddComponent<FishActor>();
        InvokePrivate(actor, "Awake");
        actor.SetSwimBounds(
            serializedSpawner.FindProperty("center").vector3Value,
            serializedSpawner.FindProperty("size").vector3Value
        );
        actor.SetReleasedFish(false);
        actor.Apply(new FishData
        {
            id = "validator-nearby-default-tag",
            nickname = "NearTag",
            species = "original",
            main_color = "#36D7FF",
            sub_color = "#FFFFFF",
            size = "medium",
            personality = "calm"
        });

        InvokePrivate(actor, "UpdateLabel");

        TextMesh visibleText = null;
        foreach (TextMesh textMesh in fishObject.GetComponentsInChildren<TextMesh>(true))
        {
            if (textMesh == null)
            {
                continue;
            }

            Renderer textRenderer = textMesh.GetComponent<Renderer>();
            bool active = textMesh.gameObject.activeSelf
                && textRenderer != null
                && textRenderer.enabled
                && textMesh.text == "NearTag";
            if (active)
            {
                visibleText = textMesh;
                break;
            }
        }

        bool viewportSafe = false;
        if (visibleText != null)
        {
            Vector3 viewport = camera.WorldToViewportPoint(visibleText.transform.position);
            viewportSafe = viewport.z > 0f
                && viewport.x >= 0.06f
                && viewport.x <= 0.94f
                && viewport.y >= 0.1f
                && viewport.y <= 0.9f;
        }

        bool lineVisible = false;
        foreach (LineRenderer line in fishObject.GetComponentsInChildren<LineRenderer>(true))
        {
            if (line != null && line.enabled)
            {
                lineVisible = true;
                break;
            }
        }

        bool valid = visibleText != null && lineVisible && viewportSafe;
        Debug.Log(
            $"FishSpawnValidator: nearbyDefaultNicknameTag textVisible={visibleText != null}, " +
            $"lineVisible={lineVisible}, viewportSafe={viewportSafe}"
        );
        if (!valid)
        {
            Debug.LogError("FishSpawnValidator: nearby non-focused default fish nickname tag did not become visible in-screen.");
        }

        Object.DestroyImmediate(fishObject);
        if (cameraObject != null)
        {
            Object.DestroyImmediate(cameraObject);
        }

        return valid;
    }

    private static bool ValidateDefaultSchoolSpawnDistribution(FishSpawner spawner, SerializedObject serializedSpawner)
    {
        HashSet<FishActor> before = new HashSet<FishActor>(Object.FindObjectsByType<FishActor>(FindObjectsInactive.Include));
        InvokePrivate(spawner, "SpawnDefaultFish");

        List<FishActor> spawned = new List<FishActor>();
        foreach (FishActor actor in Object.FindObjectsByType<FishActor>(FindObjectsInactive.Include))
        {
            if (actor != null && !before.Contains(actor) && !actor.IsReleasedFish)
            {
                spawned.Add(actor);
            }
        }

        Vector3 center = serializedSpawner.FindProperty("center").vector3Value;
        Vector3 size = serializedSpawner.FindProperty("size").vector3Value;
        int expectedCount = serializedSpawner.FindProperty("defaultFishCount").intValue;
        bool valid = spawned.Count >= Mathf.Min(expectedCount, serializedSpawner.FindProperty("maxFishCount").intValue);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        int wallPinnedCount = 0;
        foreach (FishActor actor in spawned)
        {
            Vector3 position = actor.transform.position;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minZ = Mathf.Min(minZ, position.z);
            maxZ = Mathf.Max(maxZ, position.z);

            float distanceToXWall = Mathf.Min(
                Mathf.Abs(position.x - (center.x - size.x * 0.5f)),
                Mathf.Abs(position.x - (center.x + size.x * 0.5f))
            );
            float distanceToZWall = Mathf.Min(
                Mathf.Abs(position.z - (center.z - size.z * 0.5f)),
                Mathf.Abs(position.z - (center.z + size.z * 0.5f))
            );
            if (distanceToXWall < 0.9f || distanceToZWall < 0.9f)
            {
                wallPinnedCount++;
            }
        }

        float spanX = spawned.Count > 0 ? maxX - minX : 0f;
        float spanZ = spawned.Count > 0 ? maxZ - minZ : 0f;
        bool broadlyDistributed = spanX >= Mathf.Abs(size.x) * 0.46f && spanZ >= Mathf.Abs(size.z) * 0.46f;
        bool awayFromWalls = spawned.Count > 0 && wallPinnedCount <= Mathf.Max(1, Mathf.RoundToInt(spawned.Count * 0.03f));
        valid &= broadlyDistributed && awayFromWalls;

        Debug.Log(
            $"FishSpawnValidator: defaultSchoolSpawn spawned={spawned.Count}, spanX={spanX:0.00}, spanZ={spanZ:0.00}, " +
            $"wallPinned={wallPinnedCount}, broadlyDistributed={broadlyDistributed}, awayFromWalls={awayFromWalls}"
        );
        if (!valid)
        {
            Debug.LogError("FishSpawnValidator: default fish spawn distribution is still too central or too close to the tank wall.");
        }

        foreach (FishActor actor in spawned)
        {
            if (actor != null)
            {
                Object.DestroyImmediate(actor.gameObject);
            }
        }

        Transform runtimeParent = GetPrivateField<Transform>(spawner, "runtimeFishParent");
        if (runtimeParent != null && runtimeParent.childCount == 0)
        {
            Object.DestroyImmediate(runtimeParent.gameObject);
        }

        return valid;
    }

    private static bool ValidateBoundsRecoveryTarget(SerializedObject serializedSpawner)
    {
        Vector3 center = serializedSpawner.FindProperty("center").vector3Value;
        Vector3 size = serializedSpawner.FindProperty("size").vector3Value;

        GameObject fishObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        fishObject.name = "FishSpawnValidator Boundary Fish";
        fishObject.transform.position = new Vector3(center.x + size.x * 0.5f - 0.08f, center.y, center.z);
        fishObject.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        FishActor actor = fishObject.AddComponent<FishActor>();
        InvokePrivate(actor, "Awake");
        actor.SetReleasedFish(false);
        actor.SetSwimBounds(center, size);
        InvokePrivate(actor, "PickNextTarget");

        Vector3 target = GetPrivateField<Vector3>(actor, "targetPosition");
        bool targetMovesInward = target.x < fishObject.transform.position.x - 0.5f;
        bool targetHasWallMargin = target.x <= center.x + size.x * 0.5f - 2f;
        bool valid = targetMovesInward && targetHasWallMargin;

        Debug.Log(
            $"FishSpawnValidator: boundsRecovery position={fishObject.transform.position}, target={target}, " +
            $"targetMovesInward={targetMovesInward}, targetHasWallMargin={targetHasWallMargin}"
        );
        if (!valid)
        {
            Debug.LogError("FishSpawnValidator: fish near the tank edge did not choose a safe inward recovery target.");
        }

        Object.DestroyImmediate(fishObject);
        return valid;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null)
        {
            method.Invoke(target, null);
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (T)field.GetValue(target) : default;
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
