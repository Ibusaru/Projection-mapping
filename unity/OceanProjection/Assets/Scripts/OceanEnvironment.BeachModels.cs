using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class OceanEnvironment
{
    [Header("Beach Asset Packs")]
    [SerializeField] private bool useSimpleBeachModelsWhenAvailable = true;
    [SerializeField, Range(0, 96)] private int beachPalmCount = 56;
    [SerializeField, Range(0, 64)] private int beachParasolCount = 32;
    [SerializeField, Range(0, 180)] private int beachSmallPropCount = 120;
    [SerializeField, Range(0.5f, 2f)] private float beachModelScale = 1.1f;
    [SerializeField] private bool useYughuesSandMaterialsWhenAvailable = true;
    [SerializeField] private Material dryBeachSandMaterial;
    [SerializeField] private Material seabedSandMaterial;
    [SerializeField] private string dryBeachSandMaterialName = "M_YFSM_Sand03";
    [SerializeField] private string seabedSandMaterialName = "M_YFSM_Sand05";

    [SerializeField] private GameObject beachParasolPrefab;
    [SerializeField] private GameObject beachPalmPrefab;
    [SerializeField] private GameObject beachSunbedPrefab;
    [SerializeField] private GameObject beachDeskPrefab;
    [SerializeField] private GameObject beachLifebuoyPrefab;
    [SerializeField] private GameObject beachStarfishPrefab;
    [SerializeField] private GameObject beachBallPrefab;
    [SerializeField] private GameObject beachToyOnePrefab;
    [SerializeField] private GameObject beachToyTwoPrefab;
    [SerializeField] private GameObject beachAquariusPrefab;

    private readonly List<Bounds> beachPropBounds = new List<Bounds>();
    private int beachPrefabCreateAttempts;
    private int beachPrefabDryRejects;
    private int beachPrefabInstantiateFailures;
    private int beachPrefabAnchorFailures;
    private int beachPrefabOverlapRejects;
    private string beachLastInstantiateError = string.Empty;

    private void PrepareBeachAssets()
    {
        if (!useSimpleBeachModelsWhenAvailable)
        {
            return;
        }

#if UNITY_EDITOR
        // Prefer the imported model root over the thin package wrapper.  The
        // wrapper assets in this package can resolve to an unnamed, non-
        // instantiable GameObject after a package refresh; the FBX model roots
        // are the actual renderable assets and instantiate reliably.
        beachParasolPrefab = FindSimpleBeachPrefab("Parasol") ?? beachParasolPrefab;
        beachPalmPrefab = FindSimpleBeachPrefab("Palm") ?? beachPalmPrefab;
        beachSunbedPrefab = FindSimpleBeachPrefab("Sunbed") ?? beachSunbedPrefab;
        beachDeskPrefab = FindSimpleBeachPrefab("Desk") ?? beachDeskPrefab;
        beachLifebuoyPrefab = FindSimpleBeachPrefab("Lifebuoy") ?? beachLifebuoyPrefab;
        beachStarfishPrefab = FindSimpleBeachPrefab("Starfish") ?? beachStarfishPrefab;
        beachBallPrefab = FindSimpleBeachPrefab("Ball") ?? beachBallPrefab;
        beachToyOnePrefab = FindSimpleBeachPrefab("Beach_Toy_1") ?? beachToyOnePrefab;
        beachToyTwoPrefab = FindSimpleBeachPrefab("Beach_Toy_2") ?? beachToyTwoPrefab;
        beachAquariusPrefab = FindSimpleBeachPrefab("Aquarius") ?? beachAquariusPrefab;
#endif
    }

    private void CreateBeachModelDecorations()
    {
        if (!createVisibleShoreline || !useSimpleBeachModelsWhenAvailable || !HasBeachModelPrefabs())
        {
            return;
        }

        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;
        float activeHalfX = ShorelineActiveHalfX(halfX);
        float usableWidth = ShorelineUsableWidth(halfX, activeHalfX);

        GameObject beachRootObject = new GameObject("Beach Props");
        beachRootObject.transform.SetParent(generatedRoot, false);
        MarkGeneratedObject(beachRootObject);
        Transform beachRoot = beachRootObject.transform;
        beachPropBounds.Clear();
        beachPrefabCreateAttempts = 0;
        beachPrefabDryRejects = 0;
        beachPrefabInstantiateFailures = 0;
        beachPrefabAnchorFailures = 0;
        beachPrefabOverlapRejects = 0;
        beachLastInstantiateError = string.Empty;

        CreateBeachParasolGroups(beachRoot, halfX, halfZ, activeHalfX, usableWidth);
        CreateBeachPalmScatter(beachRoot, halfX, halfZ, activeHalfX, usableWidth);
        CreateBeachSmallPropScatter(beachRoot, halfX, halfZ, activeHalfX, usableWidth);
        Debug.Log(
            $"OceanEnvironment: Beach Props built. attempts={beachPrefabCreateAttempts}, " +
            $"dryRejects={beachPrefabDryRejects}, instantiateFailures={beachPrefabInstantiateFailures}, " +
            $"anchorFailures={beachPrefabAnchorFailures}, overlapRejects={beachPrefabOverlapRejects}, " +
            $"children={beachRoot.childCount}"
        );
    }

    public string DescribeBeachDecorationBuild()
    {
        Transform root = transform.Find("_GeneratedOceanEnvironment/Beach Props");
        return $"attempts={beachPrefabCreateAttempts}, dryRejects={beachPrefabDryRejects}, " +
            $"instantiateFailures={beachPrefabInstantiateFailures}, anchorFailures={beachPrefabAnchorFailures}, " +
            $"overlapRejects={beachPrefabOverlapRejects}, children={(root != null ? root.childCount : 0)}, " +
            $"lastInstantiateError={beachLastInstantiateError}";
    }

    private void CreateBeachPalmScatter(Transform parent, float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        if (beachPalmCount <= 0)
        {
            return;
        }

        // Keep a readable central resort cluster, then continue the planting
        // across every dry part of the generated coast.  The old six-item
        // background cap was why the land looked empty immediately outside
        // the first camera frame.
        int totalCount = Mathf.Clamp(beachPalmCount, 0, 96);
        int showcaseCount = Mathf.Min(totalCount, 8);
        int backgroundCount = totalCount - showcaseCount;
        for (int i = 0; i < totalCount; i++)
        {
            bool showcase = i < showcaseCount;
            float alongX = showcase
                ? BeachNoisyAlongShore(i, showcaseCount, OceanShorelineLayout.ShowcaseHalfSize.y * 0.86f, 11)
                : BeachNoisyAlongShore(i - showcaseCount, backgroundCount, halfZ * 0.88f, 31);
            float inland = showcase
                ? Mathf.Lerp(0.42f, 0.68f, BeachNoise01(i, 17))
                : Mathf.Lerp(0.38f, 0.72f, BeachNoise01(i, 37));

            Vector3 position = SampleBeachSurfacePoint(inland, alongX, halfX, halfZ, activeHalfX, usableWidth);
            float scale = beachModelScale * Random.Range(1.08f, 1.74f);
            GameObject palm = CreateBeachPrefabInstance(
                beachPalmPrefab,
                $"Beach Palm {i + 1:00}",
                parent,
                position + Vector3.up * 0.02f,
                Random.Range(0f, 360f),
                Vector3.one * scale,
                0.16f,
                !showcase
            );

            if (palm == null)
            {
                WarnMissingBeachPrefab("Palm");
            }

            if (palm != null)
            {
                palm.transform.rotation *= Quaternion.Euler(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-3.5f, 3.5f));
                // Tilting around an imported model pivot can lift the trunk
                // after the initial grounding pass. Re-anchor the final pose.
                if (!AnchorBeachPrefabToSurface(palm))
                {
                    beachPrefabAnchorFailures++;
                    DestroyGeneratedObject(palm);
                }
            }
        }
    }

    private void CreateBeachParasolGroups(Transform parent, float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        if (beachParasolCount <= 0)
        {
            return;
        }

        int clusterCount = Mathf.Clamp(beachParasolCount, 0, 64);
        int showcaseCount = Mathf.Min(clusterCount, 8);
        int backgroundCount = clusterCount - showcaseCount;
        for (int i = 0; i < clusterCount; i++)
        {
            bool showcase = i < showcaseCount;
            float alongX = showcase
                ? BeachNoisyAlongShore(i, showcaseCount, OceanShorelineLayout.ShowcaseHalfSize.y * 0.72f, 53)
                : BeachNoisyAlongShore(i - showcaseCount, backgroundCount, halfZ * 0.86f, 71);
            float inland = showcase
                ? Mathf.Lerp(0.18f, 0.42f, BeachNoise01(i, 59))
                : Mathf.Lerp(0.16f, 0.50f, BeachNoise01(i, 79));
            Vector3 center = SampleBeachSurfacePoint(inland, alongX, halfX, halfZ, activeHalfX, usableWidth);
            if (!TryReserveBeachCluster(center, showcase ? 9.5f : 8.7f))
            {
                continue;
            }

            float yaw = Random.Range(0f, 360f);
            float scale = beachModelScale * Random.Range(1.05f, 1.42f);

            GameObject parasol = CreateBeachPrefabInstance(beachParasolPrefab, $"Beach Parasol {i + 1:00}", parent, center, yaw, Vector3.one * scale, 0.2f, false);
            if (parasol == null)
            {
                WarnMissingBeachPrefab("Parasol");
                continue;
            }

            Vector3 forward = YawToForward(yaw);
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            PlaceBeachCompanion(parent, beachSunbedPrefab, $"Beach Sunbed {i + 1:00}A", center + right * 2.35f + forward * 0.35f, yaw + 88f, beachModelScale * Random.Range(0.88f, 1.08f), halfX, halfZ, activeHalfX, usableWidth, false);
            PlaceBeachCompanion(parent, beachSunbedPrefab, $"Beach Sunbed {i + 1:00}B", center - right * 2.35f + forward * 0.35f, yaw - 88f, beachModelScale * Random.Range(0.88f, 1.08f), halfX, halfZ, activeHalfX, usableWidth, false);

            if (i % 2 == 0)
            {
                PlaceBeachCompanion(parent, beachDeskPrefab, $"Beach Desk {i + 1:00}", center + forward * 2.05f, yaw, beachModelScale * Random.Range(0.72f, 0.9f), halfX, halfZ, activeHalfX, usableWidth, false);
            }

            if (i % 3 == 1)
            {
                PlaceBeachCompanion(parent, beachLifebuoyPrefab, $"Beach Lifebuoy {i + 1:00}", center - forward * 2.2f, yaw + 18f, beachModelScale * 0.84f, halfX, halfZ, activeHalfX, usableWidth, false);
            }
        }
    }

    private void CreateBeachSmallPropScatter(Transform parent, float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        if (beachSmallPropCount <= 0)
        {
            return;
        }

        GameObject[] smallPrefabs =
        {
            beachStarfishPrefab,
            beachBallPrefab,
            beachToyOnePrefab,
            beachToyTwoPrefab,
            beachLifebuoyPrefab,
            beachAquariusPrefab
        };

        int propCount = Mathf.Clamp(beachSmallPropCount, 0, 120);
        for (int i = 0; i < propCount; i++)
        {
            GameObject prefab = smallPrefabs[i % smallPrefabs.Length];
            if (prefab == null)
            {
                WarnMissingBeachPrefab($"small prop #{i % smallPrefabs.Length}");
                continue;
            }

            bool showcase = i < Mathf.Min(propCount, 30);
            float alongX = showcase
                ? BeachNoisyAlongShore(i, Mathf.Min(propCount, 30), OceanShorelineLayout.ShowcaseHalfSize.y * 0.82f, 101)
                : BeachNoisyAlongShore(i - 30, Mathf.Max(1, propCount - 30), halfZ * 0.9f, 131);
            float inland = showcase
                ? Mathf.Lerp(0.16f, 0.52f, BeachNoise01(i, 107))
                : Mathf.Lerp(0.14f, 0.58f, BeachNoise01(i, 137));
            Vector3 position = SampleBeachSurfacePoint(inland, alongX, halfX, halfZ, activeHalfX, usableWidth);
            float scale = beachModelScale * Random.Range(0.58f, 1.12f);
            if (prefab == beachStarfishPrefab)
            {
                scale *= Random.Range(0.84f, 1.36f);
            }

            CreateBeachPrefabInstance(
                prefab,
                $"Beach {prefab.name}",
                parent,
                position + Vector3.up * 0.04f,
                Random.Range(0f, 360f),
                Vector3.one * scale,
                0.3f,
                !showcase
            );
        }
    }

    private void PlaceBeachCompanion(
        Transform parent,
        GameObject prefab,
        string objectName,
        Vector3 requestedPosition,
        float yaw,
        float scale,
        float halfX,
        float halfZ,
        float activeHalfX,
        float usableWidth,
        bool reserveFootprint)
    {
        Vector3 position = ClampToBeachSurface(requestedPosition, halfX, halfZ, activeHalfX, usableWidth);
        GameObject instance = CreateBeachPrefabInstance(prefab, objectName, parent, position, yaw, Vector3.one * scale, 0.32f, reserveFootprint);
        if (instance == null)
        {
            WarnMissingBeachPrefab(objectName);
        }
    }

    private GameObject CreateBeachPrefabInstance(
        GameObject prefab,
        string objectName,
        Transform parent,
        Vector3 position,
        float yaw,
        Vector3 scale,
        float surfaceNormalBlend,
        bool reserveFootprint)
    {
        beachPrefabCreateAttempts++;
        if (parent == null)
        {
            beachPrefabInstantiateFailures++;
            return null;
        }

        // Some Unity package refreshes leave the thin wrapper prefab present
        // but non-instantiable. Keep the scene readable in that case (and in
        // a player build where editor-only FBX substitution is unavailable)
        // with a small, deterministic procedural prop of the same category.
        GameObject instance;
        if (prefab == null)
        {
            beachPrefabInstantiateFailures++;
            instance = CreateProceduralBeachFallback(objectName, parent, position, yaw, scale);
            if (instance == null)
            {
                return null;
            }
        }
        else if (!IsDryBeachSurfacePosition(position, 5f))
        {
            beachPrefabDryRejects++;
            Debug.LogWarning($"OceanEnvironment: skipped beach prefab '{prefab.name}' outside the dry shoreline zone.");
            return null;
        }
        else
        {
            instance = InstantiateBeachPrefab(prefab, parent);
            if (instance == null)
            {
                beachPrefabInstantiateFailures++;
                instance = CreateProceduralBeachFallback(objectName, parent, position, yaw, scale);
                if (instance == null)
                {
                    return null;
                }
            }
        }

        // Instantiate as a plain scene copy.  PrefabUtility.InstantiatePrefab
        // can leave an editor prefab instance rooted at (0,0,0) while this
        // ExecuteAlways builder is rebuilding the generated hierarchy; that
        // was the reason every beach model appeared stacked at the origin.
        instance.name = objectName;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = position;
        Vector3 normal = SampleBeachSurfaceNormal(position.x, position.z);
        Vector3 blendedNormal = Vector3.Lerp(Vector3.up, normal, Mathf.Clamp01(surfaceNormalBlend)).normalized;
        Vector3 tangent = OceanShorelineLayout.Tangent(oceanSize, activeAreaSize, shorelineWidth, decorationSeed, position.z);
        float tangentYaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
        instance.transform.localRotation = Quaternion.FromToRotation(Vector3.up, blendedNormal) * Quaternion.Euler(0f, tangentYaw + yaw, 0f);
        instance.transform.localScale = scale;
        DestroyGeneratedColliders(instance);
        RepairBeachModelMaterials(instance);

        if (!AnchorBeachPrefabToSurface(instance))
        {
            beachPrefabAnchorFailures++;
            DestroyGeneratedObject(instance);
            return null;
        }

        if (reserveFootprint && !TryReserveBeachPropBounds(instance))
        {
            beachPrefabOverlapRejects++;
            DestroyGeneratedObject(instance);
            return null;
        }

        MarkGeneratedObject(instance);
        return instance;
    }

    private GameObject CreateProceduralBeachFallback(
        string objectName,
        Transform parent,
        Vector3 position,
        float yaw,
        Vector3 scale)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject root = new GameObject(objectName + " (Fallback)");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        root.transform.localScale = scale;

        string lowerName = objectName.ToLowerInvariant();
        bool parasol = lowerName.Contains("parasol");
        bool palm = lowerName.Contains("palm");
        bool sunbed = lowerName.Contains("sunbed");
        bool desk = lowerName.Contains("desk");
        bool lifebuoy = lowerName.Contains("lifebuoy");
        Material material = MakeMaterial(
            "Fallback " + objectName,
            parasol ? new Color(0.18f, 0.67f, 0.86f)
                : palm ? new Color(0.24f, 0.48f, 0.22f)
                : lifebuoy ? new Color(0.92f, 0.16f, 0.1f)
                : new Color(0.88f, 0.72f, 0.36f),
            0f
        );

        if (parasol)
        {
            AddFallbackPrimitive(root.transform, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.7f, 0f), new Vector3(0.1f, 1.7f, 0.1f), material);
            AddFallbackPrimitive(root.transform, PrimitiveType.Sphere, "Canopy", new Vector3(0f, 3.45f, 0f), new Vector3(2.8f, 0.28f, 2.8f), material);
        }
        else if (palm)
        {
            AddFallbackPrimitive(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 2.45f, 0f), new Vector3(0.3f, 2.45f, 0.3f), material);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                Transform leaf = AddFallbackPrimitive(
                    root.transform,
                    PrimitiveType.Sphere,
                    "Leaf " + i,
                    new Vector3(Mathf.Cos(angle) * 1.35f, 4.9f, Mathf.Sin(angle) * 1.35f),
                    new Vector3(1.45f, 0.16f, 0.42f),
                    material
                );
                leaf.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 12f);
            }
        }
        else if (sunbed)
        {
            AddFallbackPrimitive(root.transform, PrimitiveType.Cube, "Bed", new Vector3(0f, 0.28f, 0f), new Vector3(1.55f, 0.12f, 0.62f), material);
            AddFallbackPrimitive(root.transform, PrimitiveType.Cube, "Back", new Vector3(-0.52f, 0.72f, 0f), new Vector3(0.12f, 0.82f, 0.62f), material);
        }
        else if (desk)
        {
            AddFallbackPrimitive(root.transform, PrimitiveType.Cube, "Top", new Vector3(0f, 0.82f, 0f), new Vector3(0.9f, 0.1f, 0.72f), material);
            AddFallbackPrimitive(root.transform, PrimitiveType.Cylinder, "Leg", new Vector3(0f, 0.42f, 0f), new Vector3(0.1f, 0.42f, 0.1f), material);
        }
        else if (lifebuoy)
        {
            AddFallbackPrimitive(root.transform, PrimitiveType.Sphere, "Ring", new Vector3(0f, 0.18f, 0f), new Vector3(0.72f, 0.16f, 0.72f), material);
        }
        else
        {
            PrimitiveType primitive = lowerName.Contains("ball") ? PrimitiveType.Sphere : PrimitiveType.Cube;
            AddFallbackPrimitive(root.transform, primitive, "Prop", new Vector3(0f, 0.18f, 0f), new Vector3(0.48f, 0.24f, 0.48f), material);
        }

        DestroyGeneratedColliders(root);
        return root;
    }

    private static Transform AddFallbackPrimitive(
        Transform parent,
        PrimitiveType primitiveType,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject item = GameObject.CreatePrimitive(primitiveType);
        item.name = objectName;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = localPosition;
        item.transform.localScale = localScale;
        Renderer renderer = item.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return item.transform;
    }

    private void WarnMissingBeachPrefab(string label)
    {
        Debug.LogWarning($"OceanEnvironment: Simple_Beach_Models prefab for {label} is unavailable or could not be safely placed; using the procedural fallback.");
    }

    private bool TryReserveBeachPropBounds(GameObject instance)
    {
        if (!TryGetRendererBounds(instance, out Bounds bounds))
        {
            return true;
        }

        Bounds paddedBounds = new Bounds(
            new Vector3(bounds.center.x, 0f, bounds.center.z),
            new Vector3(bounds.size.x + 0.45f, 1f, bounds.size.z + 0.45f)
        );
        for (int i = 0; i < beachPropBounds.Count; i++)
        {
            if (beachPropBounds[i].Intersects(paddedBounds))
            {
                Debug.LogWarning($"OceanEnvironment: skipped overlapping beach prefab '{instance.name}'.");
                return false;
            }
        }

        beachPropBounds.Add(paddedBounds);
        return true;
    }

    private bool TryReserveBeachCluster(Vector3 center, float diameter)
    {
        Bounds footprint = new Bounds(
            new Vector3(center.x, 0f, center.z),
            new Vector3(diameter, 1f, diameter)
        );
        for (int i = 0; i < beachPropBounds.Count; i++)
        {
            if (beachPropBounds[i].Intersects(footprint))
            {
                return false;
            }
        }

        beachPropBounds.Add(footprint);
        return true;
    }

    private bool AnchorBeachPrefabToSurface(GameObject instance)
    {
        if (!TryGetRendererBounds(instance, out Bounds bounds))
        {
            return false;
        }

        float sandY = SampleShorelineLandHeight(instance.transform.position.x, instance.transform.position.z);
        instance.transform.position += Vector3.up * (sandY - bounds.min.y);
        if (!TryGetRendererBounds(instance, out bounds))
        {
            return false;
        }

        Vector3 groundContact = new Vector3(instance.transform.position.x, bounds.min.y, instance.transform.position.z);
        return Mathf.Abs(bounds.min.y - sandY) <= 0.035f
            && IsDryBeachSurfacePosition(groundContact, 5f);
    }

    private bool IsDryBeachSurfacePosition(Vector3 position, float shorelineSafety)
    {
        return OceanShorelineLayout.IsDryBeachPoint(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY,
            position,
            shorelineSafety
        );
    }

    private GameObject InstantiateBeachPrefab(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            try
            {
                UnityEngine.Object editorInstance = PrefabUtility.InstantiatePrefab(prefab);
                GameObject editorObject = ResolveInstantiatedGameObject(editorInstance);
                if (editorObject != null)
                {
                    // Unpack before assigning the generated transform.  A
                    // linked prefab instance can reapply its asset transform
                    // during ExecuteAlways rebuilds, which previously put all
                    // beach props back at the origin.
                    if (PrefabUtility.IsPartOfPrefabInstance(editorObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            editorObject,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }

                    editorObject.transform.SetParent(parent, false);
                    return editorObject;
                }
            }
            catch (System.Exception exception)
            {
                beachLastInstantiateError = exception.Message;
                Debug.LogWarning($"OceanEnvironment: failed to instantiate beach prefab '{prefab.name}' in editor. {exception.Message}");
            }
        }
#endif

        try
        {
            UnityEngine.Object runtimeInstance = UnityEngine.Object.Instantiate((UnityEngine.Object)prefab, parent, false);
            GameObject gameObject = ResolveInstantiatedGameObject(runtimeInstance);
            if (gameObject == null)
            {
                beachLastInstantiateError = $"Instantiate returned null. prefab={prefab.name}, type={prefab.GetType().Name}, id={prefab.GetInstanceID()}";
            }

            return gameObject;
        }
        catch (System.Exception exception)
        {
            beachLastInstantiateError = exception.Message;
            Debug.LogWarning($"OceanEnvironment: failed to instantiate beach prefab '{prefab.name}'. {exception.Message}");
            return null;
        }
    }

    private static GameObject ResolveInstantiatedGameObject(UnityEngine.Object instance)
    {
        if (instance is GameObject gameObject)
        {
            return gameObject;
        }

        return instance is Component component ? component.gameObject : null;
    }

    public bool TryGetBeachDecorationPoint(out Vector3 point)
    {
        if (!createVisibleShoreline)
        {
            return TryGetFeaturePoint(OceanFeatureKind.Beach, out point);
        }

        float halfZ = oceanSize.y * 0.5f;
        Vector3 localPoint = SampleBeachSurfacePoint(0.52f, 0f, oceanSize.x * 0.5f, halfZ, 0f, 0f);
        point = transform.TransformPoint(localPoint);
        return true;
    }

    private Vector3 SampleBeachSurfacePoint(float inland, float alongShore, float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        return OceanShorelineLayout.SampleDryBeachPoint(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY,
            Mathf.Clamp(inland, 0.16f, 0.94f),
            alongShore
        );
    }

    private Vector3 ClampToBeachSurface(Vector3 position, float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        float z = Mathf.Clamp(position.z, -halfZ * 0.9f, halfZ * 0.9f);
        float shoreX = ShorelineStartX(z);
        float x = Mathf.Clamp(position.x, shoreX + 8f, halfX - 7f);
        return new Vector3(x, SampleShorelineLandHeight(x, z), z);
    }

    private float BeachDecorationXAt(float normalized, float halfX, float spanRatio)
    {
        float span = oceanSize.y * 0.5f * 0.88f * Mathf.Clamp01(spanRatio);
        return Mathf.Lerp(-span, span, Mathf.Clamp01(normalized));
    }

    private float BeachNoisyAlongShore(int index, int count, float halfSpan, int salt)
    {
        if (count <= 1)
        {
            return (BeachNoise01(index, salt) - 0.5f) * halfSpan * 0.38f;
        }

        float slot = (index + 0.5f) / count;
        float jitter = (BeachNoise01(index, salt) - 0.5f) * 0.72f / count;
        float lowFrequency = (BeachNoise01(index, salt + 1) - 0.5f) * 0.055f;
        return Mathf.Lerp(-halfSpan, halfSpan, Mathf.Clamp01(slot + jitter + lowFrequency));
    }

    private float BeachNoise01(int index, int salt)
    {
        float x = decorationSeed * 0.0173f + salt * 0.731f;
        float y = index * 0.913f + salt * 1.719f;
        return Mathf.PerlinNoise(x, y);
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
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

        return hasBounds;
    }

    private Vector3 SampleBeachSurfaceNormal(float x, float z)
    {
        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;
        float activeHalfX = ShorelineActiveHalfX(halfX);
        float usableWidth = ShorelineUsableWidth(halfX, activeHalfX);
        float step = 0.75f;
        Vector3 center = ClampToBeachSurface(new Vector3(x, 0f, z), halfX, halfZ, activeHalfX, usableWidth);
        Vector3 right = ClampToBeachSurface(new Vector3(x + step, 0f, z), halfX, halfZ, activeHalfX, usableWidth) - center;
        Vector3 forward = ClampToBeachSurface(new Vector3(x, 0f, z + step), halfX, halfZ, activeHalfX, usableWidth) - center;
        Vector3 normal = Vector3.Cross(forward, right).normalized;
        if (normal.sqrMagnitude < 0.001f)
        {
            return Vector3.up;
        }

        return normal.y >= 0f ? normal : -normal;
    }

    private void RepairBeachModelMaterials(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = shorelineMaterial;
                continue;
            }

            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = MakeCompatibleBeachMaterial(materials[i]);
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private Material MakeCompatibleBeachMaterial(Material source)
    {
        Color color = ReadMaterialColor(source, Color.white);
        Material material = MakeMaterial(source != null ? $"Beach {source.name}" : "Beach Prop", color, 0f);
        CopyTextureIfPresent(source, material, "_MainTex", "_BaseMap", Vector2.one);
        CopyTextureIfPresent(source, material, "_MainTex", "_MainTex", Vector2.one);
        CopyTextureIfPresent(source, material, "_BumpMap", "_BumpMap", Vector2.one);

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.28f);
        }

        return material;
    }

    private bool HasBeachModelPrefabs()
    {
        return beachParasolPrefab != null
            || beachPalmPrefab != null
            || beachSunbedPrefab != null
            || beachDeskPrefab != null
            || beachLifebuoyPrefab != null
            || beachStarfishPrefab != null
            || beachBallPrefab != null
            || beachToyOnePrefab != null
            || beachToyTwoPrefab != null
            || beachAquariusPrefab != null;
    }

    public bool HasAllBeachPrefabs()
    {
        return beachParasolPrefab != null
            && beachPalmPrefab != null
            && beachSunbedPrefab != null
            && beachDeskPrefab != null
            && beachLifebuoyPrefab != null
            && beachStarfishPrefab != null
            && beachBallPrefab != null
            && beachToyOnePrefab != null
            && beachToyTwoPrefab != null
            && beachAquariusPrefab != null;
    }

    private void ApplyYughuesSandMaterials()
    {
#if UNITY_EDITOR
        dryBeachSandMaterial = dryBeachSandMaterial != null ? dryBeachSandMaterial : FindYughuesSandMaterial(dryBeachSandMaterialName);
        seabedSandMaterial = seabedSandMaterial != null ? seabedSandMaterial : FindYughuesSandMaterial(seabedSandMaterialName);
#endif

        if (!useYughuesSandMaterialsWhenAvailable)
        {
            return;
        }

        if (dryBeachSandMaterial != null)
        {
            CopySandSourceMaterial(dryBeachSandMaterial, shorelineMaterial, new Vector2(3.1f, 6.4f), new Color(0.97f, 0.9f, 0.72f, 1f));
        }

        if (seabedSandMaterial != null)
        {
            CopySandSourceMaterial(seabedSandMaterial, seabedMaterial, new Vector2(7f, 5.2f), new Color(0.92f, 0.88f, 0.7f, 1f));
        }
    }

    private void CopySandSourceMaterial(Material source, Material target, Vector2 tiling, Color fallbackColor)
    {
        if (source == null || target == null)
        {
            return;
        }

        SetMaterialColor(target, ReadMaterialColor(source, fallbackColor));
        CopyTextureIfPresent(source, target, "_MainTex", "_BaseMap", tiling);
        CopyTextureIfPresent(source, target, "_MainTex", "_MainTex", tiling);
        CopyTextureIfPresent(source, target, "_BumpMap", "_BumpMap", tiling);
        CopyTextureIfPresent(source, target, "_SpecGlossMap", "_SpecGlossMap", tiling);

        if (target.HasProperty("_Smoothness"))
        {
            target.SetFloat("_Smoothness", 0.18f);
        }
    }

    private static void CopyTextureIfPresent(Material source, Material target, string sourceProperty, string targetProperty, Vector2 tiling)
    {
        if (source == null || target == null || !source.HasProperty(sourceProperty) || !target.HasProperty(targetProperty))
        {
            return;
        }

        Texture texture = source.GetTexture(sourceProperty);
        if (texture == null)
        {
            return;
        }

        target.SetTexture(targetProperty, texture);
        target.SetTextureScale(targetProperty, tiling);
        if (targetProperty == "_BumpMap")
        {
            target.EnableKeyword("_NORMALMAP");
        }
    }

    private static Color ReadMaterialColor(Material material, Color fallback)
    {
        if (material == null)
        {
            return fallback;
        }

        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        return material.HasProperty("_Color") ? material.GetColor("_Color") : fallback;
    }

    private static Vector3 YawToForward(float yaw)
    {
        float radians = yaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }

#if UNITY_EDITOR
    private static GameObject FindSimpleBeachPrefab(string prefabName)
    {
        const string modelsFolder = "Assets/Simple_Beach_Models/Models";
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder });
        foreach (string guid in modelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith($"/{prefabName}.fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null)
            {
                return model;
            }
        }

        const string folder = "Assets/Simple_Beach_Models/Prefabs";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private static Material FindYughuesSandMaterial(string materialName)
    {
        const string folder = "Assets/YughuesFreeSandMaterials/Materials";
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.name.Equals(materialName, System.StringComparison.OrdinalIgnoreCase))
            {
                return material;
            }
        }

        return null;
    }
#endif
}
