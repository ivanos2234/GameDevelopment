using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pixel-art level select, built in code like the level HUD. Each card shows the level's enemy mix,
/// stars and best time; a level unlocks once the one before it has been cleared.
/// Call <see cref="Open"/> from a menu button.
/// </summary>
public sealed class LevelSelectMenu : MonoBehaviour
{
    private const int CardsPerRow = 5;
    private const float CardWidth = 312f;
    private const float TallCardHeight = 560f;
    private const float CompactCardHeight = 300f;
    private const float CardGap = 36f;

    [SerializeField] private LevelList levels;
    [SerializeField, Tooltip("Open every level regardless of progress, handy while testing.")]
    private bool unlockAll;
    [SerializeField, Tooltip("Show the TEST: UNLOCK ALL button (and the U key). Turn off for a release build.")]
    private bool showTestUnlock = true;

    private const string UnlockAllKey = "Debug.UnlockAll";

    private UiTheme theme;
    private RectTransform canvasRoot;
    private bool compact;
    private float cardHeight;
    private GameObject screen;

    public bool IsOpen => screen != null && screen.activeSelf;

    private void Awake()
    {
        theme = UiTheme.Current;
        RuntimeUi.EnsureEventSystem();
        BuildUi();
        screen.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
        else if (showTestUnlock && Keyboard.current.uKey.wasPressedThisFrame)
            ToggleTestUnlock();
    }

    private bool AllUnlocked => unlockAll || (showTestUnlock && PlayerPrefs.GetInt(UnlockAllKey, 0) == 1);

    /// <summary>Testing aid: opens every level (or locks them again) and remembers the choice.</summary>
    public void ToggleTestUnlock()
    {
        PlayerPrefs.SetInt(UnlockAllKey, PlayerPrefs.GetInt(UnlockAllKey, 0) == 1 ? 0 : 1);
        PlayerPrefs.Save();

        // Rebuild the cards with the new lock state and stay open.
        Destroy(canvasRoot.gameObject);
        BuildUi();
        screen.SetActive(true);
    }

    public void Open()
    {
        Sfx.Play(SfxKind.Click);
        screen.SetActive(true);
    }

    public void Close()
    {
        screen.SetActive(false);
    }

    private static void LoadLevel(string scene)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    // ---------- UI construction ----------

