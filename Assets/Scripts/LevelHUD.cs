using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// In-game HUD (hearts, timer, enemies left) and the pause / level clear / game over screens.
/// The UI is built in code so every level gets the same HUD from a single component.
/// </summary>
[RequireComponent(typeof(LevelManager))]
public sealed class LevelHUD : MonoBehaviour
{
    private const string Heart = "♥";

    [Header("Colors")]
    [SerializeField] private Color textColor = new(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color heartColor = new(0.9f, 0.2f, 0.25f, 1f);
    [SerializeField] private Color emptyHeartColor = new(0.3f, 0.3f, 0.33f, 1f);
    [SerializeField] private Color warningColor = new(1f, 0.35f, 0.3f, 1f);
    [SerializeField] private Color accentColor = new(1f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color panelColor = new(0.08f, 0.09f, 0.12f, 0.95f);
    [SerializeField] private Color buttonColor = new(0.22f, 0.25f, 0.32f, 1f);

    [Header("Hit feedback")]
    [SerializeField, Min(0f)] private float hitShakeStrength = 0.25f;
    [SerializeField, Min(0f)] private float hitShakeDuration = 0.3f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float introDuration = 3f;
    [SerializeField, Min(0f)] private float lowTimeWarning = 10f;

    private LevelManager level;
    private CameraFollow2D cameraFollow;
    private TextMeshProUGUI heartsText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI enemiesText;
    private CanvasGroup introGroup;
    private Image damageFlash;
    private GameObject pausePanel;
    private GameObject wonPanel;
    private GameObject lostPanel;
    private TextMeshProUGUI wonTitle;
    private TextMeshProUGUI wonBody;
    private TextMeshProUGUI wonNextLabel;
    private TextMeshProUGUI lostBody;
    private float introStartedAt;
    private int lastHearts = -1;

    private void Awake()
    {
        level = GetComponent<LevelManager>();
        if (Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
        EnsureEventSystem();
        BuildUi();
    }

    private void OnEnable()
    {
        level.StateChanged += RefreshPanels;
        level.EnemiesChanged += RefreshEnemies;
    }

    private void Start()
    {
        // The player is resolved in LevelManager.Awake, so subscribe here.
        if (level.Player != null)
        {
            level.Player.HeartsChanged += OnHeartsChanged;
            OnHeartsChanged(level.Player.CurrentHearts, level.Player.MaxHearts);
        }

        introStartedAt = Time.unscaledTime;
        RefreshEnemies();
        RefreshPanels();
    }

    private void OnDisable()
    {
        level.StateChanged -= RefreshPanels;
        level.EnemiesChanged -= RefreshEnemies;
        if (level.Player != null)
            level.Player.HeartsChanged -= OnHeartsChanged;
    }

    private void Update()
    {
        RefreshTimer();

        float introT = (Time.unscaledTime - introStartedAt) / Mathf.Max(0.01f, introDuration);
        introGroup.alpha = introT < 0.75f ? 1f : Mathf.Clamp01((1f - introT) / 0.25f);

        if (damageFlash.color.a > 0f)
        {
            Color c = damageFlash.color;
            c.a = Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 1.5f);
            damageFlash.color = c;
        }
    }

    private void RefreshTimer()
    {
        if (level.HasTimeLimit)
        {
            float remaining = level.TimeRemaining;
            bool low = remaining <= lowTimeWarning && level.State == LevelState.Playing;
            timerText.text = LevelManager.FormatTime(remaining);
            timerText.color = low ? warningColor : textColor;
            timerText.rectTransform.localScale = Vector3.one *
                (low ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f);
        }
        else
        {
            timerText.text = LevelManager.FormatTime(level.ElapsedTime);
        }
    }

    private void RefreshEnemies()
    {
        enemiesText.text = $"ENEMIES  <color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>" +
                           $"{level.EnemiesRemaining}</color> / {level.EnemiesTotal}";
    }

    private void OnHeartsChanged(int current, int max)
    {
        var sb = new StringBuilder();
        sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(heartColor)).Append('>');
        for (int i = 0; i < current; i++) sb.Append(Heart);
        sb.Append("</color><color=#").Append(ColorUtility.ToHtmlStringRGB(emptyHeartColor)).Append('>');
        for (int i = current; i < max; i++) sb.Append(Heart);
        sb.Append("</color>");
        heartsText.text = sb.ToString();

        if (lastHearts >= 0 && current < lastHearts)
        {
            damageFlash.color = new Color(warningColor.r, warningColor.g, warningColor.b, 0.35f);
            if (cameraFollow != null)
                cameraFollow.Shake(hitShakeStrength, hitShakeDuration);
        }
        lastHearts = current;
    }

    private void RefreshPanels()
    {
        pausePanel.SetActive(level.State == LevelState.Paused);
        wonPanel.SetActive(level.State == LevelState.Won);
        lostPanel.SetActive(level.State == LevelState.Lost);

        if (level.State == LevelState.Won)
        {
            wonTitle.text = level.HasNextLevel ? "LEVEL CLEARED!" : "YOU BEAT THE GAME!";
            string best = level.IsNewBestTime
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>NEW BEST TIME!</color>"
                : $"Best: {LevelManager.FormatTime(level.BestTime)}";
            wonBody.text = $"Time: {LevelManager.FormatTime(level.ElapsedTime)}\n{best}";
            wonNextLabel.text = level.HasNextLevel ? "Next level" : "Main menu";
        }
        else if (level.State == LevelState.Lost)
        {
            lostBody.text = $"{level.LoseReason}\nEnemies left: {level.EnemiesRemaining}";
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ---------- UI construction ----------

    private void BuildUi()
    {
        var canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasObject.GetComponent<RectTransform>();

        damageFlash = CreateImage(root, "Damage Flash", new Color(0f, 0f, 0f, 0f));
        Stretch(damageFlash.rectTransform);
        damageFlash.raycastTarget = false;

        heartsText = CreateText(root, "Hearts", 64, TextAlignmentOptions.TopLeft);
        Place(heartsText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(600f, 90f));

        TextMeshProUGUI title = CreateText(root, "Level Title", 28, TextAlignmentOptions.Top);
        title.text = level.LevelTitle.ToUpperInvariant();
        title.color = new Color(textColor.r, textColor.g, textColor.b, 0.7f);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 40f));

        timerText = CreateText(root, "Timer", 60, TextAlignmentOptions.Top);
        timerText.fontStyle = FontStyles.Bold;
        Place(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(400f, 80f));

        enemiesText = CreateText(root, "Enemies", 40, TextAlignmentOptions.TopRight);
        Place(enemiesText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -32f), new Vector2(600f, 60f));

        TextMeshProUGUI hint = CreateText(root, "Controls Hint", 24, TextAlignmentOptions.BottomLeft);
        hint.text = "WASD move  •  Esc pause  •  R restart";
        hint.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
        Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(900f, 40f));

