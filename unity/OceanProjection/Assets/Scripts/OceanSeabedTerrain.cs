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
    private readonly Vector2 activeAreaSize;
    private readonly float shorelineWidth;
    private readonly int shorelineSeed;
    private readonly float seabedY;
    private readonly float waterSurfaceY;
    private readonly float shorelineSurfaceY;
    private readonly float relief;
    private readonly float noiseOffsetX;
    private readonly float noiseOffsetZ;
    private readonly Vector2 rockMountainCenter;
    private readonly Vector2 reefCenter;
    private readonly Vector2 trenchCenter;
    private readonly TerrainFeature[] basins;
    private readonly TerrainFeature[] reefMounds;

    private OceanSeabedTerrain(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float shorelineWidth,
        int shorelineSeed,
        float seabedY,
        float waterSurfaceY,
        float shorelineSurfaceY,
        float relief,
        float noiseOffsetX,
        float noiseOffsetZ,
        Vector2 rockMountainCenter,
        Vector2 reefCenter,
        Vector2 trenchCenter,
        TerrainFeature[] basins,
        TerrainFeature[] reefMounds)
    {
        this.oceanSize = oceanSize;
        this.activeAreaSize = activeAreaSize;
        this.shorelineWidth = shorelineWidth;
        this.shorelineSeed = shorelineSeed;
        this.seabedY = seabedY;
        this.waterSurfaceY = waterSurfaceY;
        this.shorelineSurfaceY = shorelineSurfaceY;
        this.relief = Mathf.Max(0f, relief);
        this.noiseOffsetX = noiseOffsetX;
        this.noiseOffsetZ = noiseOffsetZ;
        this.rockMountainCenter = rockMountainCenter;
        this.reefCenter = reefCenter;
        this.trenchCenter = trenchCenter;
        this.basins = basins;
        this.reefMounds = reefMounds;
    }

    public static OceanSeabedTerrain Create(
        Vector2 oceanSize,
        Vector2 activeAreaSize,
        float shorelineWidth,
        float seabedY,
        float waterSurfaceY,
        float shorelineSurfaceY,
        int seed,
        float relief,
        int basinCount,
        int reefMoundCount)
    {
        System.Random random = new System.Random(seed);
        Vector2 safeSize = new Vector2(Mathf.Max(12f, oceanSize.x), Mathf.Max(12f, oceanSize.y));
        float noiseOffsetX = Range(random, -1000f, 1000f);
        float noiseOffsetZ = Range(random, -1000f, 1000f);
        Vector2 rockMountainCenter = new Vector2(-safeSize.x * 0.28f, -safeSize.y * 0.28f);
        Vector2 reefCenter = new Vector2(safeSize.x * 0.18f, safeSize.y * 0.16f);
        Vector2 trenchCenter = new Vector2(-safeSize.x * 0.32f, safeSize.y * 0.27f);
        TerrainFeature[] basins = BuildFeatures(safeSize, random, BasinAnchors, basinCount, relief, false);
        TerrainFeature[] reefMounds = BuildFeatures(safeSize, random, ReefAnchors, reefMoundCount, relief, true);

        return new OceanSeabedTerrain(
            safeSize,
            activeAreaSize,
            shorelineWidth,
            seed,
            seabedY,
            waterSurfaceY,
            shorelineSurfaceY,
            relief,
            noiseOffsetX,
            noiseOffsetZ,
            rockMountainCenter,
            reefCenter,
            trenchCenter,
            basins,
            reefMounds
        );
    }

    public static float SampleBaseHeight(float x, float z, float seabedY)
    {
        float broad = Mathf.PerlinNoise(x * 0.018f + 31.7f, z * 0.018f - 19.2f) - 0.5f;
        float fine = Mathf.PerlinNoise(x * 0.085f - 12.4f, z * 0.085f + 44.6f) - 0.5f;
        return seabedY + broad * 0.8f + fine * 0.18f;
    }

    public Vector3 SamplePosition(float x, float z)
    {
        return new Vector3(x, SampleHeight(x, z), z);
    }

    public float SampleHeight(float x, float z)
    {
        float shorelineX = OceanShorelineLayout.StartX(oceanSize, activeAreaSize, shorelineWidth, shorelineSeed, z);
        if (x >= shorelineX)
        {
            // The dry-sand renderer is only a material overlay. Keep the actual
            // seabed directly beneath it so the beach and grounded props read
            // as solid terrain when viewed from below the water surface.
            const float beachSurfaceSupportGap = 0.035f;
            return OceanShorelineLayout.SampleLandHeight(
                oceanSize,
                activeAreaSize,
                shorelineWidth,
                shorelineSeed,
                shorelineSurfaceY,
                x,
                z
            ) - beachSurfaceSupportGap;
        }

        float offshoreDistance = Mathf.Max(0f, shorelineX - x);
        float offshoreRelief = SmootherStep(Mathf.InverseLerp(28f, 145f, offshoreDistance));
        float shallowBlend = 1f - SmootherStep(Mathf.InverseLerp(20f, 138f, offshoreDistance));

        float height = seabedY + SampleSandRipple(x, z) * Mathf.Lerp(0.08f, 1f, offshoreRelief);
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
        height += Mathf.Clamp(shapedRelief, -maximumRelief * 1.55f, maximumRelief * 1.45f) * offshoreRelief;
        height += SampleBarrierReef(x, z) * offshoreRelief;
        height += SampleTrench(x, z) * offshoreRelief;
        float rockyLift = SampleRockyField(x, z) * offshoreRelief;
        if (rockyLift > 0f)
        {
            height += Mathf.Max(0f, Mathf.Min(rockyLift, waterSurfaceY - 1.35f - height));
        }

        // The near-shore shelf is a target profile, not an additive lift.  This
        // keeps reef/basin noise from punching irregular sand islands through a
        // transparent water surface.
        float shelfProgress = SmootherStep(Mathf.InverseLerp(0f, 138f, offshoreDistance));
        // Meet the supported beach datum instead of leaving the old 1.35 m
        // vertical void beneath the sand overlay.
        float shelfTarget = Mathf.Lerp(shorelineSurfaceY - 0.035f, shorelineSurfaceY - 7.4f, shelfProgress);
        height = Mathf.Lerp(height, shelfTarget, shallowBlend);

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
                point = OceanShorelineLayout.ShowcaseCenter(
                    oceanSize,
                    activeAreaSize,
                    shorelineWidth,
                    shorelineSeed,
                    shorelineSurfaceY
                );
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
        float west = SmootherStep(Mathf.InverseLerp(oceanSize.x * 0.22f, -oceanSize.x * 0.5f, x));
        float south = SmootherStep(Mathf.InverseLerp(oceanSize.y * 0.34f, -oceanSize.y * 0.5f, z));
        float basinDrift = FbmSigned(x, z, 0.0085f, 4, 2.04f, 0.5f) * 0.65f;
        return -west * 0.95f - south * 0.48f + basinDrift;
    }

    private float SampleBarrierReef(float x, float z)
    {
        Vector2 position = DomainWarp(x, z, 0.016f, 3.8f);
        float distance = EllipticalDistance(position, reefCenter, new Vector2(oceanSize.x * 0.24f, oceanSize.y * 0.18f), -18f);
        distance += FbmSigned(x, z, 0.04f, 3, 2.15f, 0.5f) * 0.14f;
        float ring = Ring(distance, 0.5f, 1.05f);
        float innerMound = 1f - SmootherStep(Mathf.Clamp01(distance / 0.76f));
        float brokenEdges = Mathf.Lerp(0.55f, 1.12f, Mathf.Clamp01(FbmSigned(x, z, 0.03f, 4, 2.03f, 0.54f) * 0.5f + 0.5f));
        float ridgeTexture = 0.82f + RidgedFbm(x, z, 0.07f, 3, 2.1f, 0.48f) * 0.42f;
        return (ring * 2.18f * brokenEdges + innerMound * 1.22f) * ridgeTexture;
    }

    private float SampleTrench(float x, float z)
    {
        Vector2 local = Rotate(new Vector2(x - trenchCenter.x, z - trenchCenter.y), 32f);
        local.y += FbmSigned(x, z, 0.013f, 4, 2.08f, 0.52f) * oceanSize.y * 0.052f;
        float halfLength = oceanSize.x * 0.38f;
        float halfWidth = oceanSize.y * 0.13f;
        float alongMask = 1f - SmootherStep(Mathf.Clamp01(Mathf.Abs(local.x) / halfLength));
        float across = Mathf.Abs(local.y) / Mathf.Max(0.001f, halfWidth);
        float core = 1f - SmootherStep(Mathf.Clamp01((across - 0.04f) / 1.22f));
        float wall = Ring(across, 1.18f, 2.35f);
        float fracture = RidgedFbm(x, z, 0.052f, 4, 2.05f, 0.5f) * 1.05f;
        float shorelineX = OceanShorelineLayout.StartX(oceanSize, activeAreaSize, shorelineWidth, shorelineSeed, z);
        float beachGap = SmootherStep(Mathf.InverseLerp(80f, 180f, shorelineX - x));
        return alongMask * beachGap * (-6.25f * core - fracture * core * 0.52f + wall * 0.42f);
    }

    private float SampleRockyField(float x, float z)
    {
        Vector2 position = DomainWarp(x, z, 0.014f, 3.2f);
        float distance = EllipticalDistance(position, rockMountainCenter, new Vector2(oceanSize.x * 0.22f, oceanSize.y * 0.18f), -12f);
        distance += FbmSigned(x, z, 0.04f, 3, 2.12f, 0.48f) * 0.14f;
        if (distance >= 1.62f)
        {
            return 0f;
        }

        float field = 1f - SmootherStep(Mathf.Clamp01((distance - 0.12f) / 1.32f));
        float core = 1f - SmootherStep(Mathf.Clamp01(distance / 0.78f));
        float brokenRim = Ring(distance, 0.58f, 1.46f);
        float ridges = RidgedFbm(x, z, 0.082f, 4, 2.05f, 0.52f);
        float boulders = RidgedFbm(x + 17.3f, z - 9.1f, 0.15f, 3, 2.15f, 0.46f);
        return field * 1.15f + core * 2.55f + brokenRim * 0.86f + ridges * field * 1.35f + boulders * field * 0.62f;
    }

    private float SampleSandRipple(float x, float z)
    {
        Vector2 warped = DomainWarp(x, z, 0.017f, 7.5f);
        float broad = FbmSigned(warped.x, warped.y, 0.012f, 5, 2.02f, 0.54f) * relief * 0.72f;
        float medium = FbmSigned(warped.x, warped.y, 0.034f, 4, 2.1f, 0.5f) * relief * 0.26f;
        float ridges = (RidgedFbm(x, z, 0.058f, 3, 2.15f, 0.48f) - 0.48f) * 0.72f;
        float fine = FbmSigned(x, z, 0.13f, 2, 2.4f, 0.42f) * 0.12f;
        return broad + medium + ridges + fine;
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
            float radiusX = oceanSize.x * Range(random, reef ? 0.045f : 0.11f, reef ? 0.095f : 0.2f);
            float radiusZ = oceanSize.y * Range(random, reef ? 0.055f : 0.1f, reef ? 0.13f : 0.19f);
            float x = anchor.x * halfX + oceanSize.x * Range(random, -0.028f, 0.028f);
            float z = anchor.y * halfZ + oceanSize.y * Range(random, -0.038f, 0.038f);
            x = Mathf.Clamp(x, -halfX + radiusX * 0.8f, halfX - radiusX * 0.8f);
            z = Mathf.Clamp(z, -halfZ + radiusZ * 0.8f, halfZ - radiusZ * 0.8f);

            float featureRelief = Mathf.Max(0f, relief) * Range(random, reef ? 0.72f : 0.66f, reef ? 1.22f : 1.05f);
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

    private Vector2 DomainWarp(float x, float z, float scale, float amount)
    {
        float warpX = FbmSigned(x + noiseOffsetX, z - noiseOffsetZ, scale, 3, 2.04f, 0.52f);
        float warpZ = FbmSigned(x - noiseOffsetZ, z + noiseOffsetX, scale, 3, 2.08f, 0.5f);
        return new Vector2(x + warpX * amount, z + warpZ * amount);
    }

    private float FbmSigned(float x, float z, float scale, int octaves, float lacunarity, float gain)
    {
        float amplitude = 1f;
        float frequency = Mathf.Max(0.0001f, scale);
        float sum = 0f;
        float normalizer = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sample = Mathf.PerlinNoise(x * frequency + noiseOffsetX + i * 37.17f, z * frequency + noiseOffsetZ - i * 19.63f);
            sum += (sample * 2f - 1f) * amplitude;
            normalizer += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return normalizer > 0f ? sum / normalizer : 0f;
    }

    private float RidgedFbm(float x, float z, float scale, int octaves, float lacunarity, float gain)
    {
        float amplitude = 1f;
        float frequency = Mathf.Max(0.0001f, scale);
        float sum = 0f;
        float normalizer = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sample = Mathf.PerlinNoise(x * frequency - noiseOffsetZ + i * 13.31f, z * frequency + noiseOffsetX + i * 29.73f);
            float ridge = 1f - Mathf.Abs(sample * 2f - 1f);
            sum += ridge * amplitude;
            normalizer += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return normalizer > 0f ? sum / normalizer : 0f;
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
            distance += (Mathf.PerlinNoise(x * 0.037f + center.x * 0.02f, z * 0.037f - center.y * 0.02f) - 0.5f) * 0.18f;
            if (distance >= (reef ? 1.24f : 1.55f))
            {
                return 0f;
            }

            if (reef)
            {
                float mound = 1f - SmootherStep(Mathf.Clamp01((distance - 0.08f) / 0.92f));
                float shoulder = Ring(distance, 0.5f, 1.16f);
                float chip = Mathf.Lerp(0.72f, 1.08f, Mathf.PerlinNoise(x * 0.082f + 8.3f, z * 0.082f - 4.7f));
                return height * (mound * 0.78f + shoulder * 0.28f) * chip;
            }

            float innerBowl = 1f - SmootherStep(Mathf.Clamp01(distance / 1.08f));
            float softShelf = 1f - SmootherStep(Mathf.Clamp01((distance - 0.38f) / 1.12f));
            float raisedRim = Ring(distance, 1.02f, 1.48f);
            return height * (-innerBowl * 0.46f - softShelf * 0.3f + raisedRim * 0.06f);
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
