using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public partial class OceanEnvironment : MonoBehaviour
{
    [Header("Layout")]
    // Keep the generated world focused on the playable water/beach story.
    // The active fish area remains 100 x 68, while this surrounding shell is
    // deliberately compact so the player reaches the beach and fish quickly.
    [SerializeField] private Vector2 oceanSize = new Vector2(420f, 280f);
    [SerializeField] private float seabedY = -8f;
    // The generated wet/dry sand transition ranges from roughly 4.54 to 4.63.
    // Keep the surface just above it without flooding the dry beach.
    [SerializeField] private float waterSurfaceY = 4.65f;
    // Keep the beach/terrain datum independent from the actual water plane so
    // raising the water does not silently lift the shoreline and props too.
    [SerializeField] private float shorelineSurfaceY = 4.5f;
    [SerializeField, Range(16, 160)] private int seabedResolution = 128;
    [SerializeField] private int decorationSeed = 4217;

    [Header("Unused Area Fill")]
    [SerializeField] private Vector2 activeAreaSize = new Vector2(100f, 68f);
    [SerializeField] private float activeDecorationPadding = 18f;
    [SerializeField] private bool createOpenOceanBackdrop = true;
    [SerializeField, Range(1f, 2.4f)] private float openOceanBackdropScale = 2f;
    // Extend only the open-water depth so the aerial camera never sees the
    // finite Z edge of the generated shoreline shell. This does not enlarge
    // the playable land or fish area.
    [SerializeField, Range(1f, 3f)] private float openOceanBackdropDepthScale = 2.1f;
    [SerializeField] private bool createVisibleShoreline = true;
    [SerializeField, Range(8f, 300f)] private float shorelineWidth = 120f;
    // Kept serialized for existing scenes. A water inset or fixed foam lines
    // would create a second shoreline, so the values are held at zero.
    [SerializeField, HideInInspector] private float shorelineWaterInset;
    [SerializeField, Range(8, 96)] private int shorelineResolution = 56;
    [SerializeField] private int outerRockCount = 260;
    [SerializeField] private int outerCoralCount = 320;
    [SerializeField] private int shoreAccentCount = 130;

    [Header("Seabed Terrain")]
    [SerializeField, Range(0f, 6f)] private float seabedRelief = 3.6f;
    [SerializeField, Range(0, 8)] private int basinCount = 5;
    [SerializeField, Range(0, 10)] private int reefMoundCount = 7;
    [SerializeField, Range(0f, 1f)] private float reefDecorationBias = 0.74f;

    [Header("Water")]
    [SerializeField] private Color waterColor = new Color(0.10f, 0.54f, 0.70f, 0.72f);
    [SerializeField] private Color deepFogColor = new Color(0.08f, 0.42f, 0.55f, 1f);
    [SerializeField] private float fogDensity = 0.014f;
    [SerializeField, Range(16, 128)] private int waterResolution = 128;
    [SerializeField] private bool tintCameras = true;

    [Header("Suimono Water System")]
    [SerializeField] private bool useSuimonoWhenAvailable;
    [SerializeField] private bool keepFallbackWaterWithSuimono = true;
    [SerializeField] private bool renderSuimonoSurfaceInUrp;
    [SerializeField] private bool disableSuimonoUnderwaterParticles = true;
    [SerializeField] private bool disableAllEnvironmentParticles = true;
    [SerializeField] private string suimonoModulePrefabHint = "Suimono_Module";
    [SerializeField] private string suimonoSurfacePrefabHint = "Suimono_Surface";
    [SerializeField] private Color shallowWaterColor = new Color(0.25f, 0.78f, 0.86f, 0.5f);
    [SerializeField] private Color foamColor = new Color(0.86f, 0.98f, 1f, 0.36f);

    // The main stable water surface is double-sided. Keep this legacy second
    // surface opt-in only so aerial views cannot show two overlapping planes.
    [SerializeField] private bool createUnderwaterSurfaceCue = false;

    [Header("Decorations")]
    [SerializeField] private int rockCount = 68;
    [SerializeField] private int simpleCoralCount = 72;
    [SerializeField] private int branchCoralCount = 96;
    [SerializeField] private int bubbleColumnCount = 5;
    [SerializeField] private int causticLineCount = 18;

    [Header("Pandazole Nature Pack")]
    [SerializeField] private bool usePandazoleNaturePackWhenAvailable = true;
    [SerializeField, Range(0f, 1f)] private float pandazoleRockShare = 0.72f;
    [SerializeField, Range(0f, 1f)] private float pandazoleCoralShare = 0.78f;

    private const string GeneratedRootName = "_GeneratedOceanEnvironment";
    private Transform generatedRoot;
    private Material seabedMaterial;
    private Material waterMaterial;
    private Material underwaterSurfaceMaterial;
    private Material rockMaterial;
    private Material coralMaterial;
    private Material whiteCoralMaterial;
    private Material shorelineMaterial;
    private Material shorePlantMaterial;
    private Material causticLineMaterial;
    private Mesh seabedMesh;
    private Mesh waterMesh;
    private OceanSeabedTerrain seabedTerrain;
    private LineRenderer[] causticLines;
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
        activeAreaSize = new Vector2(Mathf.Max(1f, activeAreaSize.x), Mathf.Max(1f, activeAreaSize.y));
        activeDecorationPadding = Mathf.Max(0f, activeDecorationPadding);
        openOceanBackdropScale = Mathf.Clamp(openOceanBackdropScale, 1f, 2.4f);
        openOceanBackdropDepthScale = Mathf.Max(1f, openOceanBackdropDepthScale);
        shorelineWidth = Mathf.Max(1f, shorelineWidth);
        shorelineWaterInset = 0f;
        shorelineResolution = Mathf.Max(4, shorelineResolution);
        outerRockCount = Mathf.Max(0, outerRockCount);
        outerCoralCount = Mathf.Max(0, outerCoralCount);
        shoreAccentCount = Mathf.Max(0, shoreAccentCount);
        beachPalmCount = Mathf.Max(0, beachPalmCount);
        beachParasolCount = Mathf.Max(0, beachParasolCount);
        beachSmallPropCount = Mathf.Max(0, beachSmallPropCount);
        beachModelScale = Mathf.Max(0.25f, beachModelScale);
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
        PrepareBeachAssets();
        Random.InitState(decorationSeed);
        seabedTerrain = OceanSeabedTerrain.Create(oceanSize, activeAreaSize, shorelineWidth, seabedY, waterSurfaceY, shorelineSurfaceY, decorationSeed, seabedRelief, basinCount, reefMoundCount);

        generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);
        MarkGeneratedObject(generatedRoot.gameObject);

        CreateSeabed();
        bool suimonoActive = useSuimonoWhenAvailable && CreateSuimonoWater();
        if (!suimonoActive || keepFallbackWaterWithSuimono)
        {
            CreateWaterSurface();
        }

        CreateUnderwaterSurfaceCue();

        CreateVisibleShoreline();
        CreateSunlight();
        CreateCausticLines();
        CreateDecorations();
        CreateBubbleColumns();
        FinalizeGeneratedRoot();
    }

    [ContextMenu("Rebuild Ocean Environment")]
    public void RebuildEnvironment()
    {
        needsRebuild = false;
        BuildEnvironment();
        ApplyRenderSettings();
    }

    private void ApplyRenderSettings()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = deepFogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.88f, 0.96f);
        RenderSettings.ambientEquatorColor = new Color(0.24f, 0.66f, 0.76f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.38f, 0.34f);
        RenderSettings.ambientIntensity = 1.22f;

        if (!tintCameras)
        {
            return;
        }

        foreach (Camera camera in Camera.allCameras)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.78f, 0.92f, 1f);
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
        float depthBlend = Smooth01(Mathf.InverseLerp(1.5f, 24f, depth));
        float aboveWaterBlend = Smooth01(Mathf.InverseLerp(waterSurfaceY + 0.5f, waterSurfaceY + 8f, localCameraY));
        float densityMultiplier = Mathf.Lerp(0.42f, 1.42f, depthBlend);
        densityMultiplier = Mathf.Lerp(densityMultiplier, 0.18f, aboveWaterBlend);
        Color shallowColor = new Color(0.24f, 0.72f, 0.82f, 1f);
        Color skyColor = new Color(0.45f, 0.82f, 0.94f, 1f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Max(0f, fogDensity * densityMultiplier);
        RenderSettings.fogColor = Color.Lerp(shallowColor, deepFogColor, Mathf.Lerp(0.08f, 0.72f, depthBlend));
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, skyColor, aboveWaterBlend * 0.86f);

        if (!tintCameras)
        {
            return;
        }

        Color cameraColor = Color.Lerp(shallowColor, deepFogColor, depthBlend * 0.52f);
        cameraColor = Color.Lerp(cameraColor, skyColor, aboveWaterBlend * 0.9f);
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
            SetMember(component, "waveScale", 0.35f);
            SetMember(component, "flowSpeed", 0.05f);

            SetMember(component, "useBeaufortScale", true);
            SetMember(component, "beaufortScale", 2.4f);
            SetMember(component, "waveHeight", 0.67f);
            SetMember(component, "lgWaveHeight", 0.058f);
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
        float extensionScale = createOpenOceanBackdrop ? Mathf.Clamp(openOceanBackdropScale, 1f, 2.4f) : 1f;
        float depthScale = createOpenOceanBackdrop ? Mathf.Clamp(openOceanBackdropDepthScale, 1f, 3f) : 1f;
        int xResolution = Mathf.Max(seabedResolution, Mathf.CeilToInt(seabedResolution * extensionScale));
        int zResolution = Mathf.Max(seabedResolution, Mathf.CeilToInt(seabedResolution * extensionScale * depthScale));
        int xPoints = xResolution + 1;
        int zPoints = zResolution + 1;
        Vector3[] vertices = new Vector3[xPoints * zPoints];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[xResolution * zResolution * 6];
        float mainHalfX = oceanSize.x * 0.5f;
        float mainHalfZ = oceanSize.y * 0.5f;
        float halfX = mainHalfX * extensionScale;
        float halfZ = mainHalfZ * extensionScale * depthScale;

        for (int z = 0; z < zPoints; z++)
        {
            for (int x = 0; x < xPoints; x++)
            {
                float tx = x / (float)xResolution;
                float tz = z / (float)zResolution;
                float px = Mathf.Lerp(-halfX, halfX, tx);
                float pz = Mathf.Lerp(-halfZ, halfZ, tz);
                vertices[z * xPoints + x] = SampleExtendedSeabedPosition(px, pz, mainHalfX, mainHalfZ);
                uvs[z * xPoints + x] = new Vector2(tx * 4f * extensionScale, tz * 4f * extensionScale * depthScale);
            }
        }

        int index = 0;
        for (int z = 0; z < zResolution; z++)
        {
            for (int x = 0; x < xResolution; x++)
            {
                int i = z * xPoints + x;
                triangles[index++] = i;
                triangles[index++] = i + xPoints;
                triangles[index++] = i + 1;
                triangles[index++] = i + 1;
                triangles[index++] = i + xPoints;
                triangles[index++] = i + xPoints + 1;
            }
        }

        seabedMesh = new Mesh { name = "Generated Seabed Mesh" };
        seabedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

    private Vector3 SampleExtendedSeabedPosition(float x, float z, float mainHalfX, float mainHalfZ)
    {
        float clampedX = Mathf.Clamp(x, -mainHalfX, mainHalfX);
        float clampedZ = Mathf.Clamp(z, -mainHalfZ, mainHalfZ);
        Vector3 edge = SampleSeabedPosition(clampedX, clampedZ);
        float outsideX = Mathf.Max(0f, Mathf.Abs(x) - mainHalfX);
        float outsideZ = Mathf.Max(0f, Mathf.Abs(z) - mainHalfZ);
        float outsideDistance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
        if (outsideDistance <= 0.001f)
        {
            return edge;
        }

        float blend = Smooth01(Mathf.InverseLerp(0f, 42f, outsideDistance));
        float deepNoise = (Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 8.3f) - 0.5f) * 0.8f;
        float deepY = seabedY - 6f + deepNoise;
        return new Vector3(x, Mathf.Lerp(edge.y, deepY, blend), z);
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
        causticLines = null;
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
