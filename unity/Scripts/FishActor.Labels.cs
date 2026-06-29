using TMPro;
using UnityEngine;

public partial class FishActor
{
    private void UpdateLabel()
    {
        if (!releasedFish)
        {
            if (nicknameLabel != null)
            {
                nicknameLabel.gameObject.SetActive(false);
            }

            return;
        }

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

    private void EnsureNicknameLabel()
    {
        if (!releasedFish || nicknameLabel != null || !createNicknameLabelWhenMissing)
        {
            return;
        }

        if (!HasTextMeshProResources())
        {
            if (!warnedMissingTmpResources)
            {
                Debug.LogWarning("FishActor: TextMesh Pro Essential Resources are missing; nickname labels will be skipped.");
                warnedMissingTmpResources = true;
            }

            return;
        }

        GameObject labelObject = new GameObject("Nickname Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = Vector3.up * 0.9f;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 0.42f;
        label.color = new Color(0.9f, 1f, 1f, 0.92f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = string.Empty;
        nicknameLabel = label;
    }

    private static bool HasTextMeshProResources()
    {
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") != null;
    }
}
