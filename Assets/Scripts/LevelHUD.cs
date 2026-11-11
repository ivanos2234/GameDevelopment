using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// In-game HUD in the pixel-art style (hearts, timer with the stars still in reach, enemies left, tips,
/// arrows to off-screen enemies) and the pause / level cleared / game over screens.
/// Built in code so every level gets the same HUD from one component; colors and fonts come from <see cref="UiTheme"/>.
/// </summary>
[RequireComponent(typeof(LevelManager))]
public sealed class LevelHUD : MonoBehaviour
{
    private const float BarHeight = 114f;
    private const int MaxEnemyMarkers = 14;
    private const int MaxArrows = 3;

    [Header("Hit feedback")]
    [SerializeField, Min(0f)] private float hitShakeStrength = 0.25f;
    [SerializeField, Min(0f)] private float hitShakeDuration = 0.3f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float introDuration = 3f;
    [SerializeField, Min(0f)] private float tipDuration = 8f;
    [SerializeField, Min(0f)] private float lowTimeWarning = 10f;

    [Header("Off-screen arrows")]
    [SerializeField, Min(0), Tooltip("Show arrows to off-screen enemies once this many or fewer are left.")]
    private int arrowsWhenEnemiesLeft = 3;

    private LevelManager level;
    private UiTheme theme;
    private Camera worldCamera;
    private CameraFollow2D cameraFollow;
    private RectTransform root;
    private RectTransform heartsRoot;

    private readonly List<UIPixelIcon> hearts = new();
    private TextMeshProUGUI timerText;
    private readonly UIPixelIcon[] timerStars = new UIPixelIcon[3];
    private TextMeshProUGUI enemiesText;
    private TextMeshProUGUI enemiesLabel;
    private TextMeshProUGUI orderText;
    private readonly List<Image> enemyMarkers = new();
    private CanvasGroup introGroup;
    private CanvasGroup tipGroup;
    private readonly List<UIPixelIcon> arrows = new();
    private Image damageFlash;

    private GameObject pausePanel;
    private TextMeshProUGUI pauseSoundLabel;
    private GameObject wonPanel;
    private TextMeshProUGUI wonTitle;
    private readonly UIPixelIcon[] wonStars = new UIPixelIcon[3];
    private TextMeshProUGUI wonStatsLeft;
    private TextMeshProUGUI wonStatsRight;
    private TextMeshProUGUI wonNextLabel;
    private GameObject lostPanel;
    private TextMeshProUGUI lostBody;

    private float introStartedAt;
    private int lastHearts = -1;

