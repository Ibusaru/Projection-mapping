using UnityEngine;

public partial class OceanEnvironment
{
    private void CreateDecorations()
    {
        CreateRockMountainAccents();

        for (int i = 0; i < rockCount; i++)
        {
            Vector3 position = RandomSeabedPosition(0.9f);
            Vector3 scale = new Vector3(Random.Range(0.85f, 2.8f), Random.Range(0.36f, 1.15f), Random.Range(0.65f, 2.1f));
            if (TryCreatePandazoleRock(position, scale, "Pandazole Seabed Rock"))
            {
                continue;
            }

            GameObject rock = GeneratedPrimitiveFactory.Create(PrimitiveType.Sphere, "Seabed Rock", rockMaterial);
            rock.transform.SetParent(generatedRoot, false);
            rock.transform.position = position;
            rock.transform.localScale = scale;
            rock.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
        }

        for (int i = 0; i < simpleCoralCount; i++)
        {
            Vector3 position = RandomSeabedPosition(0.9f, true);
            Vector3 scale = Vector3.one * Random.Range(0.58f, 1.22f);
            if (TryCreatePandazoleCoral(position + Vector3.up * 0.04f, scale, "Pandazole Reef Coral"))
            {
                continue;
            }

            GameObject coral = GeneratedPrimitiveFactory.Create(PrimitiveType.Cylinder, "Simple Coral", coralMaterial);
            coral.transform.SetParent(generatedRoot, false);
            coral.transform.position = position + Vector3.up * 0.22f;
            coral.transform.localScale = new Vector3(Random.Range(0.08f, 0.22f), Random.Range(0.38f, 1.15f), Random.Range(0.08f, 0.22f));
            coral.transform.rotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f));
        }

        for (int i = 0; i < branchCoralCount; i++)
        {
            CreateBranchCoralCluster(RandomSeabedPosition(0.86f, true), i);
        }

        CreateOuterAreaFill();
        CreateShorelineAccents();
        CreatePandazoleBoneAccents();
    }

    private void CreateOuterAreaFill()
    {
        for (int i = 0; i < outerRockCount; i++)
        {
            Vector3 position = RandomOuterSeabedPosition(0.96f, false);
            Vector3 scale = new Vector3(Random.Range(0.72f, 2.35f), Random.Range(0.28f, 1.05f), Random.Range(0.58f, 1.9f));
            if (TryCreatePandazoleRock(position + Vector3.up * Random.Range(0.02f, 0.12f), scale, "Pandazole Outer Seabed Rock"))
            {
                continue;
            }

            GameObject rock = GeneratedPrimitiveFactory.Create(PrimitiveType.Sphere, "Outer Seabed Rock", rockMaterial);
            rock.transform.SetParent(generatedRoot, false);
            rock.transform.position = position + Vector3.up * Random.Range(0.04f, 0.18f);
            rock.transform.localScale = scale;
            rock.transform.rotation = Quaternion.Euler(Random.Range(-9f, 9f), Random.Range(0f, 360f), Random.Range(-9f, 9f));
        }

        for (int i = 0; i < outerCoralCount; i++)
        {
            Vector3 position = RandomOuterSeabedPosition(0.94f, true);
            if (position.y > waterSurfaceY - 0.6f)
            {
                continue;
            }

            Vector3 scale = Vector3.one * Random.Range(0.42f, 1.04f);
            if (TryCreatePandazoleCoral(position + Vector3.up * 0.04f, scale, "Pandazole Outer Reef Coral"))
            {
                continue;
            }

            if (i % 3 == 0)
            {
                CreateBranchCoralCluster(position, branchCoralCount + i);
                continue;
            }

            GameObject coral = GeneratedPrimitiveFactory.Create(PrimitiveType.Cylinder, "Outer Simple Coral", coralMaterial);
            coral.transform.SetParent(generatedRoot, false);
            coral.transform.position = position + Vector3.up * 0.18f;
            coral.transform.localScale = new Vector3(Random.Range(0.06f, 0.18f), Random.Range(0.26f, 0.88f), Random.Range(0.06f, 0.18f));
            coral.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
        }
    }

    private void CreateShorelineAccents()
    {
        if (!createVisibleShoreline || shoreAccentCount <= 0)
        {
            return;
        }

        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;
        float activeHalfX = Mathf.Min(Mathf.Abs(activeAreaSize.x) * 0.5f, Mathf.Max(1f, halfX - 3f));
        float usableWidth = Mathf.Clamp(shorelineWidth, 3f, Mathf.Max(3f, halfX - activeHalfX - 2f));

        for (int i = 0; i < shoreAccentCount; i++)
        {
            float z = Random.Range(-halfZ * 0.88f, halfZ * 0.88f);
            float shorelineX = ShorelineStartX(z, halfX, halfZ, activeHalfX, usableWidth);
            float x = Random.Range(shorelineX + 2f, halfX - 2f);
            Vector3 position = new Vector3(x, SampleShorelineLandHeight(x, z, shorelineX, halfX), z);

            if (i % 3 == 0)
            {
                Vector3 scale = new Vector3(Random.Range(0.45f, 1.25f), Random.Range(0.18f, 0.52f), Random.Range(0.38f, 1.05f));
                if (TryCreatePandazoleRock(position + Vector3.up * 0.05f, scale, "Pandazole Shore Rock"))
                {
                    continue;
                }

                GameObject shoreRock = GeneratedPrimitiveFactory.Create(PrimitiveType.Sphere, "Shore Rock", rockMaterial);
                shoreRock.transform.SetParent(generatedRoot, false);
                shoreRock.transform.position = position + Vector3.up * 0.08f;
                shoreRock.transform.localScale = scale;
                shoreRock.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
                continue;
            }

            CreateShoreGrassTuft(position, i);
        }
    }

    private void CreateShoreGrassTuft(Vector3 position, int index)
    {
        GameObject tuft = new GameObject("Shore Grass Tuft");
        tuft.transform.SetParent(generatedRoot, false);
        tuft.transform.position = position + Vector3.up * 0.03f;
        tuft.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        int bladeCount = 3 + index % 3;
        for (int i = 0; i < bladeCount; i++)
        {
            float angle = i * Mathf.PI * 2f / bladeCount + Random.Range(-0.18f, 0.18f);
            float radius = Random.Range(0.03f, 0.14f);
            float height = Random.Range(0.22f, 0.62f);
            GameObject blade = GeneratedPrimitiveFactory.Create(PrimitiveType.Cylinder, "Shore Grass Blade", shorePlantMaterial);
            blade.transform.SetParent(tuft.transform, false);
            blade.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius);
            blade.transform.localRotation = Quaternion.Euler(Random.Range(7f, 18f), angle * Mathf.Rad2Deg, Random.Range(-8f, 8f));
            blade.transform.localScale = new Vector3(Random.Range(0.018f, 0.038f), height * 0.5f, Random.Range(0.018f, 0.038f));
        }
    }

    private Vector3 RandomOuterSeabedPosition(float inset, bool preferReef)
    {
        OceanSeabedTerrain terrain = EnsureSeabedTerrain();
        Vector3 fallback = RandomSeabedPosition(inset, preferReef);
        for (int i = 0; i < 24; i++)
        {
            float x = Random.Range(-oceanSize.x * 0.5f * inset, oceanSize.x * 0.5f * inset);
            float z = Random.Range(-oceanSize.y * 0.5f * inset, oceanSize.y * 0.5f * inset);
            if (!IsOutsideActiveArea(x, z, 2.5f))
            {
                continue;
            }

            if (preferReef && terrain != null && Random.value < reefDecorationBias && !terrain.IsReefMound(x, z))
            {
                fallback = SampleSeabedPosition(x, z);
                continue;
            }

            return SampleSeabedPosition(x, z);
        }

        return fallback;
    }

    private bool IsOutsideActiveArea(float x, float z, float padding)
    {
        Vector2 halfActive = new Vector2(
            Mathf.Min(Mathf.Abs(activeAreaSize.x) * 0.5f, oceanSize.x * 0.48f),
            Mathf.Min(Mathf.Abs(activeAreaSize.y) * 0.5f, oceanSize.y * 0.48f)
        );

        return Mathf.Abs(x) > halfActive.x + padding || Mathf.Abs(z) > halfActive.y + padding;
    }

    private void CreateRockMountainAccents()
    {
        if (!TryGetFeaturePoint(OceanFeatureKind.RockMountain, out Vector3 peak))
        {
            return;
        }

        Vector3 center = transform.InverseTransformPoint(peak);
        for (int i = 0; i < 34; i++)
        {
            float angle = i * Mathf.PI * 2f / 34f + Random.Range(-0.28f, 0.28f);
            float radius = Random.Range(2.8f, 13.5f);
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            Vector3 position = SampleSeabedPosition(x, z);
            Vector3 scale = new Vector3(Random.Range(1.05f, 2.7f), Random.Range(0.72f, 1.95f), Random.Range(0.95f, 2.45f));
            if (TryCreatePandazoleRock(position + Vector3.up * Random.Range(0.05f, 0.22f), scale, "Pandazole Rocky Field Boulder"))
            {
                continue;
            }

            GameObject spire = GeneratedPrimitiveFactory.Create(PrimitiveType.Capsule, "Rocky Field Boulder", rockMaterial);
            spire.transform.SetParent(generatedRoot, false);
            spire.transform.position = position + Vector3.up * Random.Range(0.22f, 0.78f);
            spire.transform.localScale = new Vector3(Random.Range(0.72f, 1.55f), Random.Range(0.9f, 2.45f), Random.Range(0.72f, 1.55f));
            spire.transform.rotation = Quaternion.Euler(Random.Range(-16f, 16f), Random.Range(0f, 360f), Random.Range(-16f, 16f));
        }
    }

    private void CreateBranchCoralCluster(Vector3 position, int index)
    {
        Vector3 surfaceNormal = SampleSeabedNormal(position);
        GameObject cluster = new GameObject("Branch Coral Cluster");
        cluster.transform.SetParent(generatedRoot, false);
        cluster.transform.position = position - surfaceNormal * 0.025f;
        cluster.transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), surfaceNormal)
            * Quaternion.FromToRotation(Vector3.up, surfaceNormal);

        Material material = index % 3 == 0 ? coralMaterial : whiteCoralMaterial;
        int branchCount = Random.Range(8, 15);
        float baseYaw = Random.Range(0f, 360f);

        for (int i = 0; i < branchCount; i++)
        {
            float angle = baseYaw + i * (360f / branchCount) + Random.Range(-18f, 18f);
            float angleRadians = angle * Mathf.Deg2Rad;
            float spread = Random.Range(0.38f, 0.86f);
            Vector3 direction = new Vector3(
                Mathf.Cos(angleRadians) * spread,
                Random.Range(0.58f, 1.04f),
                Mathf.Sin(angleRadians) * spread
            ).normalized;
            float length = Random.Range(0.46f, 1.28f);
            float radius = Random.Range(0.025f, 0.06f);
            AddCoralBranch(cluster.transform, Vector3.zero, direction, length, radius, material);

            if (Random.value > 0.3f)
            {
                Vector3 forkStart = direction.normalized * length * Random.Range(0.45f, 0.72f);
                Vector3 forkDirection = Quaternion.Euler(Random.Range(-18f, 24f), Random.Range(-42f, 42f), Random.Range(-22f, 22f)) * direction;
                AddCoralBranch(cluster.transform, forkStart, forkDirection, length * Random.Range(0.38f, 0.62f), radius * 0.72f, material);
            }
        }

        CreateCoralRootPebbles(cluster.transform, material);
    }

    private void AddCoralBranch(Transform parent, Vector3 start, Vector3 direction, float length, float radius, Material material)
    {
        GameObject branch = GeneratedPrimitiveFactory.Create(PrimitiveType.Cylinder, "Coral Branch", material);
        branch.transform.SetParent(parent, false);

        Vector3 normalized = direction.normalized;
        branch.transform.localPosition = start + normalized * length * 0.5f;
        branch.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normalized);
        branch.transform.localScale = new Vector3(radius, length * 0.5f, radius);
    }

    private void CreateCoralRootPebbles(Transform parent, Material coralRootMaterial)
    {
        Material material = rockMaterial != null ? rockMaterial : coralRootMaterial;
        int pebbleCount = Random.Range(3, 6);
        for (int i = 0; i < pebbleCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(0.02f, 0.16f);
            GameObject pebble = GeneratedPrimitiveFactory.Create(PrimitiveType.Sphere, "Coral Root Pebble", material);
            pebble.transform.SetParent(parent, false);
            pebble.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                Random.Range(-0.035f, -0.008f),
                Mathf.Sin(angle) * radius
            );
            pebble.transform.localScale = new Vector3(
                Random.Range(0.07f, 0.18f),
                Random.Range(0.025f, 0.06f),
                Random.Range(0.07f, 0.18f)
            );
            pebble.transform.localRotation = Quaternion.Euler(
                Random.Range(-10f, 10f),
                Random.Range(0f, 360f),
                Random.Range(-10f, 10f)
            );
        }
    }

    private Vector3 SampleSeabedNormal(Vector3 localPosition)
    {
        const float step = 0.9f;
        Vector3 left = SampleSeabedPosition(localPosition.x - step, localPosition.z);
        Vector3 right = SampleSeabedPosition(localPosition.x + step, localPosition.z);
        Vector3 back = SampleSeabedPosition(localPosition.x, localPosition.z - step);
        Vector3 forward = SampleSeabedPosition(localPosition.x, localPosition.z + step);
        Vector3 tangentX = right - left;
        Vector3 tangentZ = forward - back;
        Vector3 normal = Vector3.Cross(tangentZ, tangentX);
        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
    }

    private void CreateBubbleColumns()
    {
        for (int i = 0; i < bubbleColumnCount; i++)
        {
            OceanFeatureKind feature = i % 2 == 0 ? OceanFeatureKind.Trench : OceanFeatureKind.Reef;
            Vector3 basePosition = TryGetFeaturePoint(feature, out Vector3 featurePoint)
                ? transform.InverseTransformPoint(featurePoint)
                : RandomSeabedPosition(0.65f);
            basePosition += new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));

            GameObject column = new GameObject("Bubble Column");
            column.transform.SetParent(generatedRoot, false);
            column.transform.position = SampleSeabedPosition(basePosition.x, basePosition.z) + Vector3.up * 0.8f;

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
}