        BuildIntro(root);
        BuildPausePanel(root);
        BuildWonPanel(root);
        BuildLostPanel(root);
    }

    private void BuildIntro(RectTransform root)
    {
        var introObject = new GameObject("Intro", typeof(RectTransform), typeof(CanvasGroup));
        introObject.transform.SetParent(root, false);
        introGroup = introObject.GetComponent<CanvasGroup>();
        introGroup.blocksRaycasts = false;
        Place((RectTransform)introObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1400f, 220f));

        TextMeshProUGUI title = CreateText(introObject.transform, "Title", 88, TextAlignmentOptions.Center);
        title.text = level.LevelTitle.ToUpperInvariant();
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, 120f));

        TextMeshProUGUI goal = CreateText(introObject.transform, "Objective", 40, TextAlignmentOptions.Center);
        goal.text = level.HasTimeLimit
            ? $"{level.Objective} within {Mathf.RoundToInt(level.TimeLimit)} seconds"
            : level.Objective;
        goal.color = accentColor;
        Place(goal.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1400f, 80f));
    }

    private void BuildPausePanel(RectTransform root)
    {
        pausePanel = CreatePanel(root, "Pause Panel", "PAUSED", 3, out RectTransform box, out _);
        CreateButton(box, "Resume", level.TogglePause, 0);
        CreateButton(box, "Restart (R)", level.Restart, 1);
        CreateButton(box, "Main menu", level.LoadMainMenu, 2);
    }

    private void BuildWonPanel(RectTransform root)
    {
        wonPanel = CreatePanel(root, "Level Cleared Panel", "LEVEL CLEARED!", 3, out RectTransform box, out wonBody);
        wonTitle = box.Find("Title").GetComponent<TextMeshProUGUI>();
        wonTitle.color = accentColor;
        wonNextLabel = CreateButton(box, "Next level", level.LoadNextLevel, 0);
        CreateButton(box, "Play again (R)", level.Restart, 1);
        CreateButton(box, "Main menu", level.LoadMainMenu, 2);
    }

    private void BuildLostPanel(RectTransform root)
    {
        lostPanel = CreatePanel(root, "Game Over Panel", "GAME OVER", 2, out RectTransform box, out lostBody);
        box.Find("Title").GetComponent<TextMeshProUGUI>().color = warningColor;
        CreateButton(box, "Try again (R)", level.Restart, 0);
        CreateButton(box, "Main menu", level.LoadMainMenu, 1);
    }

    private const float ButtonsTop = 290f;
    private const float ButtonSpacing = 95f;

    private GameObject CreatePanel(RectTransform root, string name, string titleText, int buttonCount,
        out RectTransform box, out TextMeshProUGUI body)
    {
        Image dim = CreateImage(root, name, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim.rectTransform);

        Image boxImage = CreateImage(dim.transform, "Box", panelColor);
        box = boxImage.rectTransform;
        Place(box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, ButtonsTop + buttonCount * ButtonSpacing + 30f));

        TextMeshProUGUI title = CreateText(box, "Title", 64, TextAlignmentOptions.Center);
        title.text = titleText;
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(680f, 90f));

        body = CreateText(box, "Body", 34, TextAlignmentOptions.Center);
        Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(660f, 120f));

        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    private TextMeshProUGUI CreateButton(RectTransform box, string label, UnityAction onClick, int index)
    {
        Image image = CreateImage(box, label + " Button", buttonColor);
        Place(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -ButtonsTop - index * ButtonSpacing), new Vector2(460f, 76f));

        var button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(image.transform, "Label", 36, TextAlignmentOptions.Center);
        text.text = label;
        Stretch(text.rectTransform);
        return text;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = textColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
