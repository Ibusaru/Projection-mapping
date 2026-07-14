using UnityEngine;

public partial class OceanEnvironment
{
    public float WaterSurfaceY => waterSurfaceY;
    public float ShorelineSurfaceY => shorelineSurfaceY;
    public Vector2 OceanSize => oceanSize;
    public Vector2 ActiveAreaSize => activeAreaSize;
    public float HorizonBackdropDistance => HorizonBackdropHalfExtent();

    public float SampleSandMaterialTransitionHeight(float localZ)
    {
        float transitionX = OceanShorelineLayout.SandMaterialTransitionX(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            localZ
        );
        return OceanShorelineLayout.SampleLandHeight(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY,
            transitionX,
            localZ
        );
    }

    public bool IsDryBeachWorldPoint(Vector3 worldPoint, float shorelineSafety = 5f)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        return OceanShorelineLayout.IsDryBeachPoint(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY,
            localPoint,
            shorelineSafety
        );
    }

    public bool IsUnderwaterWorldPoint(Vector3 worldPoint, float shorelineSafety = 3f)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        return OceanShorelineLayout.IsUnderwaterPoint(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            localPoint,
            shorelineSafety
        );
    }

    public bool IsInsideBeachShowcaseWorldPoint(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        return OceanShorelineLayout.IsInsideShowcaseZone(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY,
            localPoint
        );
    }

    public void GetDroneInterestPoints(out Vector3 showcaseCenter, out Vector3 waterInterest)
    {
        showcaseCenter = transform.TransformPoint(OceanShorelineLayout.ShowcaseCenter(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY
        ));
        waterInterest = transform.TransformPoint(OceanShorelineLayout.WaterInterestPoint(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            waterSurfaceY
        ));
    }

    public void EvaluateShorelineWorldPoint(
        Vector3 worldPoint,
        out Vector3 localPoint,
        out float shorelineX,
        out float sandSurfaceY,
        out bool dry)
    {
        localPoint = transform.InverseTransformPoint(worldPoint);
        shorelineX = OceanShorelineLayout.SandMaterialTransitionX(oceanSize, activeAreaSize, shorelineWidth, decorationSeed, localPoint.z);
        sandSurfaceY = OceanShorelineLayout.SampleLandHeight(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY,
            localPoint.x,
            localPoint.z
        );
        dry = OceanShorelineLayout.IsDryBeachPoint(
            oceanSize,
            activeAreaSize,
            shorelineWidth,
            decorationSeed,
            shorelineSurfaceY,
            localPoint,
            5f
        );
    }

    public bool TryGetGeneratedBounds(string objectName, out Bounds bounds)
    {
        bounds = default;
        Transform root = transform.Find(GeneratedRootName);
        Transform target = root != null ? root.Find(objectName) : null;
        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
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
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    public bool HasComplementaryShorelineGeometry(float tolerance = 0.02f)
    {
        if (!createVisibleShoreline
            || shorelineWaterInset > tolerance
            || waterSurfaceY < shorelineSurfaceY + 0.03f)
        {
            return false;
        }

        for (int i = 0; i <= 16; i++)
        {
            float z = Mathf.Lerp(-oceanSize.y * 0.48f, oceanSize.y * 0.48f, i / 16f);
            float shoreX = OceanShorelineLayout.SandMaterialTransitionX(oceanSize, activeAreaSize, shorelineWidth, decorationSeed, z);
            float waterEdgeX = WaterSurfaceEdgeX(z);
            float sandEdgeY = SampleShorelineLandHeight(shoreX, z);
            if (Mathf.Abs(shoreX - waterEdgeX) > tolerance || sandEdgeY > waterSurfaceY + tolerance)
            {
                return false;
            }
        }

        return true;
    }

    public float SampleSeabedHeight(float x, float z)
    {
        OceanSeabedTerrain terrain = EnsureSeabedTerrain();
        return terrain != null ? terrain.SampleHeight(x, z) : OceanSeabedTerrain.SampleBaseHeight(x, z, seabedY);
    }

    public bool TryGetFeaturePoint(OceanFeatureKind kind, out Vector3 point)
    {
        OceanSeabedTerrain terrain = EnsureSeabedTerrain();
        if (terrain != null && terrain.TryGetFeaturePoint(kind, out Vector3 localPoint))
        {
            point = transform.TransformPoint(localPoint);
            return true;
        }

        point = transform.TransformPoint(new Vector3(0f, waterSurfaceY, 0f));
        return false;
    }

    private OceanSeabedTerrain EnsureSeabedTerrain()
    {
        if (seabedTerrain == null)
        {
            seabedTerrain = OceanSeabedTerrain.Create(oceanSize, activeAreaSize, shorelineWidth, seabedY, waterSurfaceY, shorelineSurfaceY, decorationSeed, seabedRelief, basinCount, reefMoundCount);
        }

        return seabedTerrain;
    }

    private Vector3 RandomSeabedPosition(float inset)
    {
        return RandomSeabedPosition(inset, false);
    }

    private Vector3 RandomSeabedPosition(float inset, bool preferReef)
    {
        OceanSeabedTerrain terrain = EnsureSeabedTerrain();
        bool seekReef = preferReef && terrain != null && Random.value < reefDecorationBias;
        int attempts = seekReef ? 28 : 20;
        Vector3 fallback = Vector3.zero;

        for (int i = 0; i < attempts; i++)
        {
            Vector2 sampleSize = ActiveDecorationSampleSize(inset);
            float x = Random.Range(-sampleSize.x * 0.5f, sampleSize.x * 0.5f);
            float z = Random.Range(-sampleSize.y * 0.5f, sampleSize.y * 0.5f);
            Vector3 position = SampleSeabedPosition(x, z);
            if (i == 0)
            {
                fallback = position;
            }

            if (!OceanShorelineLayout.IsUnderwaterPoint(oceanSize, activeAreaSize, shorelineWidth, decorationSeed, position, 4f))
            {
                continue;
            }

            if (!seekReef || terrain.IsReefMound(x, z))
            {
                return position;
            }
        }

        return fallback;
    }

    private Vector3 SampleSeabedPosition(float x, float z)
    {
        return new Vector3(x, SampleSeabedHeight(x, z), z);
    }

    private Vector2 ActiveDecorationSampleSize(float inset)
    {
        float safeInset = Mathf.Max(0.05f, inset);
        return new Vector2(
            Mathf.Min(oceanSize.x * safeInset, activeAreaSize.x + activeDecorationPadding * 2f),
            Mathf.Min(oceanSize.y * safeInset, activeAreaSize.y + activeDecorationPadding * 2f)
        );
    }
}
