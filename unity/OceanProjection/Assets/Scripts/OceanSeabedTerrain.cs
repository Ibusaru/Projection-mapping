using UnityEngine;

internal sealed class OceanSeabedTerrain
{
    private static readonly Vector2[] BasinAnchors =
    {
        new Vector2(-0.18f, -0.1f),
        new Vector2(0.22f, 0.24f),
        new Vector2(0.02f, -0.44f),
        new Vector2(-0.32f, 0.34f),
        new Vector2(0.54f, -0.28f),
        new Vector2(-0.62f, 0.26f)
    };

    private static readonly Vector2[] ReefAnchors =
    {
        new Vector2(0.06f, 0.14f),
        new Vector2(0.35f, -0.04f),
        new Vector2(0.26f, 0.27f),
        new Vector2(-0.12f, 0.3f),
        new Vector2(0.5f, 0.34f),
        new Vector2(-0.04f, -0.26f),
        new Vector2(0.58f, -0.24f)
    };

    private readonly Vector2 oceanSize;
    private readonly float seabedY;
    private readonly float waterSurfaceY;
    private readonly float relief;
    private readonly float noiseOffsetX;
    private readonly float noiseOffsetZ;
    private readonly Vector2 rockMountainCenter;
    private readonly Vector2 reefCenter;
    private readonly Vector2 beachCenter;
    private readonly Vector2 trenchCenter;
    private readonly TerrainFeature[] basins;
    private readonly TerrainFeature[] reefMounds;

    private OceanSeabedTerrain(
        Vector2 oceanSize,
        float seabedY,
        float waterSurfaceY,
        float relief,
        float noiseOffsetX,
        float noiseOffsetZ,
        Vector2 rockMountainCenter,
        Vector2 reefCenter,
        Vector2 beachCenter,
        Vector2 trenchCenter,
        TerrainFeature[] basins,
        TerrainFeature[] reefMounds)
    {
        this.oceanSize = oceanSize;
        this.seabedY = seabedY;
        this.waterSurfaceY = waterSurfaceY;
        this.relief = Mathf.Max(0f, relief);
        this.noiseOffsetX = noiseOffsetX;
        this.noiseOffsetZ = noiseOffsetZ;
        this.rockMountainCenter = rockMountainCenter;
        this.reefCenter = reefCenter;
        this.beachCenter = beachCenter;
        this.trenchCenter = trenchCenter;
        this.basins = basins;
        this.reefMounds = reefMounds;
    }

    public static OceanSeabedTerrain Create(
        Vector2 oceanSize,
        float seabedY,
        float waterSurfaceY,
        int seed,
        float relief,
        int basinCount,
        int reefMoundCount)
    {
        System.Random random = new System.Random(seed);
        Vector2 safeSize = new Vector2(Mathf.Max(12f, oceanSize.x), Mathf.Max(12f, oceanSize.y));
        float noiseOffsetX = Range(random, -1000f, 1000f);
        float noiseOffsetZ = Range(random, -1000f, 1000f);
        Vector2 rockMountainCenter = new Vector2(-safeSize.x * 0.31f, -safeSize.y * 0.17f);
        Vector2 reefCenter = new Vector2(safeSize.x * 0.18f, safeSize.y * 0.13f);
        Vector2 beachCenter = new Vector2(safeSize.x * 0.38f, -safeSize.y * 0.16f);
        Vector2 trenchCenter = new Vector2(-safeSize.x * 0.04f, safeSize.y * 0.02f);
        TerrainFeature[] basins = BuildFeatures(safeSize, random, BasinAnchors, basinCount, relief, false);
        TerrainFeature[] reefMounds = BuildFeatures(safeSize, random, ReefAnchors, reefMoundCount, relief, true);

        return new OceanSeabedTerrain(
            safeSize,
            seabedY,
            waterSurfaceY,
            relief,
            noiseOffsetX,
            noiseOffsetZ,
            rockMountainCenter,
            reefCenter,
            beachCenter,
            trenchCenter,
            basins,
            reefMounds
        );
    }

