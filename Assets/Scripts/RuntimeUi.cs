using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum UiFont
{
    /// <summary>Press Start 2P: titles, buttons, numbers.</summary>
    Title,
    /// <summary>VT323: labels, tips, body text.</summary>
    Body,
}

/// <summary>Helpers for building the pixel-art screens (menu, HUD, level select) in code, styled from <see cref="UiTheme"/>.</summary>
public static class RuntimeUi
{
    public static UiTheme Theme => UiTheme.Current;

    /// <summary>An overlay canvas that scales with the screen from a 1920x1080 layout.</summary>
    public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvas.pixelPerfect = true;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasObject.GetComponent<RectTransform>();
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static TextMeshProUGUI CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment,
        Color color, UiFont font = UiFont.Body)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = font == UiFont.Title ? Theme.titleFont : Theme.bodyFont;
        if (fontAsset != null)
            text.font = fontAsset;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    /// <summary>A flat box with an optional pixel border and a hard drop shadow below it.</summary>
    public static Image CreateBox(Transform parent, string name, Color fill, Color border, float borderWidth, Color shadow, float shadowDepth)
    {
        Image box = CreateImage(parent, name, fill);
        AddFrame(box, border, borderWidth, shadow, shadowDepth);
        return box;
    }

    public static void AddFrame(Graphic graphic, Color border, float borderWidth, Color shadow, float shadowDepth)
    {
        if (borderWidth > 0f)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(borderWidth, borderWidth);
        }

        if (shadowDepth > 0f)
        {
            var dropShadow = graphic.gameObject.AddComponent<Shadow>();
            dropShadow.effectColor = shadow;
            dropShadow.effectDistance = new Vector2(0f, -shadowDepth);
        }
    }

    /// <summary>A pixel-style button: red with a dark red shadow when primary, dark with a border otherwise. Clicks audibly.</summary>
    public static Button CreateButton(Transform parent, string name, string label, bool primary, UnityAction onClick,
        float fontSize, out TextMeshProUGUI labelText)
    {
        UiTheme theme = Theme;
        Image image = primary
            ? CreateBox(parent, name, theme.red, Color.clear, 0f, theme.redShadow, 9f)
            : CreateBox(parent, name, theme.card, theme.border, 4f, theme.hardShadow, 9f);

        var button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.selectedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => Sfx.Play(SfxKind.Click));
        if (onClick != null)
            button.onClick.AddListener(onClick);

        labelText = CreateText(image.transform, "Label", fontSize, TextAlignmentOptions.Center, theme.paper, UiFont.Title);
        labelText.text = label;
        Stretch(labelText.rectTransform);
        return button;
    }

    public static UIPixelIcon CreateIcon(Transform parent, string name, PixelIcon icon, Color color, Color highlight)
    {
        var iconObject = new GameObject(name, typeof(RectTransform));
        iconObject.transform.SetParent(parent, false);
        var pixelIcon = iconObject.AddComponent<UIPixelIcon>();
        pixelIcon.Icon = icon;
        pixelIcon.color = color;
        pixelIcon.Highlight = highlight;
        pixelIcon.raycastTarget = false;
        return pixelIcon;
    }

    public static UIPolygon CreatePolygon(Transform parent, string name, Color color)
    {
        var polygonObject = new GameObject(name, typeof(RectTransform));
        polygonObject.transform.SetParent(parent, false);
        var polygon = polygonObject.AddComponent<UIPolygon>();
        polygon.color = color;
        polygon.raycastTarget = false;
        return polygon;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>Places a rect by its top-left corner, in pixels from the parent's top-left corner (y grows downward).</summary>
    public static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        Place(rect, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
