using UnityEngine;

public partial class OceanEnvironment
{
    private const HideFlags GeneratedHideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

    private void CreateMaterials()
    {
        seabedMaterial = MakeMaterial("Reef White Sand", new Color(0.94f, 0.9f, 0.72f, 1f), 0f);
        waterMaterial = MakeMaterial("Bright Reef Water Surface", waterColor, 0.72f);
        rockMaterial = MakeMaterial("Reef Rock", new Color(0.5f, 0.62f, 0.52f, 1f), 0f);
        coralMaterial = MakeMaterial("Warm Reef Coral", new Color(1f, 0.66f, 0.5f, 1f), 0f);
        whiteCoralMaterial = MakeMaterial("Pale Branch Coral", new Color(0.94f, 0.9f, 0.74f, 1f), 0f);
        shorelineMaterial = MakeMaterial("Dry Shore Sand", new Color(0.95f, 0.84f, 0.58f, 1f), 0f);
        shorePlantMaterial = MakeMaterial("Sparse Shore Grass", new Color(0.48f, 0.66f, 0.36f, 1f), 0f);
        causticLineMaterial = MakeUnlitMaterial("Thin Reef Caustics", new Color(0.66f, 0.95f, 1f, 0.22f));
        foamMaterial = MakeUnlitMaterial("Soft Reef Surface Sparkle", foamColor);
    }

    private Material MakeMaterial(string materialName, Color color, float transparent)
    {
        Shader shader = FindSupportedShader(
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Standard",
            "Sprites/Default"
        );

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = GeneratedHideFlags
        };

        SetMaterialColor(material, color);
        ConfigureCommonMaterial(material, transparent > 0f);

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", transparent > 0f ? 0.72f : 0.28f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        return material;
    }

    private Material MakeUnlitMaterial(string materialName, Color color)
    {
        Shader shader = FindSupportedShader(
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Unlit/Transparent",
            "Unlit/Color"
        );

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = GeneratedHideFlags
        };

        SetMaterialColor(material, color);
        ConfigureCommonMaterial(material, true);
        return material;
    }

    private void FinalizeGeneratedRoot()
    {
        if (generatedRoot == null)
        {
            return;
        }

        ApplyGeneratedHideFlags(generatedRoot.gameObject);
        RepairUnsupportedMaterials(generatedRoot.gameObject);
    }

    private static void MarkGeneratedObject(GameObject target)
    {
        if (target != null)
        {
            target.hideFlags = GeneratedHideFlags;
        }
    }

    private static void ApplyGeneratedHideFlags(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item != null)
            {
                item.gameObject.hideFlags = GeneratedHideFlags;
            }
        }

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter != null && filter.sharedMesh != null)
            {
                filter.sharedMesh.hideFlags = GeneratedHideFlags;
            }
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                {
                    materials[i].hideFlags = GeneratedHideFlags;
                }
            }
        }
    }

    private void RepairUnsupportedMaterials(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = ChooseFallbackMaterial(renderer, null);
                continue;
            }

            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (!IsBrokenOrIncompatibleMaterial(materials[i]))
                {
                    continue;
                }

                materials[i] = ChooseFallbackMaterial(renderer, materials[i]);
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private Material ChooseFallbackMaterial(Renderer renderer, Material source)
    {
        string key = $"{renderer?.name} {renderer?.transform.parent?.name} {source?.name}".ToLowerInvariant();
        if (key.Contains("water") || key.Contains("surface") || key.Contains("suimono"))
        {
            return waterMaterial;
        }

        if (key.Contains("caustic") || key.Contains("sparkle") || key.Contains("foam"))
        {
            return key.Contains("foam") || key.Contains("sparkle") ? foamMaterial : causticLineMaterial;
        }

        if (key.Contains("coral"))
        {
            return key.Contains("pale") || key.Contains("branch") ? whiteCoralMaterial : coralMaterial;
        }

        if (key.Contains("rock") || key.Contains("stone") || key.Contains("spire"))
        {
            return rockMaterial;
        }

        return seabedMaterial;
    }

    private static Shader FindSupportedShader(params string[] shaderNames)
    {
        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (IsUsableShader(shader))
            {
                return shader;
            }
        }

        Shader fallback = Shader.Find("Sprites/Default");
        if (fallback != null)
        {
            return fallback;
        }

        Debug.LogError("OceanEnvironment: no supported fallback shader was found.");
        return Shader.Find("Hidden/InternalErrorShader");
    }

    private static bool IsBrokenOrIncompatibleMaterial(Material material)
    {
        if (material == null || !IsUsableShader(material.shader))
        {
            return true;
        }

        if (!IsUniversalRenderPipelineActive())
        {
            return false;
        }

        string shaderName = material.shader.name.ToLowerInvariant();
        return shaderName == "standard"
            || shaderName.StartsWith("legacy shaders/")
            || shaderName.StartsWith("mobile/")
            || shaderName.Contains("suimono");
    }

    private static bool IsUsableShader(Shader shader)
    {
        return shader != null
            && shader.isSupported
            && shader.name != "Hidden/InternalErrorShader";
    }

    private static void ConfigureCommonMaterial(Material material, bool transparent)
    {
        if (material == null)
        {
            return;
        }

        if (transparent)
        {
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_Cull", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return;
        }

        SetFloatIfPresent(material, "_Surface", 0f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_ZWrite", 1f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = -1;
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
