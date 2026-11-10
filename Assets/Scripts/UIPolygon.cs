using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A filled polygon drawn as a UI mesh: a trapezoid (pagoda roofs), a star (the saw blade) or a regular polygon.
/// Points are in 0..1 of the rect and are fanned from its center, so shapes must be visible from the center.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIPolygon : MaskableGraphic
{
    [SerializeField] private Vector2[] points = { new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f) };

    public void SetPoints(Vector2[] normalizedPoints)
    {
        points = normalizedPoints;
        SetVerticesDirty();
    }

    /// <summary>A trapezoid whose top edge is inset by <paramref name="topInset"/> of the width on each side.</summary>
    public void SetTrapezoid(float topInset)
    {
        SetPoints(new[] { new Vector2(0f, 0f), new Vector2(topInset, 1f), new Vector2(1f - topInset, 1f), new Vector2(1f, 0f) });
    }

    public void SetStar(int tips, float innerRatio)
    {
        var star = new Vector2[tips * 2];
        for (int i = 0; i < star.Length; i++)
        {
            float angle = Mathf.Deg2Rad * (90f - i * 180f / tips);
            float radius = (i % 2 == 0 ? 0.5f : 0.5f * innerRatio);
            star[i] = new Vector2(0.5f + Mathf.Cos(angle) * radius, 0.5f + Mathf.Sin(angle) * radius);
        }
        SetPoints(star);
    }

    public void SetRegular(int sides, float startDegrees = 90f)
    {
        var shape = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Deg2Rad * (startDegrees - i * 360f / sides);
            shape[i] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
        }
        SetPoints(shape);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Length < 3)
            return;

        Rect rect = GetPixelAdjustedRect();
        Color32 fill = color;
        vh.AddVert(rect.center, fill, Vector2.zero);
        foreach (Vector2 point in points)
            vh.AddVert(new Vector2(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height), fill, Vector2.zero);

        for (int i = 0; i < points.Length; i++)
            vh.AddTriangle(0, 1 + i, 1 + (i + 1) % points.Length);
    }
}
