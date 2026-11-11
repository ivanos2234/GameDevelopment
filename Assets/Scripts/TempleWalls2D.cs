using UnityEngine;

/// <summary>
/// Dresses the arena edge as a Chinese temple courtyard: a hall with red pillars, lattice windows, lanterns and a
/// green tiled roof along the top, tiled roofs along the sides and bottom, and pagoda gate towers in the corners.
/// The pixel art is drawn in code at startup; the stone wall colliders stay as they are, only their sprites are hidden.
/// Put it on the level's "Arena" object, whose children are the Wall5 (top/bottom) and Wall6 (side) pieces.
/// </summary>
public sealed class TempleWalls2D : MonoBehaviour
{
    private const int PixelsPerUnit = 32;
    private const float SegmentWidth = 2.5f;

    [SerializeField, Tooltip("Sorting order of the buildings; characters draw above 1.")]
    private int sortingOrder;

    private static Sprite facade, bottomRoof, leftRoof, rightRoof, tower, cornerCap;

    // Palette.
    private static readonly Color32 RoofDark = Hex(0x1F3D3A), RoofMid = Hex(0x2F5D57), RoofLight = Hex(0x4A8A80), Ridge = Hex(0x16302D);
    private static readonly Color32 Gold = Hex(0xC9A14A), Red = Hex(0xB83A3A), RedDark = Hex(0x7A1E22);
    private static readonly Color32 Plaster = Hex(0xE8E0D0), PlasterShade = Hex(0xCFC6B4);
    private static readonly Color32 Stone = Hex(0x5C6378), StoneLight = Hex(0x8A93A8), Lattice = Hex(0x3A2A20), Lantern = Hex(0xE5484D);
    private static readonly Color32 Clear = new(0, 0, 0, 0);

    private void Awake()
    {
        float halfWidth = 0f, halfHeight = 0f;
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Wall6")) halfWidth = Mathf.Max(halfWidth, Mathf.Abs(child.position.x));
            if (child.name.StartsWith("Wall5")) halfHeight = Mathf.Max(halfHeight, Mathf.Abs(child.position.y));
            foreach (SpriteRenderer stone in child.GetComponentsInChildren<SpriteRenderer>())
                stone.enabled = false;
        }
        if (halfWidth <= 0f || halfHeight <= 0f)
            return;

        BuildSprites();

        int segments = Mathf.CeilToInt(halfWidth * 2f / SegmentWidth) + 1;
        float startX = -(segments - 1) * SegmentWidth / 2f;
        for (int i = 0; i < segments; i++)
        {
            float x = startX + i * SegmentWidth;
            Place(facade, new Vector2(x, halfHeight - 0.65f), "Hall");
            Place(bottomRoof, new Vector2(x, -halfHeight), "Roof");
        }

        int sideSegments = Mathf.CeilToInt(halfHeight * 2f / SegmentWidth) + 1;
        float startY = -(sideSegments - 1) * SegmentWidth / 2f;
        for (int i = 0; i < sideSegments; i++)
        {
            float y = startY + i * SegmentWidth;
            Place(leftRoof, new Vector2(-halfWidth, y), "Side Roof");
            Place(rightRoof, new Vector2(halfWidth, y), "Side Roof");
        }

