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
        float activeHalfX = ShorelineActiveHalfX(halfX);
        float usableWidth = ShorelineUsableWidth(halfX, activeHalfX);

        for (int z = 0; z < points; z++)
        {
            float tz = z / (float)waterResolution;
            float localZ = Mathf.Lerp(-halfZ, halfZ, tz);
            float waterEdgeX = WaterSurfaceEdgeX(localZ, halfX, halfZ, activeHalfX, usableWidth);
            for (int x = 0; x < points; x++)
            {
                float tx = x / (float)waterResolution;
                vertices[z * points + x] = new Vector3(
                    Mathf.Lerp(-halfX, waterEdgeX, tx),
                    waterSurfaceY,
                    localZ
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

    private void CreateVisibleShoreline()
    {
        if (!createVisibleShoreline)
        {
            return;
        }

        int zSegments = Mathf.Max(4, shorelineResolution);
        int xSegments = Mathf.Max(4, Mathf.RoundToInt(shorelineResolution * 0.36f));
        int zPoints = zSegments + 1;
        int xPoints = xSegments + 1;
        Vector3[] vertices = new Vector3[zPoints * xPoints];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[zSegments * xSegments * 6];
        float halfX = oceanSize.x * 0.5f;
        float halfZ = oceanSize.y * 0.5f;
        float activeHalfX = Mathf.Min(Mathf.Abs(activeAreaSize.x) * 0.5f, Mathf.Max(1f, halfX - 3f));
        float usableWidth = Mathf.Clamp(shorelineWidth, 3f, Mathf.Max(3f, halfX - activeHalfX - 2f));

        for (int z = 0; z < zPoints; z++)
        {
            float v = z / (float)zSegments;
            float localZ = Mathf.Lerp(-halfZ, halfZ, v);
            float shorelineX = ShorelineStartX(localZ, halfX, halfZ, activeHalfX, usableWidth);

            for (int x = 0; x < xPoints; x++)
            {
                float u = x / (float)xSegments;
                float localX = Mathf.Lerp(shorelineX, halfX, u);
                vertices[z * xPoints + x] = new Vector3(
                    localX,
                    SampleShorelineLandHeight(localX, localZ, shorelineX, halfX),
                    localZ
                );
                uvs[z * xPoints + x] = new Vector2(u * 2.4f, v * 5f);
            }
        }

        int index = 0;
        for (int z = 0; z < zSegments; z++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int i = z * xPoints + x;
                triangles[index++] = i;
                triangles[index++] = i + xPoints;
                triangles[index++] = i + 1;
                triangles[index++] = i + 1;
                triangles[index++] = i + xPoints;
                triangles[index++] = i + xPoints + 1;
            }
        }

        Mesh shoreMesh = new Mesh { name = "Generated Visible Shoreline Mesh" };
        shoreMesh.vertices = vertices;
        shoreMesh.uv = uvs;
        shoreMesh.triangles = triangles;
        shoreMesh.RecalculateNormals();
        shoreMesh.RecalculateBounds();

        GameObject shore = new GameObject("Visible Shoreline");
        shore.transform.SetParent(generatedRoot, false);
        shore.AddComponent<MeshFilter>().sharedMesh = shoreMesh;
        shore.AddComponent<MeshRenderer>().sharedMaterial = shorelineMaterial != null ? shorelineMaterial : seabedMaterial;

        CreateShorelineFoamLines(halfX, halfZ, activeHalfX, usableWidth);
    }

    private float ShorelineActiveHalfX(float halfX)
    {
        return Mathf.Min(Mathf.Abs(activeAreaSize.x) * 0.5f, Mathf.Max(1f, halfX - 3f));
    }

    private float ShorelineUsableWidth(float halfX, float activeHalfX)
    {
        return Mathf.Clamp(shorelineWidth, 3f, Mathf.Max(3f, halfX - activeHalfX - 2f));
    }

    private float WaterSurfaceEdgeX(float z, float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        if (!createVisibleShoreline)
        {
            return halfX;
        }

        return ShorelineStartX(z, halfX, halfZ, activeHalfX, usableWidth) - Mathf.Max(0f, shorelineWaterInset);
    }

    private float ShorelineStartX(float z, float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        float normalizedZ = halfZ > 0.001f ? z / halfZ : 0f;
        float inlet = Mathf.Sin(normalizedZ * Mathf.PI * 2.7f + decorationSeed * 0.017f) * usableWidth * 0.08f;
        float grain = (Mathf.PerlinNoise(decorationSeed * 0.013f, z * 0.018f + 19.7f) - 0.5f) * usableWidth * 0.16f;
        float baseStart = activeHalfX + Mathf.Max(18f, usableWidth * 0.22f);
        float start = baseStart + inlet + grain;
        return Mathf.Clamp(start, activeHalfX + 10f, halfX - usableWidth * 0.35f);
    }

    private float SampleShorelineLandHeight(float x, float z, float shorelineX, float halfX)
    {
        float inland = Smooth01(Mathf.InverseLerp(shorelineX, halfX, x));
        float dune = Mathf.Sin(z * 0.055f + inland * 2.8f + decorationSeed * 0.004f) * 0.18f;
        float grain = (Mathf.PerlinNoise(x * 0.055f + decorationSeed * 0.01f, z * 0.055f) - 0.5f) * 0.46f;
        return waterSurfaceY + Mathf.Lerp(-0.08f, 1.35f, inland) + (dune + grain) * inland;
    }

    private void CreateShorelineFoamLines(float halfX, float halfZ, float activeHalfX, float usableWidth)
    {
        if (shorelineFoamLineCount <= 0 || foamMaterial == null)
        {
            return;
        }

        int pointCount = Mathf.Max(12, shorelineResolution);
        for (int lineIndex = 0; lineIndex < shorelineFoamLineCount; lineIndex++)
        {
            GameObject lineObject = new GameObject("Shoreline Foam");
            lineObject.transform.SetParent(generatedRoot, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = foamMaterial;
            line.positionCount = pointCount;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            float width = Mathf.Lerp(0.07f, 0.025f, lineIndex / Mathf.Max(1f, shorelineFoamLineCount - 1f));
            line.startWidth = width;
            line.endWidth = width * 0.72f;
            float alpha = Mathf.Lerp(0.34f, 0.12f, lineIndex / Mathf.Max(1f, shorelineFoamLineCount - 1f));
            Color color = new Color(foamColor.r, foamColor.g, foamColor.b, alpha);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, alpha * 0.58f);

            float offset = lineIndex * 0.78f;
            for (int i = 0; i < pointCount; i++)
            {
                float t = pointCount <= 1 ? 0f : i / (float)(pointCount - 1);
                float z = Mathf.Lerp(-halfZ * 0.98f, halfZ * 0.98f, t);
                float x = WaterSurfaceEdgeX(z, halfX, halfZ, activeHalfX, usableWidth) - offset;
                float ripple = Mathf.Sin(t * Mathf.PI * 12f + lineIndex * 1.7f) * 0.18f;
                line.SetPosition(i, new Vector3(x + ripple, waterSurfaceY + 0.055f + lineIndex * 0.01f, z));
            }
        }
    }

    private void CreateOpenOceanBackdrop()
    {
        if (!createOpenOceanBackdrop)
        {
            return;
        }

        float scale = Mathf.Max(1.2f, openOceanBackdropScale);
        float width = oceanSize.x * scale;
        float depth = oceanSize.y * scale;

        GameObject floor = GeneratedPrimitiveFactory.Create(PrimitiveType.Plane, "Open Ocean Deep Floor", seabedMaterial);
        floor.transform.SetParent(generatedRoot, false);
        floor.transform.localPosition = new Vector3(0f, seabedY - 5.5f, 0f);
        floor.transform.localScale = new Vector3(width * 0.1f, 1f, depth * 0.1f);

        float mainHalfX = oceanSize.x * 0.5f;
        float mainHalfZ = oceanSize.y * 0.5f;
        float outerHalfX = width * 0.5f;
        float outerHalfZ = depth * 0.5f;
        float stripX = Mathf.Max(0f, outerHalfX - mainHalfX);
        float stripZ = Mathf.Max(0f, outerHalfZ - mainHalfZ);
        CreateBackdropWaterStrip("Open Ocean Horizon Water East", new Vector3((mainHalfX + outerHalfX) * 0.5f, waterSurfaceY - 0.08f, 0f), new Vector2(stripX, oceanSize.y));
        CreateBackdropWaterStrip("Open Ocean Horizon Water West", new Vector3(-(mainHalfX + outerHalfX) * 0.5f, waterSurfaceY - 0.08f, 0f), new Vector2(stripX, oceanSize.y));
        CreateBackdropWaterStrip("Open Ocean Horizon Water North", new Vector3(0f, waterSurfaceY - 0.08f, (mainHalfZ + outerHalfZ) * 0.5f), new Vector2(width, stripZ));
        CreateBackdropWaterStrip("Open Ocean Horizon Water South", new Vector3(0f, waterSurfaceY - 0.08f, -(mainHalfZ + outerHalfZ) * 0.5f), new Vector2(width, stripZ));
    }

    private void CreateBackdropWaterStrip(string objectName, Vector3 position, Vector2 size)
    {
        if (size.x <= 0.01f || size.y <= 0.01f)
        {
            return;
        }

        GameObject water = GeneratedPrimitiveFactory.Create(PrimitiveType.Plane, objectName, waterMaterial);
        water.transform.SetParent(generatedRoot, false);
        water.transform.localPosition = position;
        water.transform.localScale = new Vector3(size.x * 0.1f, 1f, size.y * 0.1f);
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
        sunObject.transform.rotation = Quaternion.LookRotation(new Vector3(-0.28f, -0.9f, 0.24f).normalized, Vector3.up);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.82f);
        sun.intensity = 1.85f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        GameObject fillObject = new GameObject("Clear Reef Fill Light");
        fillObject.transform.SetParent(generatedRoot, false);
        fillObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.38f, -0.72f, -0.32f).normalized, Vector3.up);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.52f, 0.86f, 1f);
        fill.intensity = 0.58f;
        fill.shadows = LightShadows.None;

        int spotCount = 7;
        float activeHalfX = Mathf.Max(18f, activeAreaSize.x * 0.45f);
        float activeHalfZ = Mathf.Max(14f, activeAreaSize.y * 0.45f);
        for (int i = 0; i < spotCount; i++)
        {
            GameObject lightObject = new GameObject("Underwater Sun Spot");
            lightObject.transform.SetParent(generatedRoot, false);
            float ratio = spotCount <= 1 ? 0f : i / (float)(spotCount - 1);
            lightObject.transform.position = new Vector3(Mathf.Lerp(-activeHalfX, activeHalfX, ratio), waterSurfaceY - 0.08f, Mathf.Lerp(-activeHalfZ, activeHalfZ, Mathf.PingPong(i * 0.37f, 1f)));
            lightObject.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Lerp(-0.22f, 0.22f, ratio), -1f, 0.28f).normalized, Vector3.up);

            Light spot = lightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.76f, 0.96f, 1f);
            spot.intensity = 1.65f;
            spot.range = waterSurfaceY - seabedY + 18f;
            spot.spotAngle = 52f;
            spot.shadows = LightShadows.None;
        }

        CreateSunbeams();
    }

    private void CreateSunbeams()
    {
        int beamCount = 32;
        float halfX = Mathf.Max(20f, activeAreaSize.x * 0.62f);
        float halfZ = Mathf.Max(16f, activeAreaSize.y * 0.62f);
        Vector3 fallDirection = new Vector3(-0.26f, -1f, 0.32f).normalized;

        for (int i = 0; i < beamCount; i++)
        {
            GameObject beamObject = new GameObject("Clear Sunbeam");
            beamObject.transform.SetParent(generatedRoot, false);

            float x = Random.Range(-halfX, halfX);
            float z = Random.Range(-halfZ, halfZ);
            float depth = Random.Range(9f, 20f);
            Vector3 start = new Vector3(x, waterSurfaceY + 0.05f, z);
            Vector3 end = start + fallDirection * depth;

            LineRenderer beam = beamObject.AddComponent<LineRenderer>();
            beam.sharedMaterial = causticLineMaterial;
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.textureMode = LineTextureMode.Stretch;
            beam.alignment = LineAlignment.View;
            beam.startWidth = Random.Range(0.09f, 0.18f);
            beam.endWidth = Random.Range(0.24f, 0.46f);
            beam.startColor = new Color(0.78f, 0.98f, 1f, 0.24f);
            beam.endColor = new Color(0.32f, 0.72f, 0.8f, 0.035f);
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
