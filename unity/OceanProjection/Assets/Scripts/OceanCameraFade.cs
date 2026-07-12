using UnityEngine;

[DisallowMultipleComponent]
internal sealed class OceanCameraFade : MonoBehaviour
{
    private Texture2D texture;
    private float alpha;

    public void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
    }

    private void OnGUI()
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        if (texture == null)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Ocean Camera Fade Pixel",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
        }

        Color previous = GUI.color;
        GUI.color = new Color(0.015f, 0.12f, 0.18f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), texture, ScaleMode.StretchToFill);
        GUI.color = previous;
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