        Place(tower, new Vector2(-halfWidth, halfHeight - 0.9f), "Gate Tower", 1);
        Place(tower, new Vector2(halfWidth, halfHeight - 0.9f), "Gate Tower", 1);
        Place(cornerCap, new Vector2(-halfWidth, -halfHeight), "Corner", 1);
        Place(cornerCap, new Vector2(halfWidth, -halfHeight), "Corner", 1);
    }

    private void Place(Sprite sprite, Vector2 position, string name, int orderOffset = 0)
    {
        var piece = new GameObject(name);
        piece.transform.SetParent(transform, false);
        piece.transform.position = position;
        var renderer = piece.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder + orderOffset;
    }

    // ---------- pixel art ----------

    private static void BuildSprites()
    {
        if (facade != null)
            return;

        facade = MakeSprite(DrawFacade(), new Vector2(0.5f, 0f));
        bottomRoof = MakeSprite(DrawBottomRoof(), new Vector2(0.5f, 0.5f));
        leftRoof = MakeSprite(DrawSideRoof(true), new Vector2(0.5f, 0.5f));
        rightRoof = MakeSprite(DrawSideRoof(false), new Vector2(0.5f, 0.5f));
        tower = MakeSprite(DrawTower(), new Vector2(0.5f, 0f));
        cornerCap = MakeSprite(DrawCornerCap(), new Vector2(0.5f, 0.5f));
    }

    private static Sprite MakeSprite(Texture2D texture, Vector2 pivot)
    {
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, PixelsPerUnit);
    }

    private static Texture2D Canvas(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
        texture.SetPixels32(pixels);
        return texture;
    }

    private static void Fill(Texture2D t, int x0, int y0, int x1, int y1, Color32 color)
    {
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(t.height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(t.width - 1, x1); x++)
                t.SetPixel(x, y, color);
    }

    private static Texture2D DrawFacade()
    {
        var t = Canvas(80, 64);
        Fill(t, 0, 0, 79, 5, Stone);
        Fill(t, 0, 5, 79, 5, StoneLight);
        Fill(t, 0, 6, 79, 33, Plaster);
        Fill(t, 0, 6, 79, 7, PlasterShade);

        foreach (int left in new[] { 9, 47 })
        {
            Fill(t, left - 1, 10, left + 23, 27, Red);
            Fill(t, left, 11, left + 22, 26, Lattice);
            for (int x = left; x <= left + 22; x++)
                for (int y = 11; y <= 26; y++)
                    if ((x - left) % 4 == 0 || (y - 11) % 4 == 0) t.SetPixel(x, y, Gold);
        }

        foreach (int pillar in new[] { 0, 37, 75 })
        {
            Fill(t, pillar, 6, pillar + 4, 35, Red);
            Fill(t, pillar + 4, 6, pillar + 4, 35, RedDark);
            Fill(t, pillar, 6, pillar + 4, 7, Gold);
        }

        Fill(t, 0, 34, 79, 37, Red);
        Fill(t, 0, 36, 79, 36, Gold);

        foreach (int lantern in new[] { 20, 58 })
        {
            Fill(t, lantern + 2, 32, lantern + 2, 33, RedDark);
            Fill(t, lantern, 26, lantern + 4, 31, Lantern);
            Fill(t, lantern, 31, lantern + 4, 31, Gold);
            Fill(t, lantern, 26, lantern + 4, 26, Gold);
        }

        TileEnds(t, 0, 38, 79, 41, false);
        BarrelTiles(t, 0, 42, 79, 59, false);
        Fill(t, 0, 60, 79, 63, Ridge);
        for (int x = 4; x < 80; x += 10) Fill(t, x, 61, x + 1, 62, Gold);
        return t;
    }

    // Barrel tiles: each 4 px column is a rounded tile (dark edge, light crown), broken into courses every 6 px.
    private static void BarrelTiles(Texture2D t, int x0, int y0, int x1, int y1, bool vertical)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int across = vertical ? y - y0 : x - x0;
                int along = vertical ? x - x0 : y - y0;
                Color32 c = (across % 4) switch { 0 => RoofDark, 1 => RoofLight, _ => RoofMid };
                if (along % 6 == 5) c = RoofDark;
                t.SetPixel(x, y, c);
            }
    }

    // The round ends of the tile columns along an eave, with a gold trim line.
    private static void TileEnds(Texture2D t, int x0, int y0, int x1, int y1, bool vertical)
    {
        Fill(t, x0, y0, x1, y1, RoofDark);
        for (int i = vertical ? y0 : x0; i <= (vertical ? y1 : x1); i += 4)
        {
            if (vertical) Fill(t, x0 + 1, i + 1, x1 - 1, i + 2, RoofLight);
            else Fill(t, i + 1, y0 + 1, i + 2, y1 - 1, RoofLight);
        }
    }

    // Bottom edge: a red courtyard wall facing the arena, capped with a tiled roof.
    private static Texture2D DrawBottomRoof()
    {
        var t = Canvas(80, 40);
        Fill(t, 0, 0, 79, 2, Ridge);
        for (int x = 4; x < 80; x += 10) Fill(t, x, 1, x + 1, 1, Gold);
        BarrelTiles(t, 0, 3, 79, 19, false);
        TileEnds(t, 0, 20, 79, 23, false);
        Fill(t, 0, 24, 79, 24, Gold);
        Fill(t, 0, 25, 79, 37, Red);
        Fill(t, 0, 25, 79, 26, RedDark);
        for (int x = 3; x < 80; x += 8) Fill(t, x, 31, x + 1, 32, Gold);
        foreach (int post in new[] { 0, 39 }) Fill(t, post, 25, post + 2, 37, RedDark);
        Fill(t, 0, 38, 79, 39, Stone);
        return t;
    }

    // Side edges: the same wall seen from above, ridge on the outer side and the red wall facing the arena.
    private static Texture2D DrawSideRoof(bool left)
    {
        var t = Canvas(32, 80);
        int Mirror(int x) => left ? x : 31 - x;
        void Column(int x0, int x1, Color32 color)
        {
            int a = Mirror(x0), b = Mirror(x1);
            Fill(t, Mathf.Min(a, b), 0, Mathf.Max(a, b), 79, color);
        }

        Column(0, 2, Ridge);
        BarrelTiles(t, left ? 3 : 13, 0, left ? 18 : 28, 79, true);
        int ends = left ? 19 : 9;
        TileEnds(t, ends, 0, ends + 3, 79, true);
        Column(23, 23, Gold);
        Column(24, 30, Red);
        Column(24, 24, RedDark);
        for (int y = 3; y < 80; y += 8) { int x = Mirror(27); Fill(t, x, y, x, y + 1, Gold); }
        Column(31, 31, Stone);
        for (int y = 4; y < 80; y += 10) { int x = Mirror(1); Fill(t, x, y, x, y + 1, Gold); }
        return t;
    }

    private static Texture2D DrawTower()
    {
        var t = Canvas(72, 96);
        Fill(t, 8, 0, 63, 7, Stone);
        Fill(t, 8, 7, 63, 7, StoneLight);

        Fill(t, 10, 8, 61, 43, Plaster);
        Fill(t, 10, 8, 14, 43, Red); Fill(t, 57, 8, 61, 43, Red);
        Fill(t, 28, 8, 43, 30, RedDark);
        Fill(t, 30, 8, 41, 28, Lattice);
        Fill(t, 30, 28, 41, 28, Gold);
        Fill(t, 20, 30, 24, 36, Lantern); Fill(t, 47, 30, 51, 36, Lantern);

        // Lower roof with upturned corners.
        Fill(t, 2, 44, 69, 46, RoofDark);
        BarrelTiles(t, 4, 47, 67, 57, false);
        Fill(t, 0, 44, 3, 49, RoofDark); Fill(t, 68, 44, 71, 49, RoofDark);
        Fill(t, 0, 48, 1, 51, Gold); Fill(t, 70, 48, 71, 51, Gold);
        Fill(t, 4, 58, 67, 59, Ridge);

        Fill(t, 18, 60, 53, 73, Plaster);
        Fill(t, 18, 60, 21, 73, Red); Fill(t, 50, 60, 53, 73, Red);
        Fill(t, 30, 63, 41, 70, Lattice);

        // Upper roof.
        Fill(t, 8, 74, 63, 75, RoofDark);
        BarrelTiles(t, 10, 76, 61, 85, false);
        Fill(t, 6, 74, 9, 79, RoofDark); Fill(t, 62, 74, 65, 79, RoofDark);
        Fill(t, 6, 78, 7, 81, Gold); Fill(t, 64, 78, 65, 81, Gold);
        Fill(t, 10, 86, 61, 87, Ridge);

        Fill(t, 34, 88, 37, 95, Gold);
        Fill(t, 33, 91, 38, 92, Gold);
        return t;
    }

    private static Texture2D DrawCornerCap()
    {
        var t = Canvas(48, 48);
        BarrelTiles(t, 2, 2, 45, 45, false);
        Fill(t, 0, 0, 47, 3, Ridge); Fill(t, 0, 44, 47, 47, Ridge);
        Fill(t, 0, 0, 3, 47, Ridge); Fill(t, 44, 0, 47, 47, Ridge);
        Fill(t, 20, 20, 27, 27, Gold);
        Fill(t, 22, 22, 25, 25, Red);
        return t;
    }

    private static Color32 Hex(int rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
}
