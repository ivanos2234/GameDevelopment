using TMPro;
using UnityEngine;

public enum EnemyKind
{
    Dummy,
    Chaser,
    Shooter,
    Gunner,
    Turret,
    Master,
}

/// <summary>
/// The game's look in one place: the two pixel fonts and the palette of the "pixel-art dojo" style.
/// Lives at Resources/UiTheme so code-built screens (menu, HUD, level select) can find it.
/// </summary>
[CreateAssetMenu(menuName = "Ninja Spinner/UI Theme", fileName = "UiTheme")]
public sealed class UiTheme : ScriptableObject
{
    [Header("Fonts")]
    [Tooltip("Press Start 2P: titles, buttons and numbers.")]
    public TMP_FontAsset titleFont;
    [Tooltip("VT323: labels, tips and body text.")]
    public TMP_FontAsset bodyFont;

    [Header("Ground and panels")]
    public Color night = Hex("0E1220");
    public Color bar = Hex("0A0D18");
    public Color barLine = Hex("232A3D");
    public Color panel = Hex("141827");
    public Color card = Hex("161C2E");
    public Color lockedCard = Hex("10141F");
    public Color border = Hex("3A4360");
    public Color borderDim = Hex("2A3147");
    public Color hardShadow = Hex("070A12");
    public Color floor = Hex("1E2436");
    public Color stone = Hex("A3ABBE");

    [Header("Text")]
    public Color paper = Hex("F2EFE6");
    public Color muted = Hex("8A93A8");
    public Color soft = Hex("A3ABBE");

    [Header("Accents")]
    public Color red = Hex("C93A3F");
    public Color redShadow = Hex("6E1C20");
    public Color heart = Hex("E5484D");
    public Color heartHighlight = Hex("FF9EA1");
    public Color gold = Hex("F2C94C");
    public Color goldShadow = Hex("6B5313");
    public Color health = Hex("4ADE80");

    [Header("Enemy color code (rim, death burst, HUD)")]
    public Color dummy = Hex("8A93A8");
    public Color chaser = Hex("E5484D");
    public Color shooter = Hex("F28C28");
    public Color gunner = Hex("4EA8FF");
    public Color turret = Hex("A970FF");
    public Color master = Hex("E8B84A");
    [Tooltip("The Master's shield: its body and the beam that ties it to the Master.")]
    public Color guardian = Hex("3CCFBF");

    private static UiTheme current;

    public static UiTheme Current
    {
        get
        {
            if (current == null)
                current = Resources.Load<UiTheme>("UiTheme");
            if (current == null)
                current = CreateInstance<UiTheme>();
            return current;
        }
    }

    public Color EnemyColor(EnemyKind kind) => kind switch
    {
        EnemyKind.Chaser => chaser,
        EnemyKind.Shooter => shooter,
        EnemyKind.Gunner => gunner,
        EnemyKind.Turret => turret,
        EnemyKind.Master => master,
        _ => dummy,
    };

    public static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.magenta;
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
