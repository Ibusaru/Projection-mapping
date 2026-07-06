using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProceduralFishMeshDeformer : MonoBehaviour
{
    [SerializeField] private float frequency = 4.8f;
    [SerializeField] private float sideAmplitude = 0.035f;
    [SerializeField] private float twistAmplitude = 4.5f;
    [SerializeField] private float waveLength = 1.35f;
    [SerializeField] private float centerStillness = 0.18f;

    private MeshFilter[] meshFilters = new MeshFilter[0];
    private Mesh[] runtimeMeshes = new Mesh[0];
    private Vector3[][] baseVertices = new Vector3[0][];
    private Bounds[] baseBounds = new Bounds[0];
    private float phaseOffset;

    private void Awake()
    {
        phaseOffset = Random.Range(0f, 1000f);
        CaptureMeshes();
    }

    private void OnEnable()
    {
        CaptureMeshes();
    }

    private void LateUpdate()
    {
        if (!HasUsableMeshes())
        {
            CaptureMeshes();
        }

        float phase = (Time.time + phaseOffset) * frequency;
        for (int i = 0; i < runtimeMeshes.Length; i++)
        {
            Mesh mesh = runtimeMeshes[i];
            Vector3[] sourceVertices = i < baseVertices.Length ? baseVertices[i] : null;
            if (mesh == null || sourceVertices == null || sourceVertices.Length == 0)
            {
                continue;
            }

            Bounds bounds = baseBounds[i];
            int lengthAxis = LargestAxis(bounds.size);
            int sideAxis = SmallestAxis(bounds.size, lengthAxis);
            int upAxis = RemainingAxis(lengthAxis, sideAxis);
            float minLength = AxisValue(bounds.min, lengthAxis);
            float maxLength = AxisValue(bounds.max, lengthAxis);
            float sideSize = Mathf.Max(AxisValue(bounds.size, sideAxis), 0.0001f);
            float upSize = Mathf.Max(AxisValue(bounds.size, upAxis), 0.0001f);
            Vector3[] vertices = new Vector3[sourceVertices.Length];

            for (int vertexIndex = 0; vertexIndex < sourceVertices.Length; vertexIndex++)
            {
                Vector3 vertex = sourceVertices[vertexIndex];
                float t = Mathf.InverseLerp(minLength, maxLength, AxisValue(vertex, lengthAxis));
                float centered = Mathf.Abs(t - 0.5f) * 2f;
                float weight = Mathf.SmoothStep(centerStillness, 1f, centered);
                float wave = Mathf.Sin(phase - t * Mathf.PI * 2f * waveLength);
                float sideOffset = wave * sideAmplitude * sideSize * weight;
                float twist = Mathf.Sin(phase * 0.78f - t * Mathf.PI * 2f) * twistAmplitude * Mathf.Deg2Rad * weight;
                float upRelative = AxisValue(vertex, upAxis) - AxisValue(bounds.center, upAxis);

                SetAxisValue(ref vertex, sideAxis, AxisValue(vertex, sideAxis) + sideOffset + upRelative * Mathf.Sin(twist) * 0.35f);
                SetAxisValue(ref vertex, upAxis, AxisValue(vertex, upAxis) + wave * 0.01f * upSize * weight);
                vertices[vertexIndex] = vertex;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }

    private void CaptureMeshes()
    {
        meshFilters = GetComponentsInChildren<MeshFilter>(true);
        runtimeMeshes = new Mesh[meshFilters.Length];
        baseVertices = new Vector3[meshFilters.Length][];
        baseBounds = new Bounds[meshFilters.Length];

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            Mesh sourceMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (sourceMesh == null || !sourceMesh.isReadable)
            {
                continue;
            }

            Mesh runtimeMesh = Instantiate(sourceMesh);
            runtimeMesh.name = sourceMesh.name.EndsWith("_Animated") ? sourceMesh.name : $"{sourceMesh.name}_Animated";
            meshFilter.sharedMesh = runtimeMesh;
            runtimeMeshes[i] = runtimeMesh;
            baseVertices[i] = runtimeMesh.vertices;
            baseBounds[i] = runtimeMesh.bounds;
        }
    }

    private bool HasUsableMeshes()
    {
        if (meshFilters == null || runtimeMeshes == null || meshFilters.Length != runtimeMeshes.Length)
        {
            return false;
        }

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] != null && meshFilters[i].sharedMesh != runtimeMeshes[i])
            {
                return false;
            }
        }

        return runtimeMeshes.Length > 0;
    }

    private static int LargestAxis(Vector3 value)
    {
        if (value.x >= value.y && value.x >= value.z)
        {
            return 0;
        }

        return value.y >= value.z ? 1 : 2;
    }

    private static int SmallestAxis(Vector3 value, int ignoredAxis)
    {
        int bestAxis = ignoredAxis == 0 ? 1 : 0;
        float bestValue = AxisValue(value, bestAxis);
        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == ignoredAxis)
            {
                continue;
            }

            float candidate = AxisValue(value, axis);
            if (candidate < bestValue)
            {
                bestAxis = axis;
                bestValue = candidate;
            }
        }

        return bestAxis;
    }

    private static int RemainingAxis(int firstAxis, int secondAxis)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            if (axis != firstAxis && axis != secondAxis)
            {
                return axis;
            }
        }

        return 1;
    }

    private static float AxisValue(Vector3 value, int axis)
    {
        return axis switch
        {
            0 => value.x,
            1 => value.y,
            _ => value.z
        };
    }

    private static void SetAxisValue(ref Vector3 value, int axis, float axisValue)
    {
        if (axis == 0)
        {
            value.x = axisValue;
        }
        else if (axis == 1)
        {
            value.y = axisValue;
        }
        else
        {
            value.z = axisValue;
        }
    }
}