    private void Awake()
    {
        level = GetComponent<LevelManager>();
        theme = UiTheme.Current;
        worldCamera = Camera.main;
        if (worldCamera != null)
            cameraFollow = worldCamera.GetComponent<CameraFollow2D>();
        RuntimeUi.EnsureEventSystem();
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
        RefreshArrows();

        float sinceStart = Time.unscaledTime - introStartedAt;
        introGroup.alpha = Fade(sinceStart, 0f, introDuration);
        if (tipGroup != null)
            tipGroup.alpha = Fade(sinceStart, introDuration * 0.8f, introDuration * 0.8f + tipDuration);

        if (damageFlash.color.a > 0f)
        {
            Color c = damageFlash.color;
            c.a = Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 1.5f);
            damageFlash.color = c;
        }
    }

    // 1 between start and end, with a short fade in and a longer fade out.
    private static float Fade(float time, float start, float end)
    {
        if (time < start || time > end)
            return 0f;
        float fadeIn = Mathf.Clamp01((time - start) / 0.2f);
        float fadeOut = Mathf.Clamp01((end - time) / ((end - start) * 0.25f));
        return Mathf.Min(fadeIn, fadeOut);
    }

    private void RefreshTimer()
    {
        bool low = level.HasTimeLimit && level.TimeRemaining <= lowTimeWarning && level.State == LevelState.Playing;
        timerText.text = LevelManager.FormatTime(level.HasTimeLimit ? level.TimeRemaining : level.ElapsedTime);
        timerText.color = low ? theme.heart : theme.paper;
        timerText.rectTransform.localScale = Vector3.one *
            (low ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f);

        // The stars that can still be earned at the current time.
        int reachable = 1 + (level.ElapsedTime <= level.TwoStarTime ? 1 : 0) + (level.ElapsedTime <= level.ThreeStarTime ? 1 : 0);
        for (int i = 0; i < timerStars.Length; i++)
            timerStars[i].color = i < reachable ? theme.gold : theme.borderDim;
    }

    private void RefreshEnemies()
    {
        enemiesText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(theme.gold)}>{level.EnemiesRemaining}</color> / {level.EnemiesTotal}";

        // Lay the label and the markers out right to left, so two-digit counts never overlap them.
        float right = 48f + enemiesText.GetPreferredValues(enemiesText.text).x + 22f;
        enemiesLabel.rectTransform.anchoredPosition = new Vector2(-right, enemiesLabel.rectTransform.anchoredPosition.y);
        right += enemiesLabel.GetPreferredValues(enemiesLabel.text).x + 26f;
        for (int m = 0; m < enemyMarkers.Count; m++)
            enemyMarkers[m].rectTransform.anchoredPosition = new Vector2(-right - m * 21f, enemyMarkers[m].rectTransform.anchoredPosition.y);

        int i = 0;
        foreach (EnemyHealth2D enemy in level.LivingEnemies)
        {
            if (i >= enemyMarkers.Count)
                break;
            enemyMarkers[i].gameObject.SetActive(true);
            enemyMarkers[i].color = theme.EnemyColor(enemy.Kind);
            i++;
        }
        for (; i < enemyMarkers.Count; i++)
            enemyMarkers[i].gameObject.SetActive(false);

        if (orderText != null)
            orderText.text = level.NextKillOrder > 0 ? $"KILL ORDER   NEXT: <color=#{ColorUtility.ToHtmlStringRGB(theme.gold)}>#{level.NextKillOrder}</color>" : "KILL ORDER   DONE";
    }

    private void RefreshArrows()
    {
        int shown = 0;
        bool active = level.State == LevelState.Playing && level.EnemiesRemaining <= arrowsWhenEnemiesLeft && worldCamera != null;
        if (active)
        {
            Vector2 canvasSize = root.rect.size;
            foreach (EnemyHealth2D enemy in level.LivingEnemies)
            {
                if (shown >= arrows.Count || enemy == null)
                    break;

                Vector3 viewport = worldCamera.WorldToViewportPoint(enemy.transform.position);
                if (viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                    continue;

                // Point from the screen center towards the enemy and park the arrow on the screen edge.
                Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                Vector2 halfSize = canvasSize * 0.5f - new Vector2(70f, 70f);
                halfSize.y -= BarHeight * 0.5f;
                float scale = Mathf.Min(Mathf.Abs(halfSize.x / (direction.x * canvasSize.x + 0.0001f)),
                                        Mathf.Abs(halfSize.y / (direction.y * canvasSize.y + 0.0001f)));
                Vector2 position = new Vector2(direction.x * canvasSize.x, direction.y * canvasSize.y) * scale;
                position.y -= BarHeight * 0.5f;

                UIPixelIcon arrow = arrows[shown];
                arrow.gameObject.SetActive(true);
                arrow.color = theme.EnemyColor(enemy.Kind);
                arrow.rectTransform.anchoredPosition = position;
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                arrow.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
                shown++;
            }
        }

        for (int i = shown; i < arrows.Count; i++)
            arrows[i].gameObject.SetActive(false);
    }

    private void OnHeartsChanged(int current, int max)
    {
        while (hearts.Count < max)
        {
            UIPixelIcon heart = RuntimeUi.CreateIcon(heartsRoot, $"Heart {hearts.Count + 1}", PixelIcon.Heart, theme.heart, theme.heartHighlight);
            RuntimeUi.Place(heart.rectTransform, new Vector2(0f, 1f), new Vector2(48f + hearts.Count * 64f, -(BarHeight - 45f) / 2f), new Vector2(52f, 45f));
            hearts.Add(heart);
        }

        for (int i = 0; i < hearts.Count; i++)
        {
            bool full = i < current;
            hearts[i].gameObject.SetActive(i < max);
            hearts[i].color = full ? theme.heart : theme.borderDim;
            hearts[i].Highlight = full ? theme.heartHighlight : theme.borderDim;
        }

        if (lastHearts >= 0 && current < lastHearts)
        {
            damageFlash.color = UiTheme.WithAlpha(theme.heart, 0.3f);
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
        pauseSoundLabel.text = Sfx.Muted ? "SOUND: OFF" : "SOUND: ON";

        if (level.State == LevelState.Won)
        {
            wonTitle.text = level.HasNextLevel ? "LEVEL CLEARED!" : "YOU BEAT THE GAME!";
            for (int i = 0; i < wonStars.Length; i++)
            {
                bool earned = i < level.StarsEarned;
                wonStars[i].color = earned ? theme.gold : theme.borderDim;
                wonStars[i].Highlight = earned ? Color.white : theme.borderDim;
            }

            string gold = ColorUtility.ToHtmlStringRGB(theme.gold);
            string nextStar = level.StarsEarned switch
            {
                1 => $"under {LevelManager.FormatTime(level.TwoStarTime)}",
                2 => $"under {LevelManager.FormatTime(level.ThreeStarTime)}",
                _ => $"<color=#{gold}>PERFECT!</color>",
            };
            string best = level.IsNewBestTime
                ? $"<color=#{gold}>NEW BEST!</color>"
                : $"<color=#{gold}>{LevelManager.FormatTime(level.BestTime)}</color>";
            int heartsLeft = level.Player != null ? level.Player.CurrentHearts : 0;
            int maxHearts = level.Player != null ? level.Player.MaxHearts : 0;

            wonStatsLeft.text = "TIME\nBEST\nHEARTS LEFT\n" + (level.StarsEarned < 3 ? "NEXT STAR" : "STARS");
            wonStatsRight.text = $"{LevelManager.FormatTime(level.ElapsedTime)}\n{best}\n{heartsLeft} / {maxHearts}\n{nextStar}";
            wonNextLabel.text = level.HasNextLevel ? "NEXT LEVEL" : "MAIN MENU";
        }
        else if (level.State == LevelState.Lost)
        {
            lostBody.text = $"{level.LoseReason}\n<color=#{ColorUtility.ToHtmlStringRGB(theme.muted)}>Enemies left: {level.EnemiesRemaining}</color>";
        }
    }

    private void ToggleSound()
    {
        Sfx.Muted = !Sfx.Muted;
        RefreshPanels();
    }

    // ---------- UI construction ----------

    private void BuildUi()
    {
        root = RuntimeUi.CreateCanvas(transform, "HUD Canvas", 100);

        damageFlash = RuntimeUi.CreateImage(root, "Damage Flash", Color.clear);
        RuntimeUi.Stretch(damageFlash.rectTransform);
        damageFlash.raycastTarget = false;

        BuildTopBar();
        BuildArrows();

        TextMeshProUGUI hint = RuntimeUi.CreateText(root, "Controls Hint", 33, TextAlignmentOptions.BottomLeft, UiTheme.WithAlpha(theme.muted, 0.8f));
        hint.text = "WASD move  ·  Esc pause  ·  R restart  ·  M sound";
        RuntimeUi.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 18f), new Vector2(1200f, 44f));

        BuildIntro();
        BuildTip();
        BuildPausePanel();
        BuildWonPanel();
        BuildLostPanel();
    }

    private void BuildTopBar()
    {
        Image bar = RuntimeUi.CreateImage(root, "Top Bar", theme.bar);
        RuntimeUi.Place(bar.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, BarHeight));
        bar.raycastTarget = false;
        Image line = RuntimeUi.CreateImage(bar.transform, "Line", theme.barLine);
        RuntimeUi.Place(line.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 4f));

        // Hearts are added once the player's max hearts are known; this keeps them under the panels.
        heartsRoot = (RectTransform)new GameObject("Hearts", typeof(RectTransform)).transform;
        heartsRoot.SetParent(root, false);
        RuntimeUi.Stretch(heartsRoot);

        TextMeshProUGUI title = RuntimeUi.CreateText(root, "Level Title", 33, TextAlignmentOptions.Top, theme.muted);
        title.text = level.LevelTitle.ToUpperInvariant().Replace(":", " ·") +
                     (Difficulty.Level == DifficultyLevel.Easy ? "  ·  EASY" : "");
        RuntimeUi.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1100f, 40f));

        timerText = RuntimeUi.CreateText(root, "Timer", 36, TextAlignmentOptions.Center, theme.paper, UiFont.Title);
        RuntimeUi.Place(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-30f, -50f), new Vector2(320f, 48f));

        for (int i = 0; i < timerStars.Length; i++)
        {
            timerStars[i] = RuntimeUi.CreateIcon(root, $"Timer Star {i + 1}", PixelIcon.Star, theme.gold, Color.white);
            RuntimeUi.Place(timerStars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(150f + i * 30f, -61f), new Vector2(24f, 24f));
        }

        for (int i = 0; i < MaxEnemyMarkers; i++)
        {
            Image marker = RuntimeUi.CreateImage(root, $"Enemy Marker {i + 1}", theme.dummy);
            RuntimeUi.Place(marker.rectTransform, new Vector2(1f, 1f), new Vector2(-420f - i * 21f, -50f), new Vector2(15f, 15f));
            marker.raycastTarget = false;
            enemyMarkers.Add(marker);
        }

        enemiesLabel = RuntimeUi.CreateText(root, "Enemies Label", 39, TextAlignmentOptions.Right, theme.muted);
        enemiesLabel.text = "ENEMIES";
        RuntimeUi.Place(enemiesLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-260f, -34f), new Vector2(180f, 46f));

        if (level.KillInOrder)
        {
            Image orderBox = RuntimeUi.CreateBox(root, "Kill Order", theme.bar, theme.gold, 4f, Color.clear, 0f);
            RuntimeUi.Place(orderBox.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -BarHeight - 120f), new Vector2(460f, 62f));
            orderBox.raycastTarget = false;
            orderText = RuntimeUi.CreateText(orderBox.transform, "Text", 39, TextAlignmentOptions.Center, theme.paper);
            RuntimeUi.Stretch(orderText.rectTransform);
        }

        enemiesText = RuntimeUi.CreateText(root, "Enemies", 33, TextAlignmentOptions.Right, theme.paper, UiFont.Title);
        RuntimeUi.Place(enemiesText.rectTransform, new Vector2(1f, 1f), new Vector2(-48f, -36f), new Vector2(300f, 44f));
    }

    private void BuildArrows()
    {
        for (int i = 0; i < MaxArrows; i++)
        {
            UIPixelIcon arrow = RuntimeUi.CreateIcon(root, $"Offscreen Arrow {i + 1}", PixelIcon.ArrowRight, theme.paper, theme.paper);
            RuntimeUi.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 60f));
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            arrow.gameObject.SetActive(false);
            arrows.Add(arrow);
        }
    }

    private void BuildIntro()
    {
        var introObject = new GameObject("Intro", typeof(RectTransform), typeof(CanvasGroup));
        introObject.transform.SetParent(root, false);
        introGroup = introObject.GetComponent<CanvasGroup>();
        introGroup.blocksRaycasts = false;
        RuntimeUi.Place((RectTransform)introObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1800f, 220f));

        TextMeshProUGUI title = RuntimeUi.CreateText(introObject.transform, "Title", 60, TextAlignmentOptions.Center, theme.paper, UiFont.Title);
        title.text = level.LevelTitle.ToUpperInvariant();
        RuntimeUi.Place(title.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1800f, 110f));

        LevelAmbience ambience = FindFirstObjectByType<LevelAmbience>();
        if (ambience != null && !string.IsNullOrEmpty(ambience.ThemeName))
        {
            TextMeshProUGUI themeLine = RuntimeUi.CreateText(introObject.transform, "Theme", 42, TextAlignmentOptions.Center, theme.soft);
            themeLine.text = $"\u2014 {ambience.ThemeName} \u2014";
            RuntimeUi.Place(themeLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 64f), new Vector2(1800f, 56f));
        }
        UiTextShadow.Add(title, theme.red, new Vector2(7f, -7f));

        TextMeshProUGUI goal = RuntimeUi.CreateText(introObject.transform, "Objective", 48, TextAlignmentOptions.Center, theme.gold);
        // The tutorial holds its clock until the last step, so it does not promise a time yet.
        bool timedFromStart = level.HasTimeLimit && GetComponent<TutorialDirector>() == null;
        goal.text = timedFromStart
            ? $"{level.Objective} within {Mathf.RoundToInt(level.TimeLimit)} seconds"
            : level.Objective;
        RuntimeUi.Place(goal.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1800f, 70f));
    }

    private void BuildTip()
    {
        if (string.IsNullOrWhiteSpace(level.Tip))
            return;

        Image box = RuntimeUi.CreateBox(root, "Tip", theme.bar, theme.gold, 4f, Color.clear, 0f);
        box.raycastTarget = false;
        tipGroup = box.gameObject.AddComponent<CanvasGroup>();
        tipGroup.blocksRaycasts = false;
        tipGroup.alpha = 0f;

        TextMeshProUGUI label = RuntimeUi.CreateText(box.transform, "Label", 18, TextAlignmentOptions.Left, theme.gold, UiFont.Title);
        label.text = "TIP";
        RuntimeUi.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(80f, 40f));

        TextMeshProUGUI text = RuntimeUi.CreateText(box.transform, "Text", 39, TextAlignmentOptions.Left, theme.paper);
        text.text = level.Tip;

        // Size the box to the tip, up to most of the screen width.
        float textWidth = Mathf.Min(text.GetPreferredValues(level.Tip).x, 1500f);
        RuntimeUi.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 2f), new Vector2(textWidth + 10f, 60f));
        RuntimeUi.Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BarHeight - 34f), new Vector2(textWidth + 150f, 70f));
    }

    private void BuildPausePanel()
    {
        RectTransform box = CreatePanel("Pause Panel", 640f, 560f, out pausePanel);
        TextMeshProUGUI title = CreatePanelTitle(box, "PAUSED", theme.paper, theme.red, -46f);
        title.fontSize = 45;

        CreatePanelButton(box, "RESUME", true, level.TogglePause, -150f, 480f);
        CreatePanelButton(box, "RESTART", false, level.Restart, -250f, 480f);
        pauseSoundLabel = CreatePanelButton(box, "SOUND: ON", false, ToggleSound, -350f, 480f);
        CreatePanelButton(box, "MAIN MENU", false, level.LoadMainMenu, -450f, 480f);
    }

    private void BuildWonPanel()
    {
        RectTransform box = CreatePanel("Level Cleared Panel", 900f, 900f, out wonPanel);

        TextMeshProUGUI levelLine = RuntimeUi.CreateText(box, "Level", 39, TextAlignmentOptions.Center, theme.muted);
        levelLine.text = level.LevelTitle.ToUpperInvariant().Replace(":", " ·");
        RuntimeUi.Place(levelLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 46f));

        wonTitle = CreatePanelTitle(box, "LEVEL CLEARED!", theme.gold, theme.goldShadow, -100f);

        for (int i = 0; i < wonStars.Length; i++)
        {
            bool middle = i == 1;
            float size = middle ? 108f : 84f;
            wonStars[i] = RuntimeUi.CreateIcon(box, $"Star {i + 1}", PixelIcon.Star, theme.gold, Color.white);
            RuntimeUi.Place(wonStars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 126f, middle ? -176f : -200f), new Vector2(size, size));
        }

        wonStatsLeft = RuntimeUi.CreateText(box, "Stats Labels", 45, TextAlignmentOptions.TopLeft, theme.muted);
        wonStatsLeft.lineSpacing = 6f;
        RuntimeUi.Place(wonStatsLeft.rectTransform, new Vector2(0.5f, 1f), new Vector2(-180f, -316f), new Vector2(360f, 230f));
        wonStatsRight = RuntimeUi.CreateText(box, "Stats Values", 45, TextAlignmentOptions.TopRight, theme.paper);
        wonStatsRight.lineSpacing = 6f;
        RuntimeUi.Place(wonStatsRight.rectTransform, new Vector2(0.5f, 1f), new Vector2(180f, -316f), new Vector2(360f, 230f));

        wonNextLabel = CreatePanelButton(box, "NEXT LEVEL", true, level.LoadNextLevel, -580f, 720f);
        CreatePanelButton(box, "RETRY", false, level.Restart, -690f, 348f, -186f);
        CreatePanelButton(box, "MENU", false, level.LoadMainMenu, -690f, 348f, 186f);

        TextMeshProUGUI footer = RuntimeUi.CreateText(box, "Hint", 33, TextAlignmentOptions.Center, theme.muted);
        footer.text = "Enter next level  ·  R retry";
        RuntimeUi.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(820f, 40f));
    }

    private void BuildLostPanel()
    {
        RectTransform box = CreatePanel("Game Over Panel", 800f, 560f, out lostPanel);
        CreatePanelTitle(box, "GAME OVER", theme.heart, theme.redShadow, -50f);

        lostBody = RuntimeUi.CreateText(box, "Body", 48, TextAlignmentOptions.Center, theme.paper);
        RuntimeUi.Place(lostBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(720f, 130f));

        CreatePanelButton(box, "TRY AGAIN", true, level.Restart, -320f, 560f);
        CreatePanelButton(box, "MAIN MENU", false, level.LoadMainMenu, -430f, 560f);
    }

    private RectTransform CreatePanel(string name, float width, float height, out GameObject panel)
    {
        Image dim = RuntimeUi.CreateImage(root, name, new Color(0.02f, 0.03f, 0.05f, 0.72f));
        RuntimeUi.Stretch(dim.rectTransform);

        Image box = RuntimeUi.CreateBox(dim.transform, "Box", theme.panel, theme.border, 6f, new Color(0.02f, 0.03f, 0.05f, 1f), 15f);
        RuntimeUi.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));

        panel = dim.gameObject;
        panel.SetActive(false);
        return box.rectTransform;
    }

    private TextMeshProUGUI CreatePanelTitle(RectTransform box, string text, Color color, Color shadow, float y)
    {
        TextMeshProUGUI title = RuntimeUi.CreateText(box, "Title", 42, TextAlignmentOptions.Center, color, UiFont.Title);
        title.text = text;
        RuntimeUi.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, 64f));
        UiTextShadow.Add(title, shadow, new Vector2(6f, -6f));
        return title;
    }

    private TextMeshProUGUI CreatePanelButton(RectTransform box, string label, bool primary, UnityAction onClick,
        float y, float width, float x = 0f)
    {
        RuntimeUi.CreateButton(box, label + " Button", label, primary, onClick, primary ? 27f : 24f, out TextMeshProUGUI text);
        RuntimeUi.Place((RectTransform)text.transform.parent, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(width, 84f));
        return text;
    }
}
