using UnityEngine;

public class OceanCameraRig : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 orbitSize = new Vector3(10f, 3f, 8f);
    [SerializeField] private float orbitSeconds = 38f;
    [SerializeField] private float lookAhead = 2.5f;

    private void LateUpdate()
    {
        float t = Time.time / Mathf.Max(1f, orbitSeconds) * Mathf.PI * 2f;
        Vector3 position = center + new Vector3(
            Mathf.Sin(t) * orbitSize.x,
            Mathf.Sin(t * 0.45f) * orbitSize.y,
            Mathf.Cos(t) * orbitSize.z
        );

        transform.position = position;

        Vector3 lookTarget = target != null
            ? target.position
            : center + new Vector3(Mathf.Sin(t + 0.8f) * lookAhead, 0f, Mathf.Cos(t + 0.8f) * lookAhead);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookTarget - transform.position),
            1.8f * Time.deltaTime
        );
    }
}
