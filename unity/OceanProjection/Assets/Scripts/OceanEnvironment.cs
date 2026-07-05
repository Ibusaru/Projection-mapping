using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public partial class OceanEnvironment : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 oceanSize = new Vector2(140f, 96f);
    [SerializeField] private float seabedY = -8f;
    [SerializeField] private float waterSurfaceY = 4.5f;
    [SerializeField, Range(16, 160)] private int seabedResolution = 128;
    [SerializeField] private int decorationSeed = 4217;

    [Header("Seabed Terrain")]
    [SerializeField, Range(0f, 6f)] private float seabedRelief = 3.6f;
    [SerializeField, Range(0, 8)] private int basinCount = 5;
    [SerializeField, Range(0, 10)] private int reefMoundCount = 7;
    [SerializeField, Range(0f, 1f)] private float reefDecorationBias = 0.74f;

    [Header("Water")]
    [SerializeField] private Color waterColor = new Color(0.025f, 0.34f, 0.46f, 0.58f);
    [SerializeField] private Color deepFogColor = new Color(0.006f, 0.075f, 0.12f, 1f);
    [SerializeField] private float fogDensity = 0.058f;
    [SerializeField, Range(16, 128)] private int waterResolution = 96;
    [SerializeField] private float waveAmplitude = 0.32f;
    [SerializeField] private float waveSpeed = 0.68f;
    [SerializeField] private float waveLength = 0.32f;
    [SerializeField] private bool tintCameras = true;

    [Header("Suimono Water System")]
    [SerializeField] private bool useSuimonoWhenAvailable;
    [SerializeField] private bool keepFallbackWaterWithSuimono = true;
    [SerializeField] private bool renderSuimonoSurfaceInUrp;
    [SerializeField] private bool disableSuimonoUnderwaterParticles = true;
    [SerializeField] private bool disableAllEnvironmentParticles = true;
    [SerializeField] private string suimonoModulePrefabHint = "Suimono_Module";
    [SerializeField] private string suimonoSurfacePrefabHint = "Suimono_Surface";
    [SerializeField] private Color shallowWaterColor = new Color(0.12f, 0.56f, 0.66f, 0.48f);
    [SerializeField] private Color foamColor = new Color(0.72f, 0.92f, 0.94f, 0.28f);

    [Header("Decorations")]
    [SerializeField] private int rockCount = 52;
    [SerializeField] private int simpleCoralCount = 72;
    [SerializeField] private int branchCoralCount = 96;
    [SerializeField] private int bubbleColumnCount = 5;
    [SerializeField] private int causticLineCount = 96;

    [Header("Pandazole Nature Pack")]
    [SerializeField] private bool usePandazoleNaturePackWhenAvailable = true;
    [SerializeField, Range(0f, 1f)] private float pandazoleRockShare = 0.72f;
    [SerializeField, Range(0f, 1f)] private float pandazoleCoralShare = 0.78f;

    private const string GeneratedRootName = "_GeneratedOceanEnvironment";
    private Transform generatedRoot;
    private Material seabedMaterial;
    private Material waterMaterial;
    private Material rockMaterial;
    private Material coralMaterial;
    private Material whiteCoralMaterial;
    private Material causticLineMaterial;
    private Material foamMaterial;
    private Mesh seabedMesh;
    private Mesh waterMesh;
    private Vector3[] waterBaseVertices;
    private OceanSeabedTerrain seabedTerrain;
    private LineRenderer[] causticLines;
    private LineRenderer[] foamLines;
    private bool needsRebuild;

    private void OnEnable()
    {
        BuildEnvironment();
        ApplyRenderSettings();
    }

    private void OnValidate()
    {
        seabedResolution = Mathf.Max(16, seabedResolution);
        waterResolution = Mathf.Max(16, waterResolution);
        seabedRelief = Mathf.Max(0f, seabedRelief);
        basinCount = Mathf.Max(0, basinCount);
        reefMoundCount = Mathf.Max(0, reefMoundCount);
        needsRebuild = true;
    }

    private void Update()
    {
        if (needsRebuild)
        {
            needsRebuild = false;
            BuildEnvironment();
            ApplyRenderSettings();
        }

        AnimateWater();
        ApplyDepthAwareFog();

        if (disableAllEnvironmentParticles)
        {
            StopEnvironmentParticles();
        }
    }

    private void BuildEnvironment()
    {
        ClearGenerated();
        CreateMaterials();
        PreparePandazoleAssets();
        Random.InitState(decorationSeed);
        seabedTerrain = OceanSeabedTerrain.Create(oceanSize, seabedY, waterSurfaceY, decorationSeed, seabedRelief, basinCount, reefMoundCount);

        generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);
        MarkGeneratedObject(generatedRoot.gameObject);

        CreateSeabed();
        bool suimonoActive = useSuimonoWhenAvailable && CreateSuimonoWater();
        if (!suimonoActive || keepFallbackWaterWithSuimono)
        {
            CreateWaterSurface();
        }

        CreateSunlight();
        CreateCausticLines();
        CreateSurfaceHighlights();
        CreateDecorations();
        CreateBubbleColumns();
        FinalizeGeneratedRoot();
    }

    private void ApplyRenderSettings()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = deepFogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.skybox = null;
        RenderSettings.ambientSkyColor = new Color(0.08f, 0.28f, 0.34f);
        RenderSettings.ambientEquatorColor = new Color(0.035f, 0.18f, 0.24f);
        RenderSettings.ambientGroundColor = new Color(0.012f, 0.07f, 0.1f);
        RenderSettings.ambientIntensity = 0.46f;

        if (!tintCameras)
        {
            return;
        }

        foreach (Camera camera in Camera.allCameras)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.2f, 0.28f, 1f);
        }

        ApplyDepthAwareFog();
    }

    private void ApplyDepthAwareFog()
    {
        Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        float localCameraY = camera != null
            ? transform.InverseTransformPoint(camera.transform.position).y
            : waterSurfaceY;
        float depth = Mathf.Max(0f, waterSurfaceY - localCameraY);
        float depthBlend = Smooth01(Mathf.InverseLerp(0.4f, 14f, depth));
        float aboveWaterBlend = Smooth01(Mathf.InverseLerp(waterSurfaceY + 0.5f, waterSurfaceY + 8f, localCameraY));
        float densityMultiplier = Mathf.Lerp(1.25f, 4.35f, depthBlend);
        densityMultiplier = Mathf.Lerp(densityMultiplier, 0.8f, aboveWaterBlend);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Max(0f, fogDensity * densityMultiplier);
        RenderSettings.fogColor = Color.Lerp(new Color(0.025f, 0.2f, 0.28f, 1f), deepFogColor, Mathf.Lerp(0.45f, 1f, depthBlend));

        if (!tintCameras)
        {
            return;
        }

        Color cameraColor = Color.Lerp(new Color(0.025f, 0.2f, 0.28f, 1f), deepFogColor, depthBlend * 0.95f);
        foreach (Camera item in Camera.allCameras)
        {
            item.clearFlags = CameraClearFlags.SolidColor;
            item.backgroundColor = cameraColor;
        }
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private bool CreateSuimonoWater()
    {
#if UNITY_EDITOR
        GameObject modulePrefab = FindPrefabByName(suimonoModulePrefabHint, "module");
        if (modulePrefab != null)
        {
            GameObject module = (GameObject)PrefabUtility.InstantiatePrefab(modulePrefab, generatedRoot);
            module.name = "SUIMONO_Module";
            module.transform.localPosition = Vector3.zero;
            module.transform.localRotation = Quaternion.identity;
            module.transform.localScale = Vector3.one;
            ConfigureSuimonoComponent(module);
            StopSuimonoParticles(module);
        }

        GameObject surfacePrefab = FindPrefabByName(suimonoSurfacePrefabHint, "surface");
        if (surfacePrefab != null)
        {
            GameObject surface = (GameObject)PrefabUtility.InstantiatePrefab(surfacePrefab, generatedRoot);
            surface.name = "SUIMONO_Surface";
            surface.transform.localPosition = new Vector3(0f, waterSurfaceY, 0f);
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = Vector3.one;
            ConfigureSuimonoComponent(surface);
            HideBuiltInSuimonoRenderersForUrp(surface);
            StopSuimonoParticles(surface);
            return true;
        }
#endif

        System.Type suimonoSurfaceType = FindType("SuimonoObject");
        if (suimonoSurfaceType == null)
        {
            suimonoSurfaceType = FindType("Suimono.Core.SuimonoObject");
        }

        if (suimonoSurfaceType == null)
        {
            return false;
        }

        GameObject waterObject = GeneratedPrimitiveFactory.Create(PrimitiveType.Plane, "Suimono Reef Water", waterMaterial);
        waterObject.transform.SetParent(generatedRoot, false);
        waterObject.transform.localPosition = new Vector3(0f, waterSurfaceY, 0f);
        waterObject.transform.localScale = new Vector3(oceanSize.x * 0.1f, 1f, oceanSize.y * 0.1f);

        Component suimono = waterObject.AddComponent(suimonoSurfaceType);
        ConfigureSuimonoComponent(suimono.gameObject);
        HideBuiltInSuimonoRenderersForUrp(waterObject);
        StopSuimonoParticles(waterObject);
        return true;
    }

    private void StopSuimonoParticles(GameObject root)
    {
        if (!disableSuimonoUnderwaterParticles)
        {
            return;
        }

        StopParticles(root.GetComponentsInChildren<ParticleSystem>(true));
    }

    private void StopEnvironmentParticles()
    {
        if (generatedRoot != null)
        {
            StopParticles(generatedRoot.GetComponentsInChildren<ParticleSystem>(true));
        }

        ParticleSystem[] particles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particleSystem = particles[i];
            if (particleSystem == null)
            {
                continue;
            }

            string objectName = particleSystem.gameObject.name.ToLowerInvariant();
            string parentName = particleSystem.transform.parent != null
                ? particleSystem.transform.parent.name.ToLowerInvariant()
                : string.Empty;

            if (objectName.Contains("suimono")
                || objectName.Contains("underwater")
                || objectName.Contains("debris")
                || objectName.Contains("bubble")
                || parentName.Contains("suimono"))
            {
                StopParticles(new[] { particleSystem });
            }
        }
    }

    private static void StopParticles(ParticleSystem[] particleSystems)
    {
        foreach (ParticleSystem particles in particleSystems)
        {
            if (particles == null)
            {
                continue;
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                particleRenderer.enabled = false;
            }
        }
    }

    private void HideBuiltInSuimonoRenderersForUrp(GameObject suimonoSurface)
    {
        if (renderSuimonoSurfaceInUrp || !IsUniversalRenderPipelineActive())
        {
            return;
        }

        foreach (Renderer renderer in suimonoSurface.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
        }
    }

    private static bool IsUniversalRenderPipelineActive()
    {
        UnityEngine.Rendering.RenderPipelineAsset pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        return pipeline != null && pipeline.GetType().FullName.Contains("Universal");
    }

