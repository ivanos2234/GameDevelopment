using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The pixel-art main menu: a night dojo drawn from UI shapes (no sprites), the logo with a spinning saw,
/// and Play / Levels / Sound / Quit. Built in code like the HUD; scales to any screen from a 1920x1080 layout.
/// </summary>
public sealed class MainMenuScreen : MonoBehaviour
{
    [SerializeField] private MainMenu menu;
    [SerializeField] private LevelSelectMenu levelSelect;
    [SerializeField] private string version = "v0.4";

    private UiTheme theme;
    private RectTransform saw;
    private TextMeshProUGUI soundLabel;
    private TextMeshProUGUI difficultyLabel;
    private Button playButton;
    private readonly List<Image> lanterns = new();
    private readonly List<RectTransform> leaves = new();
    private readonly List<Vector2> leafVelocities = new();

    private void Awake()
    {
        theme = UiTheme.Current;
        RuntimeUi.EnsureEventSystem();
        Build();
    }

    private void Start()
    {
        // Keyboard and gamepad players start on Play.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        saw.localRotation *= Quaternion.Euler(0f, 0f, -300f * dt);

        for (int i = 0; i < lanterns.Count; i++)
            lanterns[i].color = UiTheme.WithAlpha(theme.heart, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f + i * 1.7f));

        for (int i = 0; i < leaves.Count; i++)
        {
            Vector2 position = leaves[i].anchoredPosition + leafVelocities[i] * dt;
            position.x += Mathf.Sin(Time.unscaledTime * 2f + i) * 20f * dt;
            if (position.y < -1000f)
                position = new Vector2(Random.Range(900f, 1900f), -150f);
            leaves[i].anchoredPosition = position;
        }

        if (!levelSelect.IsOpen && Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            levelSelect.Open();
    }

    private void OpenLevels() => levelSelect.Open();

    private void ToggleSound()
    {
        Sfx.Muted = !Sfx.Muted;
        soundLabel.text = SoundText;
    }

    private static string SoundText => Sfx.Muted ? "SOUND: OFF" : "SOUND: ON";

    private static string DifficultyText => Difficulty.Level == DifficultyLevel.Easy ? "EASY" : "NORMAL";

    private void ToggleDifficulty()
    {
        Difficulty.Level = Difficulty.Level == DifficultyLevel.Easy ? DifficultyLevel.Normal : DifficultyLevel.Easy;
        difficultyLabel.text = DifficultyText;
    }

    // ---------- construction ----------