    private void BuildUi()
    {
        RectTransform root = RuntimeUi.CreateCanvas(transform, "Level Select Canvas", 50);
        canvasRoot = root;
        Image background = RuntimeUi.CreateImage(root, "Level Select", theme.night);
        RuntimeUi.Stretch(background.rectTransform);
        screen = background.gameObject;

        // A 1920x1080 stage in the middle; the background fills any extra space on wider screens.
        var stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
        stage.SetParent(background.transform, false);
        RuntimeUi.Place(stage, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
        stage.pivot = new Vector2(0.5f, 0.5f);

        BuildHeader(stage);

        int count = levels != null ? levels.Levels.Count : 0;
        // More than one row of levels: shorter cards with a smaller preview so two rows fit.
        compact = count > CardsPerRow;
        cardHeight = compact ? CompactCardHeight : TallCardHeight;
        for (int i = 0; i < count; i++)
        {
            int column = i % CardsPerRow;
            int row = i / CardsPerRow;
            int inRow = Mathf.Min(CardsPerRow, count - row * CardsPerRow);
            float rowWidth = inRow * CardWidth + (inRow - 1) * CardGap;
            float x = (1920f - rowWidth) / 2f + column * (CardWidth + CardGap);
            BuildCard(stage, i, x, 210f + row * (cardHeight + CardGap));
        }

        int rowCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)CardsPerRow));
        BuildLegend(stage, 210f + rowCount * (cardHeight + CardGap) + 24f);
    }

    private void BuildHeader(RectTransform stage)
    {
        Button back = RuntimeUi.CreateButton(stage, "Back Button", "  BACK", false, Close, 21f, out TextMeshProUGUI backLabel);
        RuntimeUi.PlaceTopLeft((RectTransform)back.transform, 108f, 72f, 220f, 72f);
        UIPixelIcon arrow = RuntimeUi.CreateIcon(back.transform, "Arrow", PixelIcon.ArrowLeft, theme.paper, theme.paper);
        RuntimeUi.Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(18f, 30f));
        backLabel.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI title = RuntimeUi.CreateText(stage, "Title", 48, TextAlignmentOptions.Center, theme.paper, UiFont.Title);
        title.text = "SELECT LEVEL";
        RuntimeUi.PlaceTopLeft(title.rectTransform, 460f, 80f, 1000f, 64f);
        UiTextShadow.Add(title, theme.red, new Vector2(6f, -6f));

        int earned = 0;
        int total = 0;
        if (levels != null)
        {
            foreach (LevelList.Entry entry in levels.Levels)
            {
                earned += LevelProgress.GetStars(entry.scene);
                total += 3;
            }
        }

        UIPixelIcon star = RuntimeUi.CreateIcon(stage, "Total Star", PixelIcon.Star, theme.gold, Color.white);
        RuntimeUi.PlaceTopLeft(star.rectTransform, 1640f, 88f, 36f, 36f);
        TextMeshProUGUI totals = RuntimeUi.CreateText(stage, "Total Stars", 45, TextAlignmentOptions.Left, theme.gold);
        totals.text = $"{earned} / {total}";
        RuntimeUi.PlaceTopLeft(totals.rectTransform, 1690f, 78f, 200f, 56f);
    }

    private void BuildCard(RectTransform stage, int index, float x, float y)
    {
        LevelList.Entry entry = levels.Levels[index];
        bool unlocked = AllUnlocked || levels.IsUnlocked(index);

        Image cardImage;
        if (unlocked)
        {
            Button cardButton = RuntimeUi.CreateButton(stage, $"Level {index + 1} Card", "", false, () => LoadLevel(entry.scene), 10f, out _);
            cardImage = cardButton.GetComponent<Image>();
        }
        else
        {
            cardImage = RuntimeUi.CreateBox(stage, $"Level {index + 1} Card", theme.lockedCard, theme.borderDim, 4f, Color.clear, 0f);
        }
        RuntimeUi.PlaceTopLeft(cardImage.rectTransform, x, y, CardWidth, cardHeight);
        Transform card = cardImage.transform;
        float inner = CardWidth - 54f;

        TextMeshProUGUI number = RuntimeUi.CreateText(card, "Number", 39, TextAlignmentOptions.TopLeft, theme.muted);
        number.text = $"LEVEL {index + 1}";
        RuntimeUi.PlaceTopLeft(number.rectTransform, 27f, 20f, inner, 44f);

        TextMeshProUGUI name = RuntimeUi.CreateText(card, "Title", 19, TextAlignmentOptions.TopLeft,
            unlocked ? theme.paper : theme.muted, UiFont.Title);
        name.text = entry.title.ToUpperInvariant();
        name.textWrappingMode = TextWrappingModes.Normal;
        name.lineSpacing = 20f;
        RuntimeUi.PlaceTopLeft(name.rectTransform, 27f, 72f, inner, 60f);

        if (!unlocked)
        {
            UIPixelIcon padlock = RuntimeUi.CreateIcon(card, "Lock", PixelIcon.Lock, theme.border, theme.border);
            float lockSize = compact ? 0.6f : 1f;
            RuntimeUi.PlaceTopLeft(padlock.rectTransform, (CardWidth - 72f * lockSize) / 2f, compact ? 128f : 230f, 72f * lockSize, 84f * lockSize);
            TextMeshProUGUI hint = RuntimeUi.CreateText(card, "Locked", 36, TextAlignmentOptions.Top, theme.muted);
            hint.text = $"Clear level {index}\nto unlock";
            RuntimeUi.PlaceTopLeft(hint.rectTransform, 27f, compact ? 190f : 340f, inner, 90f);
            return;
        }

        if (entry.killInOrder)
        {
            TextMeshProUGUI tag = RuntimeUi.CreateText(card, "Order Tag", 14, TextAlignmentOptions.TopRight, theme.gold, UiFont.Title);
            tag.text = "1-2-3";
            RuntimeUi.PlaceTopLeft(tag.rectTransform, 27f, 28f, inner, 24f);
        }

        float previewHeight = compact ? 60f : 150f;
        BuildPreview(card, entry, inner, previewHeight);

        int stars = LevelProgress.GetStars(entry.scene);
        for (int s = 0; s < 3; s++)
        {
            bool earned = s < stars;
            UIPixelIcon star = RuntimeUi.CreateIcon(card, $"Star {s + 1}", PixelIcon.Star,
                earned ? theme.gold : theme.borderDim, earned ? Color.white : theme.borderDim);
            RuntimeUi.PlaceTopLeft(star.rectTransform, 27f + s * 54f, compact ? 196f : 330f, 42f, 42f);
        }

        float best = LevelProgress.GetBestTime(entry.scene);
        TextMeshProUGUI bestLabel = RuntimeUi.CreateText(card, "Best Label", 36, TextAlignmentOptions.TopLeft, theme.muted);
        bestLabel.text = "BEST";
        RuntimeUi.PlaceTopLeft(bestLabel.rectTransform, 27f, compact ? 246f : 400f, inner, 40f);
        TextMeshProUGUI bestTime = RuntimeUi.CreateText(card, "Best Time", 36, compact ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft, theme.paper);
        bestTime.text = best >= 0f ? LevelManager.FormatTime(best) : "--:--.-";
        RuntimeUi.PlaceTopLeft(bestTime.rectTransform, 27f, compact ? 246f : 438f, inner, 40f);
    }

    // A mini arena: one colored square per enemy, so the card shows what the level throws at you.
    private void BuildPreview(Transform card, LevelList.Entry entry, float inner, float height)
    {
        Image arena = RuntimeUi.CreateBox(card, "Preview", theme.floor, theme.stone, 6f, Color.clear, 0f);
        RuntimeUi.PlaceTopLeft(arena.rectTransform, 27f, compact ? 120f : 150f, inner, height);

        const float square = 18f;
        const float gap = 9f;
        int perRow = Mathf.FloorToInt((inner - 30f + gap) / (square + gap));
        int total = 0;
        if (entry.enemyCounts != null)
            foreach (int c in entry.enemyCounts) total += c;
        int rows = Mathf.Max(1, Mathf.CeilToInt(total / (float)perRow));
        float top = (height - rows * square - (rows - 1) * gap) / 2f;

        int placed = 0;
        for (int kind = 0; entry.enemyCounts != null && kind < entry.enemyCounts.Length; kind++)
        {
            for (int n = 0; n < entry.enemyCounts[kind]; n++, placed++)
            {
                int row = placed / perRow;
                int inRow = Mathf.Min(perRow, total - row * perRow);
                float left = (inner - inRow * square - (inRow - 1) * gap) / 2f;
                Image dot = RuntimeUi.CreateImage(arena.transform, "Enemy", theme.EnemyColor((EnemyKind)kind));
                RuntimeUi.PlaceTopLeft(dot.rectTransform, left + (placed % perRow) * (square + gap), top + row * (square + gap), square, square);
                dot.raycastTarget = false;
            }
        }
    }

    private void BuildLegend(RectTransform stage, float top)
    {
        TextMeshProUGUI rules = RuntimeUi.CreateText(stage, "Star Rules", 36, TextAlignmentOptions.TopLeft, theme.soft);
        rules.text = "1 star: clear the level     2 stars: beat the par time     3 stars: beat the gold time";
        RuntimeUi.PlaceTopLeft(rules.rectTransform, 108f, top, 1700f, 44f);

        float x = 108f;
        foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
        {
            Image square = RuntimeUi.CreateImage(stage, $"{kind} Legend", theme.EnemyColor(kind));
            RuntimeUi.PlaceTopLeft(square.rectTransform, x, top + 66f, 18f, 18f);
            TextMeshProUGUI label = RuntimeUi.CreateText(stage, $"{kind} Label", 36, TextAlignmentOptions.TopLeft, theme.soft);
            label.text = kind.ToString().ToLowerInvariant();
            RuntimeUi.PlaceTopLeft(label.rectTransform, x + 30f, top + 52f, 160f, 44f);
            x += 175f;
        }

        if (showTestUnlock)
        {
            bool all = AllUnlocked;
            Button test = RuntimeUi.CreateButton(stage, "Test Unlock Button", all ? "TEST: LOCK AGAIN (U)" : "TEST: UNLOCK ALL (U)",
                false, ToggleTestUnlock, 16f, out TextMeshProUGUI testLabel);
            RuntimeUi.PlaceTopLeft((RectTransform)test.transform, 1392f, top + 112f, 420f, 60f);
            testLabel.color = theme.gold;
        }

        TextMeshProUGUI keys = RuntimeUi.CreateText(stage, "Keys", 33, TextAlignmentOptions.TopRight, theme.muted);
        keys.text = "Click a level  ·  Esc back";
        RuntimeUi.PlaceTopLeft(keys.rectTransform, 1212f, top + 54f, 600f, 44f);
    }
}