    public static float SampleBaseHeight(float x, float z, float seabedY)
    {
        return seabedY + Mathf.Sin(x * 0.8f + z * 0.35f) * 0.18f + Mathf.Cos(z * 0.9f) * 0.11f;
    }

    public Vector3 SamplePosition(float x, float z)
    {
        return new Vector3(x, SampleHeight(x, z), z);
    }

    public float SampleHeight(float x, float z)
    {
        float height = seabedY + SampleSandRipple(x, z);
        height += SampleBroadOceanTilt(x, z);

        float shapedRelief = 0f;
        for (int i = 0; i < basins.Length; i++)
        {
            shapedRelief += basins[i].Sample(x, z);
        }

        for (int i = 0; i < reefMounds.Length; i++)
        {
            shapedRelief += reefMounds[i].Sample(x, z);
        }

        float maximumRelief = Mathf.Max(0.2f, relief);
        height += Mathf.Clamp(shapedRelief, -maximumRelief * 1.9f, maximumRelief * 1.85f);
        height += SampleBarrierReef(x, z);
        height += SampleBeachShelf(x, z);
        height += SampleTrench(x, z);
        height += SampleRockMountain(x, z);
        return height;
    }

    public bool IsReefMound(float x, float z)
    {
        if (EllipticalDistance(new Vector2(x, z), reefCenter, new Vector2(oceanSize.x * 0.24f, oceanSize.y * 0.18f), -18f) <= 1.08f)
        {
            return true;
        }

        for (int i = 0; i < reefMounds.Length; i++)
        {
            if (reefMounds[i].Contains(x, z, 0.98f))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetFeaturePoint(OceanFeatureKind kind, out Vector3 point)
    {
        switch (kind)
        {
            case OceanFeatureKind.Overview:
                point = new Vector3(0f, waterSurfaceY + Mathf.Max(oceanSize.x, oceanSize.y) * 0.22f, 0f);
                return true;
            case OceanFeatureKind.Beach:
                point = SamplePosition(beachCenter.x, beachCenter.y);
                return true;
            case OceanFeatureKind.Reef:
                point = SamplePosition(reefCenter.x, reefCenter.y);
                return true;
            case OceanFeatureKind.Trench:
                point = SamplePosition(trenchCenter.x, trenchCenter.y);
                return true;
            case OceanFeatureKind.RockMountain:
                point = SamplePosition(rockMountainCenter.x, rockMountainCenter.y);
                return true;
            default:
                point = Vector3.zero;
                return false;
        }
    }

    private float SampleBroadOceanTilt(float x, float z)
    {
        float west = Mathf.InverseLerp(oceanSize.x * 0.42f, -oceanSize.x * 0.46f, x);
        float south = Mathf.InverseLerp(oceanSize.y * 0.42f, -oceanSize.y * 0.48f, z);
        return -SmootherStep(west) * 1.35f - SmootherStep(south) * 0.55f;
    }

    private float SampleBeachShelf(float x, float z)
    {
        float wave = Mathf.Sin(z * 0.055f + noiseOffsetX * 0.01f) * 2.8f + Mathf.Sin(z * 0.13f) * 1.1f;
        float shorelineX = oceanSize.x * 0.26f + wave;
        float shelf = SmootherStep(Mathf.InverseLerp(shorelineX - oceanSize.x * 0.33f, shorelineX + oceanSize.x * 0.08f, x));
        float drySand = SmootherStep(Mathf.InverseLerp(shorelineX + oceanSize.x * 0.06f, shorelineX + oceanSize.x * 0.22f, x));
        float lagoonCut = Mathf.Sin((z - beachCenter.y) * 0.11f) * 0.28f + Mathf.PerlinNoise(x * 0.025f + 12f, z * 0.025f) * 0.35f;
        float shallowTargetLift = (waterSurfaceY - 0.9f - seabedY) * shelf;
        return shallowTargetLift + drySand * 1.65f + lagoonCut * shelf;
    }

    private float SampleBarrierReef(float x, float z)
    {
        Vector2 position = new Vector2(x, z);
        float distance = EllipticalDistance(position, reefCenter, new Vector2(oceanSize.x * 0.24f, oceanSize.y * 0.18f), -18f);
        float ring = Ring(distance, 0.55f, 1.08f);
        float innerMound = 1f - SmootherStep(Mathf.Clamp01(distance / 0.72f));
        float ridgeNoise = 0.75f + Mathf.PerlinNoise(x * 0.07f + noiseOffsetX, z * 0.07f + noiseOffsetZ) * 0.5f;
        return (ring * 2.55f + innerMound * 1.55f) * ridgeNoise;
    }

    private float SampleTrench(float x, float z)
    {
        Vector2 local = Rotate(new Vector2(x - trenchCenter.x, z - trenchCenter.y), 32f);
        float halfLength = oceanSize.x * 0.46f;
        float halfWidth = oceanSize.y * 0.095f;
        float alongMask = 1f - SmootherStep(Mathf.Clamp01(Mathf.Abs(local.x) / halfLength));
        float across = Mathf.Abs(local.y) / Mathf.Max(0.001f, halfWidth);
        float core = 1f - SmootherStep(Mathf.Clamp01(across));
        float wall = Ring(across, 1.0f, 1.85f);
        float fracture = Mathf.PerlinNoise(x * 0.055f - noiseOffsetZ, z * 0.055f + noiseOffsetX) * 0.7f;
        return alongMask * (-9.6f * core - fracture * core + wall * 1.55f);
    }

    private float SampleRockMountain(float x, float z)
    {
        Vector2 position = new Vector2(x, z);
        float distance = EllipticalDistance(position, rockMountainCenter, new Vector2(oceanSize.x * 0.115f, oceanSize.y * 0.16f), 19f);
        if (distance >= 1.55f)
        {
            return 0f;
        }

        float summit = 1f - SmootherStep(Mathf.Clamp01(distance / 0.54f));
        float upperCliff = 1f - SmootherStep(Mathf.Clamp01((distance - 0.24f) / 0.72f));
        float scree = Ring(distance, 0.72f, 1.42f);
        float crags = (Mathf.PerlinNoise(x * 0.13f + noiseOffsetX, z * 0.13f + noiseOffsetZ) - 0.5f) * 1.4f * upperCliff;
        float targetLift = waterSurfaceY - seabedY + 9.2f;
        return summit * targetLift + upperCliff * 4.8f + scree * 2.2f + crags;
    }

    private float SampleSandRipple(float x, float z)
    {
        float longRipple = Mathf.Sin(x * 0.42f + z * 0.26f) * 0.18f + Mathf.Cos(z * 0.52f) * 0.1f;
        float currentRidge = Mathf.Sin((x - z) * 0.18f + noiseOffsetX * 0.04f) * 0.11f;
        float lowNoise = (Mathf.PerlinNoise(x * 0.028f + noiseOffsetX, z * 0.028f + noiseOffsetZ) - 0.5f) * 0.55f;
        float fineNoise = (Mathf.PerlinNoise(x * 0.13f - noiseOffsetZ, z * 0.13f + noiseOffsetX) - 0.5f) * 0.12f;
        return longRipple + currentRidge + lowNoise + fineNoise;
    }

    private static TerrainFeature[] BuildFeatures(
        Vector2 oceanSize,
        System.Random random,
        Vector2[] anchors,
        int count,
        float relief,
        bool reef)
    {
        int safeCount = Mathf.Max(0, count);
        TerrainFeature[] features = new TerrainFeature[safeCount];
        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;

        for (int i = 0; i < safeCount; i++)
        {
            Vector2 anchor = i < anchors.Length ? anchors[i] : SpiralAnchor(i, safeCount);
            float radiusX = oceanSize.x * Range(random, reef ? 0.045f : 0.075f, reef ? 0.095f : 0.145f);
            float radiusZ = oceanSize.y * Range(random, reef ? 0.055f : 0.075f, reef ? 0.13f : 0.15f);
            float x = anchor.x * halfX + oceanSize.x * Range(random, -0.028f, 0.028f);
            float z = anchor.y * halfZ + oceanSize.y * Range(random, -0.038f, 0.038f);
            x = Mathf.Clamp(x, -halfX + radiusX * 0.8f, halfX - radiusX * 0.8f);
            z = Mathf.Clamp(z, -halfZ + radiusZ * 0.8f, halfZ - radiusZ * 0.8f);

            float featureRelief = Mathf.Max(0f, relief) * Range(random, reef ? 0.72f : 0.86f, reef ? 1.22f : 1.38f);
            float rotation = Range(random, -48f, 48f) * Mathf.Deg2Rad;
            features[i] = new TerrainFeature(new Vector2(x, z), new Vector2(radiusX, radiusZ), rotation, featureRelief, reef);
        }

        return features;
    }

    private static Vector2 SpiralAnchor(int index, int count)
    {
        float t = (index + 0.5f) / Mathf.Max(1, count);
        float angle = t * Mathf.PI * 2f * 1.618f;
        float radius = Mathf.Lerp(0.18f, 0.72f, t);
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    private static float EllipticalDistance(Vector2 position, Vector2 center, Vector2 radius, float rotationDegrees)
    {
        Vector2 local = Rotate(position - center, rotationDegrees);
        float nx = local.x / Mathf.Max(0.001f, radius.x);
        float nz = local.y / Mathf.Max(0.001f, radius.y);
        return Mathf.Sqrt(nx * nx + nz * nz);
    }

    private static float Range(System.Random random, float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private static float SmootherStep(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static float Ring(float distance, float inner, float outer)
    {
        float center = (inner + outer) * 0.5f;
        float halfWidth = Mathf.Max(0.001f, (outer - inner) * 0.5f);
        float normalized = Mathf.Abs(distance - center) / halfWidth;
        return 1f - SmootherStep(normalized);
    }

    private readonly struct TerrainFeature
    {
        private readonly Vector2 center;
        private readonly Vector2 radius;
        private readonly float sinRotation;
        private readonly float cosRotation;
        private readonly float height;
        private readonly bool reef;

        public TerrainFeature(Vector2 center, Vector2 radius, float rotation, float height, bool reef)
        {
            this.center = center;
            this.radius = radius;
            sinRotation = Mathf.Sin(rotation);
            cosRotation = Mathf.Cos(rotation);
            this.height = height;
            this.reef = reef;
        }

        public float Sample(float x, float z)
        {
            float distance = NormalizedDistance(x, z);
            if (distance >= 1.24f)
            {
                return 0f;
            }

            if (reef)
            {
                float mound = 1f - SmootherStep(Mathf.Clamp01((distance - 0.05f) / 0.9f));
                float shoulder = Ring(distance, 0.48f, 1.18f);
                return height * (mound * 0.95f + shoulder * 0.3f);
            }

            float bowl = 1f - SmootherStep(Mathf.Clamp01(distance));
            float raisedRim = Ring(distance, 0.72f, 1.16f);
            return height * (-bowl + raisedRim * 0.24f);
        }

        public bool Contains(float x, float z, float normalizedRadius)
        {
            return NormalizedDistance(x, z) <= normalizedRadius;
        }

        private float NormalizedDistance(float x, float z)
        {
            float dx = x - center.x;
            float dz = z - center.y;
            float localX = dx * cosRotation - dz * sinRotation;
            float localZ = dx * sinRotation + dz * cosRotation;
            float nx = localX / Mathf.Max(0.001f, radius.x);
            float nz = localZ / Mathf.Max(0.001f, radius.y);
            return Mathf.Sqrt(nx * nx + nz * nz);
        }
    }
}
