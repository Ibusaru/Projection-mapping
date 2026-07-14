using System.Collections.Generic;
using UnityEngine;

public partial class OceanEnvironment
{
    private const string HorizonSeabedObjectName = "Horizon Seabed Continuation";

    private void CreateHorizonSeabedContinuation()
    {
        if (!createOpenOceanBackdrop || seabedMaterial == null)
        {
            return;
        }

        Vector2 innerHalfExtents = DetailedSeabedHalfExtents();
        float outerHalfExtent = HorizonBackdropHalfExtent();
        if (outerHalfExtent <= Mathf.Max(innerHalfExtents.x, innerHalfExtents.y) + 1f)
        {
            return;
        }

        float[] xCoordinates = BuildHorizonAxis(outerHalfExtent, innerHalfExtents.x, 18, 28);
        float[] zCoordinates = BuildHorizonAxis(outerHalfExtent, innerHalfExtents.y, 18, 34);
        int xPoints = xCoordinates.Length;
        int zPoints = zCoordinates.Length;
        Vector3[] vertices = new Vector3[xPoints * zPoints];
        Vector2[] uvs = new Vector2[vertices.Length];
        List<int> triangles = new List<int>((xPoints - 1) * (zPoints - 1) * 6);
        float mainHalfX = oceanSize.x * 0.5f;
        float mainHalfZ = oceanSize.y * 0.5f;

        for (int z = 0; z < zPoints; z++)
        {
            for (int x = 0; x < xPoints; x++)
            {
                float px = xCoordinates[x];
                float pz = zCoordinates[z];
                int vertexIndex = z * xPoints + x;
                vertices[vertexIndex] = new Vector3(
                    px,
                    SampleHorizonSeabedHeight(px, pz, innerHalfExtents, mainHalfX, mainHalfZ),
                    pz
                );
                uvs[vertexIndex] = new Vector2(
                    (px + outerHalfExtent) / 58f,
                    (pz + outerHalfExtent) / 58f
                );
            }
        }

        for (int z = 0; z < zPoints - 1; z++)
        {
            float centerZ = (zCoordinates[z] + zCoordinates[z + 1]) * 0.5f;
            for (int x = 0; x < xPoints - 1; x++)
            {
                float centerX = (xCoordinates[x] + xCoordinates[x + 1]) * 0.5f;
                if (Mathf.Abs(centerX) < innerHalfExtents.x
                    && Mathf.Abs(centerZ) < innerHalfExtents.y)
                {
                    continue;
                }

                int i = z * xPoints + x;
                triangles.Add(i);
                triangles.Add(i + xPoints);
                triangles.Add(i + 1);
                triangles.Add(i + 1);
                triangles.Add(i + xPoints);
                triangles.Add(i + xPoints + 1);
            }
        }

        Mesh horizonMesh = new Mesh { name = "Generated Horizon Seabed Mesh" };
        horizonMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        horizonMesh.vertices = vertices;
        horizonMesh.uv = uvs;
        horizonMesh.triangles = triangles.ToArray();
        horizonMesh.RecalculateNormals();
        horizonMesh.RecalculateBounds();

        GameObject horizonSeabed = new GameObject(HorizonSeabedObjectName);
        horizonSeabed.transform.SetParent(generatedRoot, false);
        horizonSeabed.AddComponent<MeshFilter>().sharedMesh = horizonMesh;
        horizonSeabed.AddComponent<MeshRenderer>().sharedMaterial = seabedMaterial;
    }

    private float HorizonBackdropHalfExtent()
    {
        if (!createOpenOceanBackdrop)
        {
            return 0f;
        }

        Vector2 detailedHalfExtents = DetailedSeabedHalfExtents();
        return Mathf.Max(
            horizonBackdropDistance,
            Mathf.Max(detailedHalfExtents.x, detailedHalfExtents.y) + 64f
        );
    }

    private Vector2 DetailedSeabedHalfExtents()
    {
        float extensionScale = Mathf.Clamp(openOceanBackdropScale, 1f, 2.4f);
        float depthScale = Mathf.Clamp(openOceanBackdropDepthScale, 1f, 3f);
        return new Vector2(
            oceanSize.x * 0.5f * extensionScale,
            oceanSize.y * 0.5f * extensionScale * depthScale
        );
    }

    private float SampleHorizonSeabedHeight(
        float x,
        float z,
        Vector2 innerHalfExtents,
        float mainHalfX,
        float mainHalfZ
    )
    {
        float edgeX = Mathf.Clamp(x, -innerHalfExtents.x, innerHalfExtents.x);
        float edgeZ = Mathf.Clamp(z, -innerHalfExtents.y, innerHalfExtents.y);
        float edgeY = SampleExtendedSeabedPosition(edgeX, edgeZ, mainHalfX, mainHalfZ).y;
        float outsideX = Mathf.Max(0f, Mathf.Abs(x) - innerHalfExtents.x);
        float outsideZ = Mathf.Max(0f, Mathf.Abs(z) - innerHalfExtents.y);
        float outsideDistance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
        float blend = Smooth01(Mathf.InverseLerp(0f, horizonSeabedBlendDistance, outsideDistance));
        float noise = (Mathf.PerlinNoise(x * 0.006f + 17.3f, z * 0.006f + 42.1f) - 0.5f) * 0.9f;
        float distantY = seabedY - horizonSeabedDrop + noise;
        return Mathf.Lerp(edgeY, distantY, blend);
    }

    private static float[] BuildHorizonAxis(
        float outerHalfExtent,
        float innerHalfExtent,
        int outerSegments,
        int centerSegments
    )
    {
        List<float> values = new List<float>(outerSegments * 2 + centerSegments + 1);
        AppendAxisRange(values, -outerHalfExtent, -innerHalfExtent, outerSegments, true);
        AppendAxisRange(values, -innerHalfExtent, innerHalfExtent, centerSegments, false);
        AppendAxisRange(values, innerHalfExtent, outerHalfExtent, outerSegments, false);
        return values.ToArray();
    }

    private static void AppendAxisRange(
        List<float> values,
        float start,
        float end,
        int segments,
        bool includeStart
    )
    {
        int safeSegments = Mathf.Max(1, segments);
        int first = includeStart ? 0 : 1;
        for (int i = first; i <= safeSegments; i++)
        {
            values.Add(Mathf.Lerp(start, end, i / (float)safeSegments));
        }
    }
}
