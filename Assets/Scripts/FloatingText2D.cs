using TMPro;
using UnityEngine;

/// <summary>A small pixel-font number that rises and fades out, used for damage dealt by the saw.</summary>
public sealed class FloatingText2D : MonoBehaviour
{
    private const float Lifetime = 0.6f;
    private const float RiseSpeed = 1.4f;

    private TextMeshPro text;
    private Color color;
    private float startedAt;

    public static void Spawn(Vector3 position, string message, Color color, float size = 2.4f)
    {
        var textObject = new GameObject("Floating Text");
        textObject.transform.position = position;
        var floating = textObject.AddComponent<FloatingText2D>();

        floating.text = textObject.AddComponent<TextMeshPro>();
        if (UiTheme.Current.titleFont != null)
            floating.text.font = UiTheme.Current.titleFont;
        floating.text.text = message;
        floating.text.fontSize = size;
        floating.text.alignment = TextAlignmentOptions.Center;
        floating.text.color = color;
        floating.text.sortingOrder = 20;
        floating.text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        floating.color = color;
        floating.startedAt = Time.unscaledTime;
    }

    private void Update()
    {
        float age = Time.unscaledTime - startedAt;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += Vector3.up * (RiseSpeed * Time.unscaledDeltaTime);
        Color faded = color;
        faded.a *= 1f - Mathf.Clamp01((age - Lifetime * 0.5f) / (Lifetime * 0.5f));
        text.color = faded;
    }
}
