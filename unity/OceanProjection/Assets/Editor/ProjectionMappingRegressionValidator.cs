using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class ProjectionMappingRegressionValidator
{
    public static void Run()
    {
        bool valid = true;
        valid &= ValidateReleasedDrawingFlipDefault();
        valid &= ValidateProjectionFrameIsFlipped();
        valid &= ValidateTransparentFishMaskKeepsProjectionScale();
        valid &= ValidateFallbackMeshUvPreservesWebOrientation();
        valid &= ValidateReleasedDrawingUsesFlatVisual();
        valid &= ValidateNicknameLabelStaysCameraParallel();

        EditorApplication.Exit(valid ? 0 : 2);
    }

    public static void RunDrawingUv()
    {
        bool valid = true;
        valid &= ValidateDrawingUvShaderLoads();
        valid &= ValidateGeneratedDrawingUvsAreFlipped();

        EditorApplication.Exit(valid ? 0 : 2);
    }

    public static void RunReleasedDrawingFlatVisual()
    {
        bool valid = true;
        valid &= ValidateFallbackMeshUvPreservesWebOrientation();
        valid &= ValidateReleasedDrawingUsesFlatVisual();

        EditorApplication.Exit(valid ? 0 : 2);
    }

    private static bool ValidateDrawingUvShaderLoads()
    {
        Shader shader = Shader.Find("OceanProjection/Drawing Fish UV");
        if (shader == null)
        {
            shader = Resources.Load<Shader>("Shaders/DrawingFishUv");
        }

        bool valid = shader != null && shader.isSupported && shader.name != "Hidden/InternalErrorShader";
        if (!valid)
        {
            Debug.LogError("ProjectionMappingRegressionValidator: Drawing Fish UV shader could not be loaded.");
        }

        return valid;
    }

    private static bool ValidateGeneratedDrawingUvsAreFlipped()
    {
        GameObject owner = new GameObject("Projection Mapping Generated UV Owner");
        owner.hideFlags = HideFlags.HideAndDontSave;
        GameObject visualObject = new GameObject("Projection Mapping Generated UV Visual");
        visualObject.hideFlags = HideFlags.HideAndDontSave;
        visualObject.transform.SetParent(owner.transform, false);

        Mesh mesh = new Mesh
        {
            name = "ProjectionMappingGeneratedUvMesh"
        };
        mesh.vertices = new[]
        {
            new Vector3(-1f, -0.5f, 0f),
            new Vector3(1f, -0.5f, 0f),
            new Vector3(-1f, 0.5f, 0f),
            new Vector3(1f, 0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        MeshRenderer renderer = visualObject.AddComponent<MeshRenderer>();

        bool applied = DrawingTextureMapper.ApplyGeneratedUvs(new Renderer[] { renderer }, owner.transform, true);
        Mesh mappedMesh = meshFilter.sharedMesh;
        Vector2[] uvs = mappedMesh != null ? mappedMesh.uv : null;
        bool valid = applied
            && mappedMesh != null
            && mappedMesh != mesh
            && uvs != null
            && uvs.Length == 4
            && uvs[0].x > 0.95f
            && uvs[1].x < 0.05f
            && uvs[2].y > 0.95f;

        if (!valid)
        {
            string uvSummary = uvs == null ? "missing" : string.Join(", ", uvs);
            Debug.LogError($"ProjectionMappingRegressionValidator: generated drawing UVs are wrong. applied={applied}, uvs={uvSummary}");
        }

        Object.DestroyImmediate(owner);
        Object.DestroyImmediate(mesh);
        return valid;
    }

    private static bool ValidateReleasedDrawingFlipDefault()
    {
        GameObject instance = new GameObject("Projection Mapping Flip Default Validation");
        instance.hideFlags = HideFlags.HideAndDontSave;
        FishActor actor = instance.AddComponent<FishActor>();
        SerializedObject serializedActor = new SerializedObject(actor);
        SerializedProperty flipProperty = serializedActor.FindProperty("flipReleasedDrawingHorizontally");
        bool valid = flipProperty != null && flipProperty.boolValue;

        if (!valid)
        {
            Debug.LogError("ProjectionMappingRegressionValidator: released drawing textures must be horizontally flipped by default.");
        }

        Object.DestroyImmediate(instance);
        return valid;
    }

    private static bool ValidateProjectionFrameIsFlipped()
    {
        MethodInfo method = typeof(FishActor).GetMethod("CreateProjectionFrame", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogError("ProjectionMappingRegressionValidator: CreateProjectionFrame was not found.");
            return false;
        }

        object[] args =
        {
            new Bounds(Vector3.zero, new Vector3(2f, 1f, 4f)),
            true,
            null,
            null,
            null
        };
        method.Invoke(null, args);

        Vector3 origin = (Vector3)args[2];
        Vector3 uVector = (Vector3)args[3];
        bool valid = Mathf.Abs(origin.z - 2f) <= 0.0001f
            && Vector3.Distance(uVector, Vector3.back * 4f) <= 0.0001f;

        if (!valid)
        {
            Debug.LogError(
                $"ProjectionMappingRegressionValidator: flipped projection frame is wrong. origin={origin}, u={uVector}"
            );
        }

        return valid;
    }

    private static bool ValidateTransparentFishMaskKeepsProjectionScale()
    {
        Texture2D source = new Texture2D(16, 8, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[16 * 8];
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 whiteMask = new Color32(255, 255, 255, 245);
        Color32 redMark = new Color32(255, 64, 32, 255);

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        for (int y = 2; y <= 5; y++)
        {
            for (int x = 2; x <= 13; x++)
            {
                pixels[y * 16 + x] = whiteMask;
            }
        }

        pixels[4 * 16 + 12] = redMark;
        source.SetPixels32(pixels);
        source.Apply(false, false);

        Texture2D projection = DrawingTextureMapper.CreateProjectionTexture(source, 0.05f, Vector2.zero);
        Color32[] projectedPixels = projection != null ? projection.GetPixels32() : null;
        int redCount = 0;
        if (projectedPixels != null)
        {
            for (int i = 0; i < projectedPixels.Length; i++)
            {
                Color32 color = projectedPixels[i];
                if (color.a > 0 && color.r > 220 && color.g < 120 && color.b < 90)
                {
                    redCount++;
                }
            }
        }

        bool valid = projection != null && redCount > 0 && redCount < projectedPixels.Length / 4;
        if (!valid)
        {
            Debug.LogError(
                $"ProjectionMappingRegressionValidator: transparent fish-mask projection scale is wrong. redPixels={redCount}"
            );
        }

        Object.DestroyImmediate(source);
        Object.DestroyImmediate(projection);
        return valid;
    }

    private static bool ValidateFallbackMeshUvPreservesWebOrientation()
    {
        GameObject owner = new GameObject("Projection Mapping Fallback Owner");
        owner.hideFlags = HideFlags.HideAndDontSave;
        GameObject visualObject = new GameObject("Projection Mapping Fallback Visual");
        visualObject.hideFlags = HideFlags.HideAndDontSave;
        visualObject.transform.SetParent(owner.transform, false);

        DrawingFishVisual visual = visualObject.AddComponent<DrawingFishVisual>();
        Texture2D texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
        visual.Apply(texture, new Bounds(owner.transform.position, new Vector3(2f, 1f, 4f)));

        MeshFilter meshFilter = visualObject.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        bool valid = false;
        if (mesh != null)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            float maxZ = float.NegativeInfinity;
            float uvAtHeadSide = 0f;

            for (int i = 0; i < vertices.Length && i < uvs.Length; i++)
            {
                if (vertices[i].z > maxZ)
                {
                    maxZ = vertices[i].z;
                    uvAtHeadSide = uvs[i].x;
                }
            }

            valid = uvAtHeadSide < 0.15f;
        }

        if (!valid)
        {
            Debug.LogError("ProjectionMappingRegressionValidator: fallback drawing mesh UVs do not preserve the web drawing orientation.");
        }

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(owner);
        return valid;
    }

    private static bool ValidateReleasedDrawingUsesFlatVisual()
    {
        GameObject fishObject = GeneratedPrimitiveFactory.Create(PrimitiveType.Cube, "Projection Mapping Released Fish Model");
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        FishActor actor = fishObject.AddComponent<FishActor>();
        Renderer originalRenderer = fishObject.GetComponent<Renderer>();
        Texture2D texture = new Texture2D(8, 4, TextureFormat.RGBA32, false);

        MethodInfo method = typeof(FishActor).GetMethod("ApplyReleasedDrawingTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogError("ProjectionMappingRegressionValidator: ApplyReleasedDrawingTexture was not found.");
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(fishObject);
            return false;
        }

        method.Invoke(actor, new object[] { texture });

        DrawingFishVisual visual = fishObject.GetComponentInChildren<DrawingFishVisual>(true);
        Renderer drawingRenderer = visual != null ? visual.Renderer : null;
        Material drawingMaterial = drawingRenderer != null ? drawingRenderer.sharedMaterial : null;
        MeshFilter meshFilter = visual != null ? visual.GetComponent<MeshFilter>() : null;
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;

        bool valid = originalRenderer != null
            && !originalRenderer.enabled
            && visual != null
            && visual.gameObject.activeSelf
            && drawingRenderer != null
            && drawingRenderer.enabled
            && drawingMaterial != null
            && drawingMaterial.mainTexture == texture
            && mesh != null
            && mesh.vertexCount > 0;

        if (!valid)
        {
            Debug.LogError(
                $"ProjectionMappingRegressionValidator: released drawing did not switch to the flat visual. originalEnabled={originalRenderer?.enabled}, visual={visual != null}, drawingEnabled={drawingRenderer?.enabled}, hasTexture={drawingMaterial != null && drawingMaterial.mainTexture == texture}, vertices={mesh?.vertexCount ?? 0}"
            );
        }

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(fishObject);
        return valid;
    }

    private static bool ValidateNicknameLabelStaysCameraParallel()
    {
        GameObject cameraObject = new GameObject("Projection Mapping Label Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0f, 1.4f, -7f);
        cameraObject.transform.rotation = Quaternion.Euler(11f, -18f, 0f);

        GameObject fishObject = GeneratedPrimitiveFactory.Create(PrimitiveType.Cube, "Projection Mapping Label Fish");
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        fishObject.transform.position = cameraObject.transform.position
            + cameraObject.transform.forward * 3f
            + cameraObject.transform.right * 0.8f;
        fishObject.transform.localScale = Vector3.one * 0.45f;

        FishActor actor = fishObject.AddComponent<FishActor>();
        InvokePrivate(actor, "Awake");
        SetPrivateField(actor, "mainCamera", camera);
        actor.SetSwimBounds(fishObject.transform.position, Vector3.one * 8f);
        actor.SetReleasedFish(true);
        actor.SetCameraFocused(true);
        actor.Apply(new FishData
        {
            id = "projection-validator-label",
            nickname = "LabelTest",
            species = "original",
            main_color = "#36D7FF",
            sub_color = "#FFFFFF",
            size = "medium",
            personality = "calm"
        });
        SetPrivateField(actor, "nicknameTagRevealProgress", 1f);
        InvokePrivate(actor, "UpdateLabel");

        Transform labelTransform = FindActiveNicknameLabel(fishObject);
        bool valid = labelTransform != null
            && Quaternion.Angle(labelTransform.rotation, cameraObject.transform.rotation) <= 0.5f;

        if (!valid)
        {
            string rotation = labelTransform != null ? labelTransform.rotation.eulerAngles.ToString() : "missing";
            Debug.LogError(
                $"ProjectionMappingRegressionValidator: nickname label is not camera-parallel. " +
                $"camera={cameraObject.transform.rotation.eulerAngles}, label={rotation}"
            );
        }

        Object.DestroyImmediate(fishObject);
        Object.DestroyImmediate(cameraObject);
        return valid;
    }

    private static Transform FindActiveNicknameLabel(GameObject root)
    {
        TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text text = tmpTexts[i];
            if (text != null && text.gameObject.activeInHierarchy)
            {
                return text.transform;
            }
        }

        TextMesh[] textMeshes = root.GetComponentsInChildren<TextMesh>(true);
        for (int i = 0; i < textMeshes.Length; i++)
        {
            TextMesh text = textMeshes[i];
            if (text != null && text.gameObject.activeInHierarchy)
            {
                return text.transform;
            }
        }

        return null;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(target, null);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
