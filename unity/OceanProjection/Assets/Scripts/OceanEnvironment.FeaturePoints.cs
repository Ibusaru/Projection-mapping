using UnityEngine;

public partial class OceanEnvironment
{
    public float WaterSurfaceY => waterSurfaceY;
    public Vector2 OceanSize => oceanSize;
    public Vector2 ActiveAreaSize => activeAreaSize;

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
            seabedTerrain = OceanSeabedTerrain.Create(oceanSize, seabedY, waterSurfaceY, decorationSeed, seabedRelief, basinCount, reefMoundCount);
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
        int attempts = seekReef ? 18 : 1;
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