#if UNITY_EDITOR
    private static GameObject FindPrefabByName(string preferredName, string requiredToken)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab Suimono");
        GameObject best = null;
        int bestScore = int.MinValue;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            string lowerName = prefab.name.ToLowerInvariant();
            string lowerPath = path.ToLowerInvariant();
            if (!lowerName.Contains("suimono") && !lowerPath.Contains("suimono"))
            {
                continue;
            }

            int score = 0;
            if (!string.IsNullOrEmpty(preferredName) && lowerName.Contains(preferredName.ToLowerInvariant()))
            {
                score += 100;
            }

            if (lowerName.Contains(requiredToken) || lowerPath.Contains(requiredToken))
            {
                score += 30;
            }

            if (lowerName.Contains("surface") || lowerName.Contains("object") || lowerName.Contains("water"))
            {
                score += requiredToken == "surface" ? 15 : 0;
            }

            if (lowerName.Contains("module"))
            {
                score += requiredToken == "module" ? 15 : -10;
            }

            if (score > bestScore)
            {
                best = prefab;
                bestScore = score;
            }
        }

        return bestScore > 0 ? best : null;
    }
#endif

    private void ConfigureSuimonoComponent(GameObject root)
    {
        Component[] components = root.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().Name.ToLowerInvariant();
            if (!typeName.Contains("suimono") && !typeName.Contains("caustic"))
            {
                continue;
            }

            SetMember(component, "enableCaustics", true);
            SetMember(component, "enableCausticsBlending", true);
            SetMember(component, "waveScale", Mathf.Max(0.35f, waveLength));
            SetMember(component, "flowSpeed", waveSpeed * 0.075f);

            SetMember(component, "useBeaufortScale", true);
            SetMember(component, "beaufortScale", 2.4f);
            SetMember(component, "waveHeight", waveAmplitude * 2.1f);
            SetMember(component, "lgWaveHeight", waveAmplitude * 0.18f);
            SetMember(component, "lgWaveScale", 0.04f);
            SetMember(component, "turbulenceFactor", 0.18f);
            SetMember(component, "oceanScale", Mathf.Max(oceanSize.x, oceanSize.y) / 8.5f);

            SetMember(component, "overallBright", 0.72f);
            SetMember(component, "overallTransparency", 0.62f);
            SetMember(component, "depthAmt", 0.26f);
            SetMember(component, "shallowAmt", 0.1f);
            SetMember(component, "edgeAmt", 0.08f);
            SetMember(component, "depthColor", deepFogColor);
            SetMember(component, "shallowColor", shallowWaterColor);
            SetMember(component, "blendColor", new Color(0.08f, 0.5f, 0.58f, 0.3f));
            SetMember(component, "specularColor", new Color(0.56f, 0.82f, 0.9f, 0.28f));
            SetMember(component, "sssColor", new Color(0.05f, 0.34f, 0.38f, 0.24f));

            SetMember(component, "enableFoam", true);
            SetMember(component, "foamColor", foamColor);
            SetMember(component, "foamScale", 0.72f);
            SetMember(component, "foamSpeed", 0.18f);
            SetMember(component, "edgeFoamAmt", 0.42f);
            SetMember(component, "shallowFoamAmt", 0.55f);
            SetMember(component, "heightFoamAmt", 0.32f);
            SetMember(component, "hFoamHeight", 0.68f);
            SetMember(component, "hFoamSpread", 0.24f);

            SetMember(component, "enableCausticFX", true);
            SetMember(component, "causticsColor", new Color(0.54f, 0.86f, 0.9f, 0.78f));
            SetMember(component, "causticsFade", 0.36f);
            SetMember(component, "causticTint", new Color(0.54f, 0.86f, 0.9f, 0.78f));
            SetMember(component, "causticIntensity", 0.82f);
            SetMember(component, "causticScale", 7f);

            SetMember(component, "enableReflections", true);
            SetMember(component, "enableDynamicReflections", true);
            SetMember(component, "useReflections", true);
            SetMember(component, "useDynReflections", true);
            SetMember(component, "reflectResolution", 2);
            SetMember(component, "reflectionDistance", 42f);
            SetMember(component, "reflectBlur", 0.32f);
            SetMember(component, "reflectionColor", new Color(0.56f, 0.78f, 0.86f, 0.2f));

            SetMember(component, "enableUnderwater", true);
            SetMember(component, "enableUnderDebris", false);
            SetMember(component, "underwaterColor", new Color(deepFogColor.r, deepFogColor.g, deepFogColor.b, 0.68f));
            SetMember(component, "underwaterFogDist", 13f);
            SetMember(component, "underwaterFogSpread", 0.34f);
            SetMember(component, "underRefractionAmount", 0.012f);
            SetMember(component, "underRefractionScale", 0.72f);
            SetMember(component, "underBlurAmount", 0.42f);
        }
    }

    private static System.Type FindType(string typeName)
    {
        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        foreach (System.Reflection.Assembly assembly in assemblies)
        {
            System.Type type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            System.Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            foreach (System.Type candidate in types)
            {
                if (candidate != null && candidate.Name == typeName)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void SetMember(Component component, string memberName, object value)
    {
        System.Type type = component.GetType();
        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;

        System.Reflection.FieldInfo field = type.GetField(memberName, flags);
        if (field != null && CanAssign(field.FieldType, value))
        {
            field.SetValue(component, value);
            return;
        }

        System.Reflection.PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite && CanAssign(property.PropertyType, value))
        {
            property.SetValue(component, value, null);
        }
    }

    private static bool CanAssign(System.Type targetType, object value)
    {
        return value != null && targetType.IsAssignableFrom(value.GetType());
    }

    private void CreateSeabed()
    {
        int points = seabedResolution + 1;
        Vector3[] vertices = new Vector3[points * points];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[seabedResolution * seabedResolution * 6];
        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;

        for (int z = 0; z < points; z++)
        {
            for (int x = 0; x < points; x++)
            {
                float tx = x / (float)seabedResolution;
                float tz = z / (float)seabedResolution;
                float px = Mathf.Lerp(-halfX, halfX, tx);
                float pz = Mathf.Lerp(-halfZ, halfZ, tz);
                vertices[z * points + x] = SampleSeabedPosition(px, pz);
                uvs[z * points + x] = new Vector2(tx * 4f, tz * 4f);
            }
        }

        int index = 0;
        for (int z = 0; z < seabedResolution; z++)
        {
            for (int x = 0; x < seabedResolution; x++)
            {
                int i = z * points + x;
                triangles[index++] = i;
                triangles[index++] = i + points;
                triangles[index++] = i + 1;
                triangles[index++] = i + 1;
                triangles[index++] = i + points;
                triangles[index++] = i + points + 1;
            }
        }

        seabedMesh = new Mesh { name = "Generated Seabed Mesh" };
        seabedMesh.vertices = vertices;
        seabedMesh.uv = uvs;
        seabedMesh.triangles = triangles;
        seabedMesh.RecalculateNormals();
        seabedMesh.RecalculateBounds();

        GameObject seabed = new GameObject("Seabed");
        seabed.transform.SetParent(generatedRoot, false);
        seabed.AddComponent<MeshFilter>().sharedMesh = seabedMesh;
        seabed.AddComponent<MeshRenderer>().sharedMaterial = seabedMaterial;
        seabed.AddComponent<MeshCollider>().sharedMesh = seabedMesh;
    }

    private void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != GeneratedRootName)
            {
                continue;
            }

            DestroyGeneratedObject(child.gameObject);
        }

        generatedRoot = null;
        seabedMesh = null;
        waterMesh = null;
        waterBaseVertices = null;
        causticLines = null;
        foamLines = null;
    }

    private static void DestroyGeneratedObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void DestroyCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }
    }
}
