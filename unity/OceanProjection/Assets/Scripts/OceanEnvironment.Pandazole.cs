using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class OceanEnvironment
{
    private GameObject[] pandazoleRockPrefabs;
    private GameObject[] pandazoleCoralPrefabs;
    private GameObject[] pandazoleBonePrefabs;

    private void PreparePandazoleAssets()
    {
        pandazoleRockPrefabs = null;
        pandazoleCoralPrefabs = null;
        pandazoleBonePrefabs = null;

        if (!usePandazoleNaturePackWhenAvailable)
        {
            return;
        }

#if UNITY_EDITOR
        pandazoleRockPrefabs = FindPandazolePrefabs("HardRock_");
        pandazoleCoralPrefabs = FindPandazolePrefabs("Coral_");
        pandazoleBonePrefabs = FindPandazolePrefabs("Bones_");
#endif
    }

    private bool TryCreatePandazoleRock(Vector3 position, Vector3 scale, string objectName)
    {
        if (!ShouldUsePandazole(pandazoleRockPrefabs, pandazoleRockShare))
        {
            return false;
        }

        GameObject rock = CreatePandazoleInstance(pandazoleRockPrefabs, objectName, rockMaterial);
        if (rock == null)
        {
            return false;
        }

        rock.transform.position = position;
        rock.transform.localRotation = Quaternion.Euler(Random.Range(-7f, 7f), Random.Range(0f, 360f), Random.Range(-7f, 7f));
        rock.transform.localScale = scale;
        DestroyGeneratedColliders(rock);
        return true;
    }

    private bool TryCreatePandazoleCoral(Vector3 position, Vector3 scale, string objectName)
    {
        if (!ShouldUsePandazole(pandazoleCoralPrefabs, pandazoleCoralShare))
        {
            return false;
        }

        Material fallback = Random.value > 0.35f ? coralMaterial : whiteCoralMaterial;
        GameObject coral = CreatePandazoleInstance(pandazoleCoralPrefabs, objectName, fallback);
        if (coral == null)
        {
            return false;
        }

        coral.transform.position = position;
        coral.transform.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
        coral.transform.localScale = scale;
        DestroyGeneratedColliders(coral);
        return true;
    }

    private void CreatePandazoleBoneAccents()
    {
        if (!HasPandazolePrefabs(pandazoleBonePrefabs))
        {
            return;
        }

        int accentCount = Mathf.Clamp(bubbleColumnCount + 3, 4, 10);
        for (int i = 0; i < accentCount; i++)
        {
            OceanFeatureKind feature = i % 2 == 0 ? OceanFeatureKind.Trench : OceanFeatureKind.Reef;
            Vector3 localCenter = TryGetFeaturePoint(feature, out Vector3 featurePoint)
                ? transform.InverseTransformPoint(featurePoint)
                : Vector3.zero;
            Vector3 position = SampleSeabedPosition(
                localCenter.x + Random.Range(-12f, 12f),
                localCenter.z + Random.Range(-10f, 10f)
            );

            GameObject bones = CreatePandazoleInstance(pandazoleBonePrefabs, "Seabed Bones", whiteCoralMaterial);
            if (bones == null)
            {
                continue;
            }

            float scale = Random.Range(0.75f, 1.45f);
            bones.transform.position = position + Vector3.up * 0.04f;
            bones.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));
            bones.transform.localScale = new Vector3(scale, scale, scale);
            DestroyGeneratedColliders(bones);
        }
    }

    private GameObject CreatePandazoleInstance(GameObject[] prefabs, string objectName, Material fallbackMaterial)
    {
        if (!HasPandazolePrefabs(prefabs))
        {
            return null;
        }

        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, generatedRoot);
        instance.name = objectName;
        RepairPandazoleMaterials(instance, fallbackMaterial);
        return instance;
    }

    private void RepairPandazoleMaterials(GameObject instance, Material fallbackMaterial)
    {
        if (instance == null)
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = fallbackMaterial;
                continue;
            }

            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (!IsBrokenOrIncompatibleMaterial(materials[i]))
                {
                    continue;
                }

                materials[i] = fallbackMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static bool ShouldUsePandazole(GameObject[] prefabs, float share)
    {
        return HasPandazolePrefabs(prefabs) && Random.value <= Mathf.Clamp01(share);
    }

    private static bool HasPandazolePrefabs(GameObject[] prefabs)
    {
        return prefabs != null && prefabs.Length > 0;
    }

    private static void DestroyGeneratedColliders(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

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

#if UNITY_EDITOR
    private static GameObject[] FindPandazolePrefabs(string namePrefix)
    {
        const string folder = "Assets/Pandazole_Ultimate_Pack/Pandazole Nature Environment Pack/Prefabs";
        string[] guids = AssetDatabase.FindAssets($"t:Prefab {namePrefix}", new[] { folder });
        System.Collections.Generic.List<GameObject> prefabs = new System.Collections.Generic.List<GameObject>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !prefab.name.StartsWith(namePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            prefabs.Add(prefab);
        }

        return prefabs.ToArray();
    }
#endif
}
