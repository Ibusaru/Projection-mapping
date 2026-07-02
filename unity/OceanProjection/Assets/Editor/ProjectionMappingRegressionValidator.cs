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
        valid &= ValidateFallbackMeshUvIsFlipped();
        valid &= ValidateNicknameLabelStaysCameraParallel();

        EditorApplication.Exit(valid ? 0 : 2);
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

    private static bool ValidateFallbackMeshUvIsFlipped()
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

            valid = uvAtHeadSide > 0.85f;
        }

        if (!valid)
        {
            Debug.LogError("ProjectionMappingRegressionValidator: fallback drawing mesh UVs are not horizontally flipped.");
        }

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(owner);
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

        GameObject fishObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fishObject.hideFlags = HideFlags.HideAndDontSave;
        fishObject.name = "Projection Mapping Label Fish";
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