    private void Build()
    {
        RectTransform root = RuntimeUi.CreateCanvas(transform, "Menu Canvas", 10);
        Image backdrop = RuntimeUi.CreateImage(root, "Backdrop", theme.night);
        RuntimeUi.Stretch(backdrop.rectTransform);

        var stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
        stage.SetParent(root, false);
        RuntimeUi.Place(stage, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
        stage.pivot = new Vector2(0.5f, 0.5f);

        BuildSky(stage);
        BuildMountains(stage);
        BuildPagoda(stage);
        BuildMaple(stage);
        BuildLogo(stage);
        BuildButtons(stage);

        TextMeshProUGUI keys = RuntimeUi.CreateText(stage, "Keys", 36, TextAlignmentOptions.TopLeft, theme.muted);
        keys.text = "WASD move  ·  Esc pause  ·  R restart  ·  M sound";
        RuntimeUi.PlaceTopLeft(keys.rectTransform, 144f, 1000f, 1000f, 46f);
        TextMeshProUGUI versionText = RuntimeUi.CreateText(stage, "Version", 36, TextAlignmentOptions.TopRight, theme.muted);
        versionText.text = version;
        RuntimeUi.PlaceTopLeft(versionText.rectTransform, 1600f, 1000f, 248f, 46f);
    }

    private Image Rect(Transform parent, string name, Color color, float x, float y, float width, float height)
    {
        Image image = RuntimeUi.CreateImage(parent, name, color);
        RuntimeUi.PlaceTopLeft(image.rectTransform, x, y, width, height);
        image.raycastTarget = false;
        return image;
    }

    private void BuildSky(RectTransform stage)
    {
        float[,] stars =
        {
            { 780, 90, 6 }, { 915, 225, 6 }, { 1050, 135, 9 }, { 1170, 315, 6 }, { 1560, 75, 6 },
            { 1770, 165, 9 }, { 690, 345, 6 }, { 990, 450, 6 }, { 1380, 260, 6 }, { 600, 160, 6 },
        };
        for (int i = 0; i < stars.GetLength(0); i++)
            Rect(stage, "Star", UiTheme.WithAlpha(theme.paper, 0.4f + 0.1f * (i % 4)), stars[i, 0], stars[i, 1], stars[i, 2], stars[i, 2]);

        UIPixelIcon moon = RuntimeUi.CreateIcon(stage, "Moon", PixelIcon.Moon, UiTheme.Hex("F2E6C8"), UiTheme.Hex("E3D5B0"));
        RuntimeUi.PlaceTopLeft(moon.rectTransform, 1260f, 96f, 192f, 192f);
    }

    // Stepped silhouettes, built from rectangles: each step is (start %, end %, top % of the layer).
    private void BuildMountains(RectTransform stage)
    {
        float[,] back =
        {
            { 0, 8, 40 }, { 8, 16, 30 }, { 16, 24, 18 }, { 24, 34, 28 }, { 34, 42, 12 }, { 42, 50, 4 },
            { 50, 60, 16 }, { 60, 70, 24 }, { 70, 78, 10 }, { 78, 86, 0 }, { 86, 94, 14 }, { 94, 100, 26 },
        };
        float[,] front =
        {
            { 0, 12, 30 }, { 12, 22, 18 }, { 22, 36, 36 }, { 36, 48, 22 }, { 48, 58, 40 }, { 58, 66, 26 }, { 66, 100, 12 },
        };
        Steps(stage, "Far Hills", back, 540f, 540f, UiTheme.Hex("141A2E"));
        Steps(stage, "Near Hills", front, 705f, 375f, UiTheme.Hex("19213A"));
        Rect(stage, "Ground", theme.bar, 0f, 975f, 1920f, 105f);
    }

    private void Steps(RectTransform stage, string name, float[,] steps, float layerTop, float layerHeight, Color color)
    {
        for (int i = 0; i < steps.GetLength(0); i++)
        {
            float x0 = steps[i, 0] / 100f * 1920f;
            float x1 = steps[i, 1] / 100f * 1920f;
            float top = layerTop + steps[i, 2] / 100f * layerHeight;
            Rect(stage, name, color, x0, top, x1 - x0 + 1f, layerTop + layerHeight - top);
        }
    }

    private void BuildPagoda(RectTransform stage)
    {
        Color dark = theme.bar;
        Color body = UiTheme.Hex("111629");
        Rect(stage, "Rock", dark, 1290f, 840f, 630f, 240f);

        Rect(stage, "Tier 1", body, 1440f, 705f, 270f, 135f);
        Rect(stage, "Window", theme.gold, 1479f, 750f, 21f, 30f);
        Rect(stage, "Window", theme.gold, 1564f, 750f, 21f, 30f);
        Rect(stage, "Window", UiTheme.WithAlpha(theme.gold, 0.6f), 1650f, 750f, 21f, 30f);
        Roof(stage, 1374f, 657f, 402f, 54f, 0.1f, dark);

        Rect(stage, "Tier 2", body, 1479f, 558f, 192f, 99f);
        Rect(stage, "Window", theme.gold, 1515f, 594f, 18f, 27f);
        Rect(stage, "Window", UiTheme.WithAlpha(theme.gold, 0.6f), 1617f, 594f, 18f, 27f);
        Roof(stage, 1416f, 513f, 318f, 48f, 0.12f, dark);

        Rect(stage, "Tier 3", body, 1518f, 438f, 114f, 78f);
        Roof(stage, 1461f, 399f, 228f, 42f, 0.16f, dark);
        Rect(stage, "Spire", dark, 1569f, 333f, 12f, 69f);

        foreach (Vector2 lantern in new[] { new Vector2(1395f, 711f), new Vector2(1740f, 711f), new Vector2(1440f, 561f), new Vector2(1695f, 561f) })
            lanterns.Add(Rect(stage, "Lantern", theme.heart, lantern.x, lantern.y, 15f, 21f));
    }

    private void Roof(RectTransform stage, float x, float y, float width, float height, float inset, Color color)
    {
        UIPolygon roof = RuntimeUi.CreatePolygon(stage, "Roof", color);
        roof.SetTrapezoid(inset);
        RuntimeUi.PlaceTopLeft(roof.rectTransform, x, y, width, height);
    }

    private void BuildMaple(RectTransform stage)
    {
        Color deep = UiTheme.Hex("8E2A2E");
        Color mid = UiTheme.Hex("B83A3A");
        Rect(stage, "Maple", deep, 1725f, 225f, 72f, 48f);
        Rect(stage, "Maple", mid, 1773f, 189f, 96f, 60f);
        Rect(stage, "Maple", theme.heart, 1821f, 249f, 99f, 72f);
        Rect(stage, "Maple", mid, 1749f, 297f, 84f, 48f);
        Rect(stage, "Maple", deep, 1845f, 345f, 75f, 60f);
        Rect(stage, "Maple", theme.heart, 1794f, 393f, 60f, 36f);

        for (int i = 0; i < 9; i++)
        {
            Image leaf = Rect(stage, "Leaf", i % 2 == 0 ? theme.heart : mid, 0f, 0f, 12f, 12f);
            leaf.rectTransform.anchoredPosition = new Vector2(Random.Range(900f, 1900f), -Random.Range(150f, 950f));
            leaves.Add(leaf.rectTransform);
            leafVelocities.Add(new Vector2(Random.Range(-40f, -15f), Random.Range(-70f, -35f)));
        }
    }

    private void BuildLogo(RectTransform stage)
    {
        var sawRoot = (RectTransform)new GameObject("Saw", typeof(RectTransform)).transform;
        sawRoot.SetParent(stage, false);
        RuntimeUi.PlaceTopLeft(sawRoot, 144f, 135f, 156f, 156f);
        sawRoot.pivot = new Vector2(0.5f, 0.5f);
        sawRoot.anchoredPosition += new Vector2(78f, -78f);
        saw = sawRoot;

        UIPolygon blade = RuntimeUi.CreatePolygon(sawRoot, "Blade", UiTheme.Hex("C9CFDB"));
        blade.SetStar(12, 0.7f);
        RuntimeUi.Stretch(blade.rectTransform);
        UIPolygon hub = RuntimeUi.CreatePolygon(sawRoot, "Hub", UiTheme.Hex("5C6378"));
        hub.SetRegular(8);
        RuntimeUi.Place(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
        UIPolygon pin = RuntimeUi.CreatePolygon(sawRoot, "Pin", theme.heart);
        pin.SetRegular(6);
        RuntimeUi.Place(pin.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

        foreach ((string word, float y) in new[] { ("NINJA", 132f), ("SPINNER", 240f) })
        {
            TextMeshProUGUI text = RuntimeUi.CreateText(stage, word, 84, TextAlignmentOptions.TopLeft, theme.paper, UiFont.Title);
            text.text = word;
            RuntimeUi.PlaceTopLeft(text.rectTransform, 342f, y, 800f, 96f);
            UiTextShadow.Add(text, theme.red, new Vector2(9f, -9f));
        }

        TextMeshProUGUI tagline = RuntimeUi.CreateText(stage, "Tagline", 48, TextAlignmentOptions.TopLeft, theme.soft);
        tagline.text = "Spin the blade. Clear the dojo.";
        RuntimeUi.PlaceTopLeft(tagline.rectTransform, 144f, 368f, 900f, 56f);
    }

    private void BuildButtons(RectTransform stage)
    {
        playButton = MenuButton(stage, "PLAY", true, menu.PlayGame, 0, 570f, 0f, out _);
        UIPixelIcon arrow = RuntimeUi.CreateIcon(playButton.transform, "Arrow", PixelIcon.ArrowRight, theme.paper, theme.paper);
        RuntimeUi.Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(21f, 33f));

        MenuButton(stage, "LEVELS", false, OpenLevels, 1, 570f, 0f, out _);
        MenuButton(stage, "TUTORIAL", false, menu.PlayTutorial, 2, 570f, 0f, out _);
        MenuButton(stage, DifficultyText, false, ToggleDifficulty, 3, 279f, 0f, out difficultyLabel);
        MenuButton(stage, SoundText, false, ToggleSound, 3, 279f, 291f, out soundLabel);
        MenuButton(stage, "QUIT", false, menu.QuitGame, 4, 570f, 0f, out _);
        difficultyLabel.margin = soundLabel.margin = new Vector4(0f, 0f, 0f, 0f);
        difficultyLabel.alignment = soundLabel.alignment = TextAlignmentOptions.Center;
        difficultyLabel.fontSize = soundLabel.fontSize = 21f;
    }

    private Button MenuButton(RectTransform stage, string label, bool primary, UnityEngine.Events.UnityAction onClick, int row,
        float width, float x, out TextMeshProUGUI text)
    {
        Button button = RuntimeUi.CreateButton(stage, label + " Button", label, primary, onClick, 30f, out text);
        RuntimeUi.PlaceTopLeft((RectTransform)button.transform, 144f + x, 470f + row * 104f, width, 82f);
        text.alignment = TextAlignmentOptions.Left;
        text.margin = new Vector4(90f, 0f, 0f, 0f);
        return button;
    }
}
