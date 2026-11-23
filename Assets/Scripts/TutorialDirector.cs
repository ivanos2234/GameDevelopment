using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Walks a new player through the game in the tutorial level: what the game is, moving, spinning the saw,
/// breaking a dummy, hearts, and finally the time limit with a short timed challenge.
/// Holds the level's timer and win until the last step.
/// </summary>
[RequireComponent(typeof(LevelManager))]
public sealed class TutorialDirector : MonoBehaviour
{
    public const string DoneKey = "Tutorial.Done";

    [SerializeField] private Rigidbody2D sawBody;
    [SerializeField, Tooltip("Enabled for the 'break a dummy' step.")] private EnemyHealth2D firstDummy;
    [SerializeField, Tooltip("Enabled for the timed final step.")] private EnemyHealth2D[] finalDummies;

    [Header("Goals")]
    [SerializeField, Min(0f)] private float moveDistance = 6f;
    [SerializeField, Min(0f)] private float spinSpeed = 650f;
    [SerializeField, Min(0f)] private float spinSeconds = 1f;

    private enum Step { Welcome, Move, Spin, BreakDummy, Hearts, TimeLimit, Done }

    private LevelManager level;
    private UiTheme theme;
    private Step step;
    private Transform player;
    private Vector2 lastPlayerPosition;
    private float distanceMoved;
    private float spinTime;

    private CanvasGroup panelGroup;
    private TextMeshProUGUI stepCounter;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI progressText;
    private UIPixelIcon timerArrow;
    private GameObject continueButton;

    private void Awake()
    {
        level = GetComponent<LevelManager>();
        theme = UiTheme.Current;
        level.TimerRunning = false;
        level.WinBlocked = true;
        BuildPanel();
    }

    private void Start()
    {
        player = level.Player != null ? level.Player.transform : null;
        // The dojo is a safe place to learn: nothing hurts here.
        if (level.Player != null)
            level.Player.Invulnerable = true;
        if (player != null)
            lastPlayerPosition = player.position;

        level.StateChanged += OnLevelStateChanged;
        ShowStep(Step.Welcome);
    }

    private void OnDestroy()
    {
        if (level != null)
            level.StateChanged -= OnLevelStateChanged;
    }

    private void OnLevelStateChanged()
    {
        if (level.State == LevelState.Won)
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        panelGroup.alpha = level.State == LevelState.Playing ? 1f : 0f;
    }

    private void Update()
    {
        if (level.State != LevelState.Playing)
            return;

        if (timerArrow.gameObject.activeSelf)
            timerArrow.rectTransform.anchoredPosition = new Vector2(-30f, -170f - 12f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));

