using UnityEngine;

public partial class OceanEnvironment
{
    private void CreateWaterSurface()
    {
        int points = waterResolution + 1;
        Vector3[] vertices = new Vector3[points * points];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[waterResolution * waterResolution * 6];
        float mainHalfX = oceanSize.x * 0.5f;
        float mainHalfZ = oceanSize.y * 0.5f;
        float extensionScale = createOpenOceanBackdrop ? Mathf.Clamp(openOceanBackdropScale, 1f, 2.4f) : 1f;
        float halfX = mainHalfX * extensionScale;
        float depthScale = createOpenOceanBackdrop ? Mathf.Clamp(openOceanBackdropDepthScale, 1f, 3f) : 1f;
        float halfZ = mainHalfZ * extensionScale * depthScale;
        float coastlineHalfZ = CoastlineRenderHalfZ();

        for (int z = 0; z < points; z++)
        {
            float tz = z / (float)waterResolution;
            float localZ = Mathf.Lerp(-halfZ, halfZ, tz);
            float coastZ = Mathf.Clamp(localZ, -coastlineHalfZ, coastlineHalfZ);
            float coastEndBlend = Smooth01(Mathf.InverseLerp(coastlineHalfZ * 0.92f, coastlineHalfZ * 1.08f, Mathf.Abs(localZ)));
            float waterEdgeX = Mathf.Lerp(WaterSurfaceEdgeX(coastZ), halfX, coastEndBlend);
            for (int x = 0; x < points; x++)
            {
                float tx = x / (float)waterResolution;
                vertices[z * points + x] = new Vector3(
                    Mathf.Lerp(-halfX, waterEdgeX, tx),
                    0f,
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

        GameObject water = new GameObject("Water Surface");
        water.transform.SetParent(generatedRoot, false);
        // Keep the authoritative water level on the transform. The imported
        // Simple Water Shader Graph reconstructs vertex Position and replaces
        // local Y with wave noise, so baking the world height into mesh
        // vertices made every configured waterSurfaceY render back near zero.
        water.transform.localPosition = Vector3.up * waterSurfaceY;
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
        float halfZ = CoastlineRenderHalfZ();

        for (int z = 0; z < zPoints; z++)
        {
            float v = z / (float)zSegments;
            float localZ = Mathf.Lerp(-halfZ, halfZ, v);
            float shorelineX = SandMaterialTransitionX(localZ);

            for (int x = 0; x < xPoints; x++)
            {
                float u = x / (float)xSegments;
                float localX = Mathf.Lerp(shorelineX, halfX, u);
                vertices[z * xPoints + x] = new Vector3(
                    localX,
                    SampleShorelineLandHeight(localX, localZ),
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

    }

    private void CreateUnderwaterSurfaceCue()
    {
        if (!createUnderwaterSurfaceCue || waterMesh == null || underwaterSurfaceMaterial == null)
        {
            return;
        }

        GameObject cue = new GameObject("Underwater Water Surface Cue");
        cue.transform.SetParent(generatedRoot, false);
        cue.transform.localPosition = Vector3.up * (waterSurfaceY - 0.035f);
        cue.AddComponent<MeshFilter>().sharedMesh = waterMesh;
        cue.AddComponent<MeshRenderer>().sharedMaterial = underwaterSurfaceMaterial;
        underwaterSurfaceMaterial.SetFloat("_WaterLevel", transform.TransformPoint(new Vector3(0f, waterSurfaceY, 0f)).y);
    }

    private float ShorelineActiveHalfX(float halfX)
    {
        return OceanShorelineLayout.ActiveHalfX(oceanSize, activeAreaSize);
    }

    private float ShorelineUsableWidth(float halfX, float activeHalfX)
    {
        return OceanShorelineLayout.UsableWidth(oceanSize, activeAreaSize, shorelineWidth);
    }

    private float WaterSurfaceEdgeX(float z)
    {
        return createVisibleShoreline ? SandMaterialTransitionX(z) : oceanSize.x * 0.5f;
    }

    private float CoastlineRenderHalfZ()
    {
        float mainHalfZ = oceanSize.y * 0.5f;
        return createOpenOceanBackdrop ? mainHalfZ * 1.45f : mainHalfZ;
    }

    private float ShorelineStartX(float z)
    {
        return OceanShorelineLayout.StartX(oceanSize, activeAreaSize, shorelineWidth, decorationSeed, z);
    }

    private float SandMaterialTransitionX(float z)
    {
        return OceanShorelineLayout.SandMaterialTransitionX(oceanSize, activeAreaSize, shorelineWidth, decorationSeed, z);
    }

    private float SampleShorelineLandHeight(float x, float z)
    {
        return OceanShorelineLayout.SampleLandHeight(oceanSize, activeAreaSize, shorelineWidth, decorationSeed, shorelineSurfaceY, x, z);
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

    }

    private void CreateCausticLines()
    {
        int visibleCount = Mathf.Min(causticLineCount, 18);
        causticLines = new LineRenderer[visibleCount];

        for (int i = 0; i < visibleCount; i++)
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

}
