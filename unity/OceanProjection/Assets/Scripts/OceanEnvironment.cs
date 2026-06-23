using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class OceanEnvironment : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 oceanSize = new Vector2(64f, 44f);
    [SerializeField] private float seabedY = -4.8f;
    [SerializeField] private float waterSurfaceY = 3.2f;
    [SerializeField, Range(8, 80)] private int seabedResolution = 36;
    [SerializeField] private int decorationSeed = 4217;

    [Header("Water")]
    [SerializeField] private Color waterColor = new Color(0.16f, 0.78f, 0.96f, 0.36f);
    [SerializeField] private Color deepFogColor = new Color(0.04f, 0.58f, 0.82f, 1f);
    [SerializeField] private float fogDensity = 0.035f;
    [SerializeField, Range(8, 80)] private int waterResolution = 32;
    [SerializeField] private float waveAmplitude = 0.22f;
    [SerializeField] private float waveSpeed = 0.72f;
    [SerializeField] private float waveLength = 0.42f;
    [SerializeField] private bool tintCameras = true;

    [Header("Suimono Water System")]
    [SerializeField] private bool useSuimonoWhenAvailable = true;
    [SerializeField] private bool keepFallbackWaterWithSuimono = true;
    [SerializeField] private bool renderSuimonoSurfaceInUrp;
    [SerializeField] private bool disableSuimonoUnderwaterParticles = true;
    [SerializeField] private bool disableAllEnvironmentParticles = true;
    [SerializeField] private string suimonoModulePrefabHint = "Suimono_Module";
    [SerializeField] private string suimonoSurfacePrefabHint = "Suimono_Surface";
    [SerializeField] private Color shallowWaterColor = new Color(0.28f, 0.94f, 1f, 0.55f);
    [SerializeField] private Color foamColor = new Color(0.92f, 1f, 1f, 0.48f);

    [Header("Decorations")]
    [SerializeField] private int rockCount = 10;
    [SerializeField] private int simpleCoralCount = 16;
    [SerializeField] private int branchCoralCount = 28;
    [SerializeField] private int bubbleColumnCount = 5;
    [SerializeField] private int causticLineCount = 30;

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
        seabedResolution = Mathf.Max(8, seabedResolution);
        waterResolution = Mathf.Max(8, waterResolution);
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

        if (disableAllEnvironmentParticles)
        {
            StopEnvironmentParticles();
        }
    }

    private void BuildEnvironment()
    {
        ClearGenerated();
        CreateMaterials();
        Random.InitState(decorationSeed);

        generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);

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
    }

    private void ApplyRenderSettings()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = deepFogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.skybox = null;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.86f, 0.98f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.68f, 0.84f);
        RenderSettings.ambientGroundColor = new Color(0.18f, 0.44f, 0.52f);
        RenderSettings.ambientIntensity = 1.25f;

        if (!tintCameras)
        {
            return;
        }

        foreach (Camera camera in Camera.allCameras)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = deepFogColor;
        }
    }

    private void CreateMaterials()
    {
        seabedMaterial = MakeMaterial("Reef White Sand", new Color(0.86f, 0.82f, 0.66f, 1f), 0f);
        waterMaterial = MakeMaterial("Bright Reef Water Surface", waterColor, 0.72f);
        rockMaterial = MakeMaterial("Reef Rock", new Color(0.42f, 0.52f, 0.43f, 1f), 0f);
        coralMaterial = MakeMaterial("Warm Reef Coral", new Color(0.96f, 0.58f, 0.44f, 1f), 0f);
        whiteCoralMaterial = MakeMaterial("Pale Branch Coral", new Color(0.88f, 0.84f, 0.7f, 1f), 0f);
        causticLineMaterial = MakeUnlitMaterial("Thin Reef Caustics", new Color(0.92f, 1f, 1f, 0.23f));
        foamMaterial = MakeUnlitMaterial("Soft Reef Surface Sparkle", foamColor);
    }

    private Material MakeMaterial(string materialName, Color color, float transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        if (transparent > 0f)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", transparent > 0f ? 0.92f : 0.28f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (transparent > 0f && material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }

    private Material MakeUnlitMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_Cull", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        return material;
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

        GameObject waterObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterObject.name = "Suimono Reef Water";
        waterObject.transform.SetParent(generatedRoot, false);
        waterObject.transform.localPosition = new Vector3(0f, waterSurfaceY, 0f);
        waterObject.transform.localScale = new Vector3(oceanSize.x * 0.1f, 1f, oceanSize.y * 0.1f);
        DestroyCollider(waterObject);

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

            SetMember(component, "overallBright", 1.12f);
            SetMember(component, "overallTransparency", 0.78f);
            SetMember(component, "depthAmt", 0.09f);
            SetMember(component, "shallowAmt", 0.18f);
            SetMember(component, "edgeAmt", 0.08f);
            SetMember(component, "depthColor", deepFogColor);
            SetMember(component, "shallowColor", shallowWaterColor);
            SetMember(component, "blendColor", new Color(0.5f, 1f, 0.92f, 0.28f));
            SetMember(component, "specularColor", new Color(0.82f, 0.98f, 1f, 0.42f));
            SetMember(component, "sssColor", new Color(0.12f, 0.72f, 0.62f, 0.26f));

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
            SetMember(component, "causticsColor", new Color(0.86f, 1f, 1f, 1f));
            SetMember(component, "causticsFade", 0.18f);
            SetMember(component, "causticTint", new Color(0.86f, 1f, 1f, 1f));
            SetMember(component, "causticIntensity", 1.6f);
            SetMember(component, "causticScale", 7f);

            SetMember(component, "enableReflections", true);
            SetMember(component, "enableDynamicReflections", true);
            SetMember(component, "useReflections", true);
            SetMember(component, "useDynReflections", true);
            SetMember(component, "reflectResolution", 2);
            SetMember(component, "reflectionDistance", 90f);
            SetMember(component, "reflectBlur", 0.18f);
            SetMember(component, "reflectionColor", new Color(1f, 1f, 1f, 0.32f));

            SetMember(component, "enableUnderwater", true);
            SetMember(component, "enableUnderDebris", false);
            SetMember(component, "underwaterColor", new Color(deepFogColor.r, deepFogColor.g, deepFogColor.b, 0.42f));
            SetMember(component, "underwaterFogDist", 28f);
            SetMember(component, "underwaterFogSpread", 0.18f);
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
                float ripple = Mathf.Sin(px * 0.8f + pz * 0.35f) * 0.18f
                    + Mathf.Cos(pz * 0.9f) * 0.11f;
                vertices[z * points + x] = new Vector3(px, seabedY + ripple, pz);
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
    }

    private void CreateWaterSurface()
    {
        int points = waterResolution + 1;
        Vector3[] vertices = new Vector3[points * points];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[waterResolution * waterResolution * 6];
        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;

        for (int z = 0; z < points; z++)
        {
            for (int x = 0; x < points; x++)
            {
                float tx = x / (float)waterResolution;
                float tz = z / (float)waterResolution;
                vertices[z * points + x] = new Vector3(
                    Mathf.Lerp(-halfX, halfX, tx),
                    waterSurfaceY,
                    Mathf.Lerp(-halfZ, halfZ, tz)
                );
                uvs[z * points + x] = new Vector2(tx * 3f, tz * 3f);
            }
        }

        int index = 0;
        for (int z = 0; z < waterResolution; z++)
        {
            for (int x = 0; x < waterResolution; x++)
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

        waterMesh = new Mesh { name = "Generated Water Surface Mesh" };
        waterMesh.vertices = vertices;
        waterMesh.uv = uvs;
        waterMesh.triangles = triangles;
        waterMesh.RecalculateNormals();
        waterMesh.RecalculateBounds();
        waterBaseVertices = vertices;

        GameObject water = new GameObject("Water Surface");
        water.transform.SetParent(generatedRoot, false);
        water.AddComponent<MeshFilter>().sharedMesh = waterMesh;
        water.AddComponent<MeshRenderer>().sharedMaterial = waterMaterial;
    }

    private void AnimateWater()
    {
        if (waterMesh == null)
        {
            return;
        }

        Vector3[] vertices = waterMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 baseVertex = waterBaseVertices[i];
            float waveA = Mathf.Sin(Time.time * waveSpeed + baseVertex.x * waveLength + baseVertex.z * 0.22f);
            float waveB = Mathf.Cos(Time.time * waveSpeed * 1.37f + baseVertex.z * waveLength * 1.2f);
            float waveC = Mathf.Sin(Time.time * waveSpeed * 0.63f + (baseVertex.x + baseVertex.z) * waveLength * 0.58f);
            vertices[i].y = waterSurfaceY + (waveA * 0.52f + waveB * 0.31f + waveC * 0.17f) * waveAmplitude;
        }

        waterMesh.vertices = vertices;
        waterMesh.RecalculateNormals();

        if (causticLines == null)
        {
            return;
        }

        for (int i = 0; i < causticLines.Length; i++)
        {
            LineRenderer line = causticLines[i];
            if (line == null)
            {
                continue;
            }

            float width = 0.018f + Mathf.Sin(Time.time * 1.4f + i * 0.47f) * 0.006f;
            line.startWidth = width;
            line.endWidth = width * 0.72f;
        }

        if (foamLines == null)
        {
            return;
        }

        for (int i = 0; i < foamLines.Length; i++)
        {
            LineRenderer line = foamLines[i];
            if (line == null)
            {
                continue;
            }

            float shimmer = 0.5f + Mathf.Sin(Time.time * 1.8f + i * 0.91f) * 0.5f;
            Color color = Color.Lerp(new Color(foamColor.r, foamColor.g, foamColor.b, 0.12f), foamColor, shimmer);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
        }
    }

    private void CreateSunlight()
    {
        for (int i = 0; i < 4; i++)
        {
            GameObject lightObject = new GameObject("Underwater Sun Spot");
            lightObject.transform.SetParent(generatedRoot, false);
            lightObject.transform.position = new Vector3(Mathf.Lerp(-10f, 10f, i / 3f), waterSurfaceY - 0.1f, -5f + i * 3f);
            lightObject.transform.rotation = Quaternion.Euler(78f, Mathf.Lerp(-20f, 20f, i / 3f), 0f);

            Light spot = lightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.82f, 0.98f, 1f);
            spot.intensity = 1.65f;
            spot.range = waterSurfaceY - seabedY + 3f;
            spot.spotAngle = 54f;
            spot.shadows = LightShadows.None;
        }
    }

    private void CreateCausticLines()
    {
        causticLines = new LineRenderer[causticLineCount];

        for (int i = 0; i < causticLineCount; i++)
        {
            GameObject lineObject = new GameObject("Thin Reef Caustic");
            lineObject.transform.SetParent(generatedRoot, false);

            Vector3 start = RandomSeabedPosition(0.72f) + Vector3.up * 0.035f;
            float length = Random.Range(0.55f, 1.35f);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = causticLineMaterial;
            line.positionCount = 4;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.startWidth = 0.018f;
            line.endWidth = 0.012f;

            for (int point = 0; point < 4; point++)
            {
                float t = point / 3f;
                Vector3 position = start + direction * (length * (t - 0.5f));
                position += side * Mathf.Sin(t * Mathf.PI * 2f + i) * 0.08f;
                line.SetPosition(point, position);
            }

            causticLines[i] = line;
        }
    }

    private void CreateSurfaceHighlights()
    {
        int highlightCount = Mathf.Max(8, causticLineCount / 2);
        foamLines = new LineRenderer[highlightCount];
        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;

        for (int i = 0; i < highlightCount; i++)
        {
            GameObject lineObject = new GameObject("Surface Sparkle");
            lineObject.transform.SetParent(generatedRoot, false);

            float x = Random.Range(-halfX * 0.92f, halfX * 0.92f);
            float z = Random.Range(-halfZ * 0.92f, halfZ * 0.92f);
            float length = Random.Range(0.35f, 1.1f);
            float angle = Random.Range(-35f, 35f) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = foamMaterial;
            line.positionCount = 3;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.startWidth = Random.Range(0.012f, 0.028f);
            line.endWidth = 0.004f;
            line.startColor = foamColor;
            line.endColor = new Color(foamColor.r, foamColor.g, foamColor.b, 0.12f);

            Vector3 center = new Vector3(x, waterSurfaceY + 0.045f, z);
            line.SetPosition(0, center - direction * length * 0.5f);
            line.SetPosition(1, center + Vector3.up * Random.Range(0.01f, 0.04f));
            line.SetPosition(2, center + direction * length * 0.5f);
            foamLines[i] = line;
        }
    }

    private void CreateDecorations()
    {
        for (int i = 0; i < rockCount; i++)
        {
            Vector3 position = RandomSeabedPosition(0.82f);
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Seabed Rock";
            rock.transform.SetParent(generatedRoot, false);
            rock.transform.position = position;
            rock.transform.localScale = new Vector3(Random.Range(0.7f, 1.8f), Random.Range(0.18f, 0.45f), Random.Range(0.5f, 1.35f));
            rock.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            rock.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
            DestroyCollider(rock);
        }

        for (int i = 0; i < simpleCoralCount; i++)
        {
            Vector3 position = RandomSeabedPosition(0.9f);
            GameObject coral = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coral.name = "Simple Coral";
            coral.transform.SetParent(generatedRoot, false);
            coral.transform.position = position + Vector3.up * 0.18f;
            coral.transform.localScale = new Vector3(Random.Range(0.08f, 0.18f), Random.Range(0.35f, 0.85f), Random.Range(0.08f, 0.18f));
            coral.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
            coral.GetComponent<MeshRenderer>().sharedMaterial = coralMaterial;
            DestroyCollider(coral);
        }

        for (int i = 0; i < branchCoralCount; i++)
        {
            CreateBranchCoralCluster(RandomSeabedPosition(0.86f), i);
        }
    }

    private void CreateBranchCoralCluster(Vector3 position, int index)
    {
        GameObject cluster = new GameObject("Branch Coral Cluster");
        cluster.transform.SetParent(generatedRoot, false);
        cluster.transform.position = position + Vector3.up * 0.05f;

        Material material = index % 3 == 0 ? coralMaterial : whiteCoralMaterial;
        int branchCount = Random.Range(7, 13);
        float baseYaw = Random.Range(0f, 360f);

        for (int i = 0; i < branchCount; i++)
        {
            float angle = baseYaw + i * (360f / branchCount) + Random.Range(-16f, 16f);
            float angleRadians = angle * Mathf.Deg2Rad;
            float spread = Random.Range(0.36f, 0.72f);
            Vector3 direction = new Vector3(
                Mathf.Cos(angleRadians) * spread,
                Random.Range(0.58f, 0.94f),
                Mathf.Sin(angleRadians) * spread
            ).normalized;
            float length = Random.Range(0.42f, 0.95f);
            float radius = Random.Range(0.025f, 0.055f);
            AddCoralBranch(cluster.transform, Vector3.zero, direction, length, radius, material);

            if (Random.value > 0.35f)
            {
                Vector3 forkStart = direction.normalized * length * Random.Range(0.45f, 0.72f);
                Vector3 forkDirection = Quaternion.Euler(Random.Range(-18f, 20f), Random.Range(-38f, 38f), Random.Range(-20f, 20f)) * direction;
                AddCoralBranch(cluster.transform, forkStart, forkDirection, length * Random.Range(0.38f, 0.58f), radius * 0.7f, material);
            }
        }

        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mound.name = "Coral Base";
        mound.transform.SetParent(cluster.transform, false);
        mound.transform.localPosition = Vector3.up * 0.03f;
        mound.transform.localScale = new Vector3(Random.Range(0.38f, 0.72f), 0.12f, Random.Range(0.38f, 0.72f));
        mound.GetComponent<MeshRenderer>().sharedMaterial = whiteCoralMaterial;
        DestroyCollider(mound);
    }

    private void AddCoralBranch(Transform parent, Vector3 start, Vector3 direction, float length, float radius, Material material)
    {
        GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        branch.name = "Coral Branch";
        branch.transform.SetParent(parent, false);

        Vector3 normalized = direction.normalized;
        branch.transform.localPosition = start + normalized * length * 0.5f;
        branch.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normalized);
        branch.transform.localScale = new Vector3(radius, length * 0.5f, radius);
        branch.GetComponent<MeshRenderer>().sharedMaterial = material;
        DestroyCollider(branch);
    }

    private void CreateBubbleColumns()
    {
        for (int i = 0; i < bubbleColumnCount; i++)
        {
            GameObject column = new GameObject("Bubble Column");
            column.transform.SetParent(generatedRoot, false);
            column.transform.position = RandomSeabedPosition(0.65f) + Vector3.up * 0.8f;

            ParticleSystem particles = column.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startColor = new Color(0.75f, 0.95f, 1f, 0.36f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.4f, 5.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(8f, 16f);

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        }
    }

    private Vector3 RandomSeabedPosition(float inset)
    {
        float x = Random.Range(-oceanSize.x * 0.5f * inset, oceanSize.x * 0.5f * inset);
        float z = Random.Range(-oceanSize.y * 0.5f * inset, oceanSize.y * 0.5f * inset);
        float ripple = Mathf.Sin(x * 0.8f + z * 0.35f) * 0.18f + Mathf.Cos(z * 0.9f) * 0.11f;
        return new Vector3(x, seabedY + ripple, z);
    }

    private void ClearGenerated()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
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
