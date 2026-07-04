using UnityEngine;

public partial class OceanEnvironment
{
    private void CreateDecorations()
    {
        CreateRockMountainAccents();

        for (int i = 0; i < rockCount; i++)
        {
            Vector3 position = RandomSeabedPosition(0.9f);
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Seabed Rock";
            rock.transform.SetParent(generatedRoot, false);
            rock.transform.position = position;
            rock.transform.localScale = new Vector3(Random.Range(0.85f, 2.8f), Random.Range(0.24f, 0.9f), Random.Range(0.65f, 2.1f));
            rock.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            rock.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
            DestroyCollider(rock);
        }

        for (int i = 0; i < simpleCoralCount; i++)
        {
            Vector3 position = RandomSeabedPosition(0.9f, true);
            GameObject coral = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coral.name = "Simple Coral";
            coral.transform.SetParent(generatedRoot, false);
            coral.transform.position = position + Vector3.up * 0.22f;
            coral.transform.localScale = new Vector3(Random.Range(0.08f, 0.22f), Random.Range(0.38f, 1.15f), Random.Range(0.08f, 0.22f));
            coral.transform.rotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f));
            coral.GetComponent<MeshRenderer>().sharedMaterial = coralMaterial;
            DestroyCollider(coral);
        }

        for (int i = 0; i < branchCoralCount; i++)
        {
            CreateBranchCoralCluster(RandomSeabedPosition(0.86f, true), i);
        }
    }

    private void CreateRockMountainAccents()
    {
        if (!TryGetFeaturePoint(OceanFeatureKind.RockMountain, out Vector3 peak))
        {
            return;
        }

        Vector3 center = transform.InverseTransformPoint(peak);
        for (int i = 0; i < 18; i++)
        {
            float angle = i * Mathf.PI * 2f / 18f + Random.Range(-0.16f, 0.16f);
            float radius = Random.Range(3.8f, 10.5f);
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            Vector3 position = SampleSeabedPosition(x, z);
            GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spire.name = "Rock Mountain Spire";
            spire.transform.SetParent(generatedRoot, false);
            spire.transform.position = position + Vector3.up * Random.Range(0.8f, 2.4f);
            spire.transform.localScale = new Vector3(Random.Range(0.7f, 1.4f), Random.Range(1.8f, 4.8f), Random.Range(0.7f, 1.4f));
            spire.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            spire.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
            DestroyCollider(spire);
        }
    }

    private void CreateBranchCoralCluster(Vector3 position, int index)
    {
        GameObject cluster = new GameObject("Branch Coral Cluster");
        cluster.transform.SetParent(generatedRoot, false);
        cluster.transform.position = position + Vector3.up * 0.05f;

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

        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mound.name = "Coral Base";
        mound.transform.SetParent(cluster.transform, false);
        mound.transform.localPosition = Vector3.up * 0.03f;
        mound.transform.localScale = new Vector3(Random.Range(0.42f, 0.86f), 0.14f, Random.Range(0.42f, 0.86f));
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
