using UnityEngine;
using UnityEngine.UI;

public enum PixelIcon
{
    Heart,
    Star,
    ArrowRight,
    ArrowLeft,
    Lock,
    Moon,
}

/// <summary>
/// Small pixel-art icons drawn from text patterns as a UI mesh, so the HUD and menus need no sprites.
/// In a pattern '#' is the main color, 'o' the highlight color and '.' is empty.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIPixelIcon : MaskableGraphic
{
    private static readonly string[] HeartPattern =
    {
        ".##.##.",
        "#o#####",
        "#######",
        ".#####.",
        "..###..",
        "...#...",
    };

    private static readonly string[] StarPattern =
    {
        "...#...",
        "..###..",
        "#######",
        ".#####.",
        "..###..",
        ".##.##.",
        "##...##",
    };

    private static readonly string[] ArrowRightPattern = { "#..", "##.", "###", "##.", "#.." };
    private static readonly string[] ArrowLeftPattern = { "..#", ".##", "###", ".##", "..#" };

    private static readonly string[] LockPattern =
    {
        ".####.",
        ".#..#.",
        ".#..#.",
        "######",
        "##.###",
        "##.###",
        "######",
    };

    private static readonly string[] MoonPattern =
    {
        "...####...",
        ".########.",
        ".#o######.",
        "##o#######",
        "##########",
        "#######o##",
        "##########",
        ".####o###.",
        ".########.",
        "...####...",
    };

    [SerializeField] private PixelIcon icon = PixelIcon.Heart;
    [SerializeField] private Color highlight = Color.white;

    public PixelIcon Icon
    {
        get => icon;
        set { icon = value; SetVerticesDirty(); }
    }

    public Color Highlight
    {
        get => highlight;
        set { highlight = value; SetVerticesDirty(); }
    }

    private static string[] Pattern(PixelIcon icon) => icon switch
    {
        PixelIcon.Star => StarPattern,
        PixelIcon.ArrowRight => ArrowRightPattern,
        PixelIcon.ArrowLeft => ArrowLeftPattern,
        PixelIcon.Lock => LockPattern,
        PixelIcon.Moon => MoonPattern,
        _ => HeartPattern,
    };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        string[] rows = Pattern(icon);
        Rect rect = GetPixelAdjustedRect();
        float cellWidth = rect.width / rows[0].Length;
        float cellHeight = rect.height / rows.Length;

        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                char cell = rows[y][x];
                if (cell == '.')
                    continue;

                Color32 cellColor = cell == 'o' ? highlight * color : color;
                float left = rect.xMin + x * cellWidth;
                float top = rect.yMax - y * cellHeight;
                AddQuad(vh, new Vector2(left, top - cellHeight), new Vector2(left + cellWidth, top), cellColor);
            }
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color32 quadColor)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(min.x, min.y), quadColor, Vector2.zero);
        vh.AddVert(new Vector3(min.x, max.y), quadColor, Vector2.zero);
        vh.AddVert(new Vector3(max.x, max.y), quadColor, Vector2.zero);
        vh.AddVert(new Vector3(max.x, min.y), quadColor, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
