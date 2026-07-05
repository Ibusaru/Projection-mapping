using UnityEngine;

public partial class OceanEnvironment
{
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
        GameObject sunObject = new GameObject("Clear Reef Sun");
        sunObject.transform.SetParent(generatedRoot, false);
        sunObject.transform.rotation = Quaternion.LookRotation(new Vector3(-0.34f, -0.86f, 0.38f).normalized, Vector3.up);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.75f, 0.92f, 0.96f);
        sun.intensity = 1.05f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        for (int i = 0; i < 4; i++)
        {
            GameObject lightObject = new GameObject("Underwater Sun Spot");
            lightObject.transform.SetParent(generatedRoot, false);
            float ratio = i / 3f;
            lightObject.transform.position = new Vector3(Mathf.Lerp(-28f, 28f, ratio), waterSurfaceY - 0.08f, -18f + i * 10f);
            lightObject.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Lerp(-0.22f, 0.22f, ratio), -1f, 0.28f).normalized, Vector3.up);

            Light spot = lightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.58f, 0.86f, 0.88f);
            spot.intensity = 0.84f;
            spot.range = waterSurfaceY - seabedY + 8f;
            spot.spotAngle = 36f;
            spot.shadows = LightShadows.None;
        }

        CreateSunbeams();
    }

    private void CreateSunbeams()
    {
        int beamCount = 18;
        float halfX = oceanSize.x * 0.44f;
        float halfZ = oceanSize.y * 0.42f;
        Vector3 fallDirection = new Vector3(-0.26f, -1f, 0.32f).normalized;

        for (int i = 0; i < beamCount; i++)
        {
            GameObject beamObject = new GameObject("Clear Sunbeam");
            beamObject.transform.SetParent(generatedRoot, false);

            float x = Random.Range(-halfX, halfX);
            float z = Random.Range(-halfZ, halfZ);
            float depth = Random.Range(7f, 15f);
            Vector3 start = new Vector3(x, waterSurfaceY + 0.05f, z);
            Vector3 end = start + fallDirection * depth;

            LineRenderer beam = beamObject.AddComponent<LineRenderer>();
            beam.sharedMaterial = causticLineMaterial;
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.textureMode = LineTextureMode.Stretch;
            beam.alignment = LineAlignment.View;
            beam.startWidth = Random.Range(0.06f, 0.13f);
            beam.endWidth = Random.Range(0.18f, 0.34f);
            beam.startColor = new Color(0.62f, 0.88f, 0.9f, 0.14f);
            beam.endColor = new Color(0.16f, 0.42f, 0.5f, 0.018f);
            beam.SetPosition(0, start);
            beam.SetPosition(1, end);
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
}
