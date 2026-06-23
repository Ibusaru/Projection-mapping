using TMPro;
using UnityEngine;

public class FishActor : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Renderer[] colorRenderers;
    [SerializeField] private Renderer[] subColorRenderers;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private TMP_Text nicknameLabel;

    [Header("Movement")]
    [SerializeField] private float baseSpeed = 1.2f;
    [SerializeField] private float turnSpeed = 1.8f;
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float labelVisibleDistance = 3.2f;

    private Vector3 targetPosition;
    private string species = "original";
    private string personality = "calm";
    private Camera mainCamera;

    public float SpawnTime { get; private set; }

    private void Awake()
    {
        mainCamera = Camera.main;
        SpawnTime = Time.time;
        PickNextTarget();
    }

    public void Apply(FishData data)
    {
        species = string.IsNullOrWhiteSpace(data.species) ? "original" : data.species;
        personality = string.IsNullOrWhiteSpace(data.personality) ? "calm" : data.personality;

        ApplyColor(colorRenderers, ParseColor(data.main_color, Color.cyan));
        ApplyColor(subColorRenderers, ParseColor(data.sub_color, Color.white));

        if (modelRoot != null)
        {
            modelRoot.localScale = Vector3.one * SizeToScale(data.size);
        }

        if (nicknameLabel != null)
        {
            nicknameLabel.text = SanitizeNickname(data.nickname);
            nicknameLabel.gameObject.SetActive(false);
        }

        baseSpeed *= PersonalitySpeedMultiplier(personality);
    }

    private void Update()
    {
        Swim();
        UpdateLabel();
    }

    private void Swim()
    {
        if (species == "jellyfish")
        {
            Vector3 floatMotion = new Vector3(
                Mathf.Sin(Time.time * 0.45f + transform.position.x) * 0.25f,
                Mathf.Sin(Time.time * 0.8f) * 0.45f + 0.22f,
                Mathf.Cos(Time.time * 0.35f + transform.position.z) * 0.22f
            );
            transform.position += floatMotion * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(floatMotion.x, 0f, floatMotion.z).normalized + Vector3.forward),
                turnSpeed * 0.35f * Time.deltaTime
            );
            return;
        }

        Vector3 toTarget = targetPosition - transform.position;
        if (toTarget.magnitude < 0.8f)
        {
            PickNextTarget();
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * baseSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            turnSpeed * Time.deltaTime
        );

        if (modelRoot != null)
        {
            float sway = Mathf.Sin(Time.time * 7f) * 4f;
            modelRoot.localRotation = Quaternion.Euler(0f, sway, 0f);
        }
    }

    private void UpdateLabel()
    {
        if (nicknameLabel == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
        bool visible = distance <= labelVisibleDistance;
        nicknameLabel.gameObject.SetActive(visible);

        if (visible)
        {
            nicknameLabel.transform.LookAt(mainCamera.transform);
            nicknameLabel.transform.Rotate(0f, 180f, 0f);
        }
    }

    private void PickNextTarget()
    {
        targetPosition = new Vector3(
            Random.Range(-wanderRadius, wanderRadius),
            Random.Range(-wanderRadius * 0.35f, wanderRadius * 0.35f),
            Random.Range(-wanderRadius, wanderRadius)
        );
    }

    private static void ApplyColor(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer item in renderers)
        {
            if (item != null)
            {
                item.material.color = color;
            }
        }
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
    }

    private static float SizeToScale(string size)
    {
        return size switch
        {
            "small" => 0.82f,
            "large" => 1.18f,
            _ => 1f
        };
    }

    private static float PersonalitySpeedMultiplier(string value)
    {
        return value switch
        {
            "fast" => 1.45f,
            "schooling" => 1.12f,
            _ => 0.88f
        };
    }

    private static string SanitizeNickname(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "no name";
        }

        string cleaned = value.Replace("\r", "").Replace("\n", "").Trim();
        return cleaned.Length > 12 ? cleaned.Substring(0, 12) : cleaned;
    }
}
