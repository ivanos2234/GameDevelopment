using TMPro;
using UnityEngine;

/// <summary>
/// A hard, pixel-style drop shadow for a TextMeshPro UI text: a copy of the text drawn behind it at an offset.
/// Keeps the copy's text in sync, so the main text can be changed freely.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class UiTextShadow : MonoBehaviour
{
    private TextMeshProUGUI source;
    private TextMeshProUGUI shadow;

    public static void Add(TextMeshProUGUI text, Color shadowColor, Vector2 offset)
    {
        var shadowObject = Object.Instantiate(text.gameObject, text.transform.parent);
        shadowObject.name = text.name + " Shadow";
        shadowObject.transform.SetSiblingIndex(text.transform.GetSiblingIndex());
        var shadowRect = (RectTransform)shadowObject.transform;
        shadowRect.anchoredPosition += offset;

        var component = text.gameObject.AddComponent<UiTextShadow>();
        component.source = text;
        component.shadow = shadowObject.GetComponent<TextMeshProUGUI>();
        component.shadow.color = shadowColor;
    }

    private void LateUpdate()
    {
        if (shadow == null)
            return;

        if (shadow.text != source.text)
            shadow.text = source.text;
        shadow.gameObject.SetActive(source.isActiveAndEnabled && source.alpha > 0f);
        shadow.alpha = source.alpha;
    }
}
