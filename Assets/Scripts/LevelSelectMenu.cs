using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level select screen for the main menu, built in code like the level HUD. Each card shows the
/// stars and best time for a level; a level unlocks once the one before it has been cleared.
/// Hook a menu button's OnClick to <see cref="Open"/>.
/// </summary>
public sealed class LevelSelectMenu : MonoBehaviour
{
    private const int CardsPerRow = 5;
    private const float CardWidth = 280f;
    private const float CardHeight = 330f;
    private const float CardGap = 28f;

    [SerializeField] private LevelList levels;
    [SerializeField, Tooltip("Open every level regardless of progress, handy while testing.")]
    private bool unlockAll;

    [Header("Colors")]
    [SerializeField] private Color textColor = new(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color accentColor = new(1f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color panelColor = new(0.08f, 0.09f, 0.12f, 0.97f);
    [SerializeField] private Color cardColor = new(0.22f, 0.25f, 0.32f, 1f);
    [SerializeField] private Color lockedCardColor = new(0.13f, 0.14f, 0.18f, 1f);
    [SerializeField] private Color starOffColor = new(0.32f, 0.34f, 0.4f, 1f);

    private GameObject screen;

    public bool IsOpen => screen != null && screen.activeSelf;

    private void Awake()
    {
        RuntimeUi.EnsureEventSystem();
        BuildUi();
        screen.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
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

        Image dim = RuntimeUi.CreateImage(root, "Level Select", new Color(0f, 0f, 0f, 0.8f));
        RuntimeUi.Stretch(dim.rectTransform);
        screen = dim.gameObject;

        int count = levels != null ? levels.Levels.Count : 0;
        int columns = Mathf.Clamp(count, 1, CardsPerRow);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)CardsPerRow));
        float rowWidth = columns * CardWidth + (columns - 1) * CardGap;
        const float cardsTop = 150f;
        float boxHeight = cardsTop + rows * (CardHeight + CardGap) + 140f;

        Image box = RuntimeUi.CreateImage(dim.transform, "Box", panelColor);
        RuntimeUi.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(rowWidth + 120f, boxHeight));

        TextMeshProUGUI title = RuntimeUi.CreateText(box.transform, "Title", 64, TextAlignmentOptions.Center, textColor);
        title.text = "SELECT LEVEL";
        title.fontStyle = FontStyles.Bold;
        RuntimeUi.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(rowWidth, 90f));

        for (int i = 0; i < count; i++)
        {
            int column = i % CardsPerRow;
            int row = i / CardsPerRow;
            int inRow = Mathf.Min(CardsPerRow, count - row * CardsPerRow);
            float rowStart = -(inRow * CardWidth + (inRow - 1) * CardGap) / 2f + CardWidth / 2f;
            var position = new Vector2(rowStart + column * (CardWidth + CardGap), -cardsTop - row * (CardHeight + CardGap));
            BuildCard(box.transform, i, position);
        }

        Button back = RuntimeUi.CreateButton(box.transform, "Back Button", cardColor, Close);
        RuntimeUi.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 76f));
        TextMeshProUGUI backLabel = RuntimeUi.CreateText(back.transform, "Label", 36, TextAlignmentOptions.Center, textColor);
        backLabel.text = "Back (Esc)";
        RuntimeUi.Stretch(backLabel.rectTransform);
    }

    private void BuildCard(Transform parent, int index, Vector2 position)
    {
        LevelList.Entry entry = levels.Levels[index];
        bool unlocked = unlockAll || levels.IsUnlocked(index);
        int stars = LevelProgress.GetStars(entry.scene);
        float best = LevelProgress.GetBestTime(entry.scene);
        Color dimText = new(textColor.r, textColor.g, textColor.b, 0.6f);

        Button card = RuntimeUi.CreateButton(parent, $"Level {index + 1} Card", unlocked ? cardColor : lockedCardColor,
            () => LoadLevel(entry.scene));
        card.interactable = unlocked;
        RuntimeUi.Place((RectTransform)card.transform, new Vector2(0.5f, 1f), position, new Vector2(CardWidth, CardHeight));

        TextMeshProUGUI number = RuntimeUi.CreateText(card.transform, "Number", 28, TextAlignmentOptions.Center, dimText);
        number.text = $"LEVEL {index + 1}";
        RuntimeUi.Place(number.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(CardWidth - 30f, 40f));

        TextMeshProUGUI name = RuntimeUi.CreateText(card.transform, "Title", 34, TextAlignmentOptions.Center,
            unlocked ? textColor : dimText);
        name.text = entry.title;
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.Normal;
        RuntimeUi.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(CardWidth - 30f, 90f));

        if (!unlocked)
        {
            TextMeshProUGUI locked = RuntimeUi.CreateText(card.transform, "Locked", 30, TextAlignmentOptions.Center, dimText);
            locked.text = $"LOCKED\n<size=80%>Clear level {index} first</size>";
            RuntimeUi.Place(locked.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(CardWidth - 30f, 110f));
            return;
        }

        for (int s = 0; s < 3; s++)
        {
            UIStar star = RuntimeUi.CreateStar(card.transform, $"Star {s + 1}", s < stars ? accentColor : starOffColor);
            RuntimeUi.Place(star.rectTransform, new Vector2(0.5f, 0f), new Vector2((s - 1) * 70f, 110f), new Vector2(58f, 58f));
        }

        TextMeshProUGUI bestText = RuntimeUi.CreateText(card.transform, "Best", 28, TextAlignmentOptions.Center, dimText);
        bestText.text = best >= 0f ? $"Best {LevelManager.FormatTime(best)}" : "Not cleared yet";
        RuntimeUi.Place(bestText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(CardWidth - 30f, 44f));
    }
}