        switch (step)
        {
            case Step.Welcome:
            case Step.Hearts:
                if (ContinuePressed())
                    ShowStep(step + 1);
                break;

            case Step.Move:
                if (player != null)
                {
                    distanceMoved += Vector2.Distance(player.position, lastPlayerPosition);
                    lastPlayerPosition = player.position;
                }
                progressText.text = Progress(distanceMoved / moveDistance);
                if (distanceMoved >= moveDistance)
                    ShowStep(Step.Spin);
                break;

            case Step.Spin:
                if (sawBody != null && Mathf.Abs(sawBody.angularVelocity) >= spinSpeed)
                    spinTime += Time.deltaTime;
                progressText.text = Progress(spinTime / spinSeconds);
                if (spinTime >= spinSeconds)
                    ShowStep(Step.BreakDummy);
                break;

            case Step.BreakDummy:
                if (firstDummy == null)
                    ShowStep(Step.Hearts);
                break;
        }
    }

    private static bool ContinuePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
    }

    /// <summary>Moves on from an information step; the panel's button calls this too.</summary>
    public void Continue()
    {
        if (step == Step.Welcome || step == Step.Hearts)
            ShowStep(step + 1);
    }

    private static string Progress(float fraction)
    {
        int filled = Mathf.Clamp(Mathf.FloorToInt(fraction * 10f), 0, 10);
        return new string('#', filled) + new string('-', 10 - filled);
    }

    private void ShowStep(Step next)
    {
        step = next;
        progressText.text = "";
        timerArrow.gameObject.SetActive(false);
        continueButton.SetActive(step == Step.Welcome || step == Step.Hearts);
        stepCounter.text = step < Step.Done ? $"STEP {(int)step + 1} / {(int)Step.Done}" : "";

        switch (step)
        {
            case Step.Welcome:
                Say("WELCOME, NINJA",
                    "You fight with a saw blade on a chain. Destroy every enemy\nin the dojo before the time runs out.",
                    "Press Enter to continue");
                break;

            case Step.Move:
                Say("MOVE", "Walk around with WASD or the arrow keys.", "");
                break;

            case Step.Spin:
                Say("SPIN THE SAW", "The saw swings behind you. Run in circles to spin it fast:\nthe faster it spins, the more damage it does.", "");
                break;

            case Step.BreakDummy:
                Say("BREAK THE DUMMY", "A training dummy appeared. Swing the saw through it until it breaks.", "");
                Activate(firstDummy);
                break;

            case Step.Hearts:
                Say("HEARTS", "Outside the dojo, enemies hurt you on touch and so do their shots. You have\n5 hearts: lose them all and it's game over. Heart pickups heal you.",
                    "Press Enter to continue");
                break;

            case Step.TimeLimit:
                Say("BEAT THE CLOCK", "Every level has a time limit - watch the timer at the top.\nBreak both dummies before it runs out!", "");
                timerArrow.gameObject.SetActive(true);
                foreach (EnemyHealth2D dummy in finalDummies)
                    Activate(dummy);
                level.WinBlocked = false;
                level.TimerRunning = true;
                break;
        }
    }

    private void Activate(EnemyHealth2D enemy)
    {
        if (enemy == null)
            return;

        enemy.gameObject.SetActive(true);
        level.RegisterEnemy(enemy);
    }

    private void Say(string title, string body, string footer)
    {
        titleText.text = title;
        bodyText.text = body;
        progressText.text = footer;
    }

    // ---------- UI ----------

    private void BuildPanel()
    {
        RectTransform root = RuntimeUi.CreateCanvas(transform, "Tutorial Canvas", 90);

        Image box = RuntimeUi.CreateBox(root, "Tutorial Panel", theme.panel, theme.gold, 4f, new Color(0.02f, 0.03f, 0.05f, 1f), 12f);
        RuntimeUi.Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1240f, 250f));
        panelGroup = box.gameObject.AddComponent<CanvasGroup>();

        stepCounter = RuntimeUi.CreateText(box.transform, "Step", 33, TextAlignmentOptions.TopRight, theme.muted);
        RuntimeUi.PlaceTopLeft(stepCounter.rectTransform, 840f, 22f, 370f, 40f);

        titleText = RuntimeUi.CreateText(box.transform, "Title", 30, TextAlignmentOptions.TopLeft, theme.gold, UiFont.Title);
        RuntimeUi.PlaceTopLeft(titleText.rectTransform, 36f, 28f, 800f, 44f);

        bodyText = RuntimeUi.CreateText(box.transform, "Body", 42, TextAlignmentOptions.TopLeft, theme.paper);
        bodyText.lineSpacing = 4f;
        RuntimeUi.PlaceTopLeft(bodyText.rectTransform, 36f, 86f, 1170f, 110f);

        progressText = RuntimeUi.CreateText(box.transform, "Progress", 36, TextAlignmentOptions.BottomLeft, theme.muted);
        RuntimeUi.Place(progressText.rectTransform, new Vector2(0f, 0f), new Vector2(36f, 16f), new Vector2(900f, 44f));

        Button next = RuntimeUi.CreateButton(box.transform, "Continue Button", "CONTINUE", true, Continue, 20f, out _);
        RuntimeUi.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(260f, 62f));
        continueButton = next.gameObject;

        timerArrow = RuntimeUi.CreateIcon(root, "Timer Arrow", PixelIcon.ArrowRight, theme.gold, theme.gold);
        RuntimeUi.Place(timerArrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(-30f, -170f), new Vector2(54f, 90f));
        timerArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        timerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        timerArrow.gameObject.SetActive(false);
    }
}
