using UnityEngine;
using UnityEngine.UI;

/// <summary>A five-pointed star drawn as a UI mesh. The game font has no star glyph, and this needs no sprite.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIStar : MaskableGraphic
{
    [SerializeField, Range(0.2f, 0.8f)] private float innerRatio = 0.45f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        float inner = outer * innerRatio;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        vh.AddVert(vertex);

        // Ten rim points alternating between the tips and the inner corners, starting at the top tip.
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.Deg2Rad * (90f + i * 36f);
            float radius = i % 2 == 0 ? outer : inner;
            vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vh.AddVert(vertex);
        }

        // Clockwise, like Unity's own UI quads, so the triangles are not culled as back faces.
        for (int i = 0; i < 10; i++)
            vh.AddTriangle(0, 1 + (i + 1) % 10, 1 + i);
    }
}
